﻿using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace switcheo
{
    public class BrokerContract : SmartContract
    {
        public delegate object NEP5Contract(string method, object[] args);

        [DisplayName("created")]
        public static event Action<byte[], byte[], BigInteger, byte[], BigInteger> Created; // (offerHash, offerAssetID, offerAmount, wantAssetID, wantAmount)

        [DisplayName("filled")]
        public static event Action<byte[], byte[], BigInteger, byte[], BigInteger, byte[], BigInteger> Filled; // (address, offerHash, fillAmount, offerAssetID, offerAmount, wantAssetID, wantAmount)

        [DisplayName("failed")]
        public static event Action<byte[], byte[]> Failed; // (address, offerHash)

        [DisplayName("cancelled")]
        public static event Action<byte[]> Cancelled; // (offerHash)

        [DisplayName("transferred")]
        public static event Action<byte[], byte[], BigInteger> Transferred; // (address, assetID, amount)

        [DisplayName("withdrawn")]
        public static event Action<byte[], byte[], BigInteger> Withdrawn; // (address, assetID, amount)

        private static readonly byte[] Owner = "AHDfSLZANnJ4N9Rj3FCokP14jceu3u7Bvw".ToScriptHash();
        private static readonly byte[] NativeToken = "AYdPyCbHS3MZoJDeZSntgdnDbpa5ScXade".ToScriptHash();
        private const ulong feeFactor = 1000000; // 1 => 0.0001%
        private const int maxFee = 5000; // 5000/1000000 = 0.5%
        private const int stakeDuration = 82800; // 82800secs = 23hrs
        private const int nativeTokenDiscount = 2; // 1/2 => 50%

        // Contract States
        private static readonly byte[] Pending = { };         // only can initialize
        private static readonly byte[] Active = { 0x01 };     // all operations active
        private static readonly byte[] Inactive = { 0x02 };   // trading halted - only can do cancel, withdrawl & owner actions

        // Asset Categories
        private static readonly byte[] SystemAsset = { 0x99 };
        private static readonly byte[] NEP5 = { 0x98 };

        // Flags / Byte Constants
        private static readonly byte[] Empty = { };
        private static readonly byte[] Withdrawing = { 0x50 };
        private static readonly byte[] FeesAccumulated = { 0x60 };
        private static readonly byte[] StakedAmount = { 0x61 };
        private static readonly byte[] StakedTime = { 0x62 };
        private static readonly byte[] StakedTotal = { 0x63 };
        private static readonly byte[] Native = { 0x70 };
        private static readonly byte[] Foreign = { 0x71 };
        private static readonly byte[] Zeroes = { 0, 0, 0, 0, 0, 0, 0, 0 }; // for fixed8 (8 bytes)
        private static readonly byte[] Null = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // for fixed width list ptr (32bytes)        
        private static StorageContext Context() => Storage.CurrentContext;
        private static BigInteger CurrentBucket() => Runtime.Time / stakeDuration;

        private struct Offer
        {
            public byte[] MakerAddress;
            public byte[] OfferAssetID;
            public byte[] OfferAssetCategory;
            public BigInteger OfferAmount;
            public byte[] WantAssetID;
            public byte[] WantAssetCategory;
            public BigInteger WantAmount;
            public BigInteger AvailableAmount;
            public byte[] PreviousOfferHash; // in same trading pair
            public byte[] NextOfferHash;     // in same trading pair
        }

        private static Offer NewOffer(
            byte[] makerAddress,
            byte[] offerAssetID, byte[] offerAmount,
            byte[] wantAssetID, byte[] wantAmount,
            byte[] availableAmount,
            byte[] previousOfferHash, byte[] nextOfferHash
        )
        {
            var offerAssetCategory = NEP5;
            var wantAssetCategory = NEP5;
            if (offerAssetID.Length == 32) offerAssetCategory = SystemAsset;
            if (wantAssetID.Length == 32) wantAssetCategory = SystemAsset;

            return new Offer
            {
                MakerAddress = makerAddress.Take(20),
                OfferAssetID = offerAssetID,
                OfferAssetCategory = offerAssetCategory,
                OfferAmount = offerAmount.AsBigInteger(),
                WantAssetID = wantAssetID,
                WantAssetCategory = wantAssetCategory,
                WantAmount = wantAmount.AsBigInteger(),
                AvailableAmount = availableAmount.AsBigInteger(),
                PreviousOfferHash = previousOfferHash,
                NextOfferHash = nextOfferHash
            };
        }

        /// <summary>
        ///   This is the Switcheo smart contract entrypoint.
        /// 
        ///   Parameter List: 0710
        ///   Return List: 05
        /// </summary>
        /// <param name="operation">
        ///   The method to be invoked.
        /// </param>
        /// <param name="args">
        ///   Input parameters for the delegated method.
        /// </param>
        public static object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                // == Withdrawal of SystemAsset ==
                // Check that the TransactionAttribute has been set to signify deduction for double withdrawal checks
                var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
                if (!IsWithdrawingSystemAsset(currentTxn)) return false;

                // Verify that the contract is initialized
                if (GetState() == Pending) return false;

                // Get the withdrawing address
                var withdrawingAddr = GetWithdrawalAddress(currentTxn);

                // Verify that each output is allowed
                var outputs = currentTxn.GetOutputs();
                ulong totalOut = 0;
                foreach (var o in outputs)
                {
                    // Get amount for each asset
                    var amount = GetAmountForAssetInOutputs(o.AssetId, outputs);
                    // Verify that the output address owns the balance 
                    if (!VerifyWithdrawal(withdrawingAddr, o.AssetId, amount)) return false;
                    // Accumulate total for checking against inputs later
                    totalOut += (ulong)o.Value;
                }

                // Check that all previous withdrawals has been cleared (SC amounts have been updated through invoke)
                var startOfWithdrawal = (uint)Storage.Get(Context(), WithdrawalKey(withdrawingAddr)).AsBigInteger();
                var currentHeight = Blockchain.GetHeight();

                // Check that start of withdrawal has been initiated previously
                if (startOfWithdrawal == 0) return false;

                // Check that withdrawal was not already done
                for (var i = startOfWithdrawal; i < currentHeight; i++)
                {
                    var block = Blockchain.GetBlock(i);
                    var txns = block.GetTransactions();
                    foreach (var transaction in txns)
                    {
                        // Since this is flagged as a withdrawal from this contract,
                        // and it is signed by the withdrawing user,
                        // we know that an withdrawal has already been executed without 
                        // a corresponding application invocation to reduce balance,
                        // therefore we should reject further withdrawals.
                        if (IsWithdrawingSystemAsset(transaction) &&
                            GetWithdrawalAddress(transaction) == withdrawingAddr) return false;
                    }
                }

                // Ensure that nothing is burnt
                ulong totalIn = 0;
                foreach (var i in currentTxn.GetReferences()) totalIn += (ulong)i.Value;
                if (totalIn != totalOut) return false;

                return true;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                // == Init ==
                if (operation == "initialize")
                {
                    if (!Runtime.CheckWitness(Owner))
                    {
                        Runtime.Log("Owner signature verification failed!");
                        return false;
                    }
                    if (args.Length != 3) return false;
                    return Initialize((BigInteger)args[0], (BigInteger)args[1], (byte[])args[2]);
                }

                // == Getters ==
                if (operation == "getState") return GetState();
                if (operation == "getMakerFee") return GetMakerFee();
                if (operation == "getTakerFee") return GetTakerFee();
                if (operation == "getOffers") return GetOffers((byte[])args[0], (BigInteger)args[1]);
                if (operation == "getBalance") return GetBalance((byte[])args[0], (byte[])args[1]);
                if (operation == "getFeeBalance") return GetFeeBalance((byte[])args[0], (BigInteger)args[1]);
                if (operation == "getTotalStaked") return GetTotalStaked((BigInteger)args[0]);
                if (operation == "getStakeDetails")
                {
                    var stakerAddress = (byte[])args[0];
                    return new BigInteger[] {
                        Storage.Get(Context(), StakedAmountKey(stakerAddress)).AsBigInteger(),
                        Storage.Get(Context(), StakedTimeKey(stakerAddress)).AsBigInteger()
                    };
                }

                // == Execute ==
                if (operation == "deposit")
                {
                    if (GetState() != Active) return false;
                    if (IsWithdrawingSystemAsset((Transaction)ExecutionEngine.ScriptContainer)) return false;
                    if (args.Length != 3) return false;
                    TransferAssetTo((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                    return VerifySentAmount((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                }
                if (operation == "makeOffer")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 6) return false;
                    var offer = NewOffer((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3], (byte[])args[4], (byte[])args[2], Null, Null);
                    var offerHash = Hash(offer, (byte[])args[5]);
                    return MakeOffer(offerHash, offer);
                }
                if (operation == "fillOffer")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 4) return false;
                    return FillOffer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], (bool)args[3]);
                }
                if (operation == "stakeTokens")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 2) return false;
                    return StakeTokens((byte[])args[0], (BigInteger)args[1]);
                }
                if (operation == "claimFees")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 2) return false;
                    return ClaimFees((byte[])args[0], (byte[][])args[1], (BigInteger)args[2]);
                }
                if (operation == "cancelStake")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 1) return false;
                    return CancelStake((byte[])args[0]);
                }

                // == Cancel / Withdraw ==
                if (GetState() == Pending)
                {
                    Runtime.Log("Contract not initialized!");
                    return false;
                }
                if (operation == "cancelOffer")
                {
                    if (args.Length != 1) return false;
                    return CancelOffer((byte[])args[0]);
                }
                if (operation == "withdrawAssets") // NEP-5 only
                {
                    if (args.Length != 3) return false;
                    if (VerifyWithdrawal((byte[])args[0], (byte[])args[1], (BigInteger)args[2]))
                    {
                        return WithdrawAssets((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                    }
                    else
                    {
                        Runtime.Log("Withdrawal is invalid!");
                        return false;
                    }
                }
                if (operation == "prepareAssetWithdrawal")
                {
                    if (args.Length != 1) return false;
                    return PrepareAssetWithdrawal((byte[])args[0]);
                }
                if (operation == "completeAssetWithdrawal") // SystemAsset only
                {
                    var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
                    if (!IsWithdrawingSystemAsset(currentTxn)) return false;

                    var outputs = currentTxn.GetOutputs();
                    foreach (var o in outputs)
                    {
                        if (o.ScriptHash != ExecutionEngine.ExecutingScriptHash)
                        {
                            Runtime.Log("Found a withdrawal..");
                            if (!ReduceBalance(o.ScriptHash, o.AssetId, o.Value)) return false;
                        }
                    }

                    var withdrawingAddr = GetWithdrawalAddress(currentTxn);
                    Storage.Delete(Context(), WithdrawalKey(withdrawingAddr));

                    return true;
                }

                // == Owner ==
                if (!Runtime.CheckWitness(Owner))
                {
                    Runtime.Log("Owner signature verification failed");
                    return false;
                }
                if (operation == "freezeTrading")
                {
                    Storage.Put(Context(), "state", Inactive);
                    return true;
                }
                if (operation == "unfreezeTrading")
                {
                    Storage.Put(Context(), "state", Active);
                    return true;
                }
                if (operation == "setFees")
                {
                    if (args.Length != 2) return false;
                    return SetFees((BigInteger)args[0], (BigInteger)args[1]);
                }
                if (operation == "setFeeAddress")
                {
                    if (args.Length != 1) return false;
                    return SetFeeAddress((byte[])args[0]);
                }
            }

            return true;
        }

        private static bool Initialize(BigInteger takerFee, BigInteger makerFee, byte[] feeAddress)
        {
            if (GetState() != Pending) return false;
            if (!SetFees(takerFee, makerFee)) return false;
            if (!SetFeeAddress(feeAddress)) return false;

            Storage.Put(Context(), "state", Active);
            Storage.Put(Context(), StakedTotalKey(CurrentBucket()), 0);

            Runtime.Log("Contract initialized");
            return true;
        }

        private static byte[] GetState()
        {
            return Storage.Get(Context(), "state");
        }

        private static BigInteger GetMakerFee()
        {
            return Storage.Get(Context(), "makerFee").AsBigInteger();
        }

        private static BigInteger GetTakerFee()
        {
            return Storage.Get(Context(), "takerFee").AsBigInteger();
        }

        private static BigInteger GetBalance(byte[] originator, byte[] assetID)
        {
            return Storage.Get(Context(), StoreKey(originator, assetID)).AsBigInteger();
        }

        private static BigInteger GetFeeBalance(byte[] assetID, BigInteger bucketNumber)
        {
            return Storage.Get(Context(), StoreKey(FeeAddressFor(bucketNumber), assetID)).AsBigInteger();
        }

        private static BigInteger GetTotalStaked(BigInteger bucketNumber)
        {
            var key = StakedTotalKey(bucketNumber);
            var total = Storage.Get(Context(), key);
            if (total.Length == 0)
            {
                var previousTotal = GetTotalStaked(bucketNumber - 1);
                Storage.Put(Context(), key, previousTotal);
                return previousTotal;
            }
            return total.AsBigInteger();
        }

        private static BigInteger[] GetExchangeRate(byte[] assetID) // against native token
        {
            var bucketNumber = CurrentBucket() - 1;
            var nativeVolume = Storage.Get(Context(), NativeVolumeKey(assetID, bucketNumber)).AsBigInteger();
            var otherVolume = Storage.Get(Context(), ForeignVolumeKey(assetID, bucketNumber)).AsBigInteger();

            return new BigInteger[] { otherVolume, nativeVolume };
        }

        private static byte[][] GetOffers(byte[] start, BigInteger count) // offerAssetID.Concat(wantAssetID)
        {
            var ptr = start;
            var result = new byte[50][]; // TODO: dynamic count doesn't work?
            var i = 0;
            while (ptr != Empty && ptr != Null)
            {
                result[i] = ptr.Concat(Storage.Get(Context(), ptr));
                ptr = GetOffer(ptr).PreviousOfferHash;
                i++;
            }

            return result;
        }

        private static bool MakeOffer(byte[] offerHash, Offer offer)
        {
            // Check that transaction is signed by the maker
            if (!Runtime.CheckWitness(offer.MakerAddress)) return false;

            // Check that nonce is not repeated
            if (Storage.Get(Context(), offerHash) != Empty) return false;

            // Check that the amounts > 0
            if (!(offer.OfferAmount > 0 && offer.WantAmount > 0)) return false;

            // Check the trade is across different assets
            if (offer.OfferAssetID == offer.WantAssetID) return false;

            // Check that asset IDs are valid
            if ((offer.OfferAssetID.Length != 20 && offer.OfferAssetID.Length != 32) ||
                (offer.WantAssetID.Length != 20 && offer.WantAssetID.Length != 32)) return false;

            // Reduce available balance for the offered asset and amount
            if (!ReduceBalance(offer.MakerAddress, offer.OfferAssetID, offer.OfferAmount)) return false;

            // Add the offer to storage
            AddOffer(offerHash, offer);

            // Notify clients
            Created(offerHash, offer.OfferAssetID, offer.OfferAmount, offer.WantAssetID, offer.WantAmount);
            return true;
        }

        private static bool FillOffer(byte[] fillerAddress, byte[] offerHash, BigInteger amountToFill, bool useNativeTokens)
        {
            // Check that transaction is signed by the filler
            if (!Runtime.CheckWitness(fillerAddress)) return false;

            // Check that the offer still exists 
            Offer offer = GetOffer(offerHash);
            if (offer.MakerAddress == Empty)
            {
                // Notify clients of failure
                Failed(fillerAddress, offerHash);
                return true;
            }

            // Check that the filler is different from the maker
            if (fillerAddress == offer.MakerAddress) return false;

            // Calculate max amount that can be offered & filled
            BigInteger amountToOffer = (offer.OfferAmount * amountToFill) / offer.WantAmount;
            if (amountToOffer > offer.AvailableAmount)
            {
                amountToOffer = offer.AvailableAmount;
                amountToFill = (amountToOffer * offer.WantAmount) / offer.OfferAmount;
            }

            // Check that the amount available is sufficient
            if (amountToOffer < 1 || amountToFill < 1)
            {
                // Notify clients of failure
                Failed(fillerAddress, offerHash);
                return true; // TODO: can we return false?
            }

            // Reduce available balance for the filled asset and amount
            if (!ReduceBalance(fillerAddress, offer.WantAssetID, amountToFill)) return false;

            // Calculate offered amount and fees
            BigInteger makerFeeRate = GetMakerFee();
            BigInteger takerFeeRate = GetTakerFee();
            BigInteger makerFee = (amountToFill * makerFeeRate) / feeFactor;
            BigInteger takerFee = (amountToOffer * takerFeeRate) / feeFactor;

            // Move fees
            var feeAddress = FeeAddressFor(CurrentBucket());
            TransferAssetTo(feeAddress, offer.WantAssetID, makerFee);
            TransferAssetTo(feeAddress, offer.OfferAssetID, takerFee);

            // Move asset to the maker balance and notify clients
            TransferAssetTo(offer.MakerAddress, offer.WantAssetID, amountToFill - makerFee);
            Transferred(offer.MakerAddress, offer.WantAssetID, amountToFill - makerFee);

            // Move asset to the taker balance and notify clients
            if (useNativeTokens)
            {
                // Use previous trading period's exchange rate
                var bucketNumber = CurrentBucket() - 1;

                // Derive rate from volumes traded
                var nativeVolume = Storage.Get(Context(), NativeVolumeKey(offer.OfferAssetID, bucketNumber)).AsBigInteger();
                var otherVolume = Storage.Get(Context(), ForeignVolumeKey(offer.OfferAssetID, bucketNumber)).AsBigInteger();
                BigInteger nativeFee = 0;
                if (otherVolume > 0)
                {
                    // use native fee, if we can get an exchange rate
                    nativeFee = (takerFee * nativeVolume) / (otherVolume * nativeTokenDiscount);
                }
                ReduceBalance(fillerAddress, NativeToken, nativeFee);
                TransferAssetTo(fillerAddress, offer.OfferAssetID, amountToOffer);
                Transferred(fillerAddress, offer.OfferAssetID, amountToOffer);
            }
            else
            {
                TransferAssetTo(fillerAddress, offer.OfferAssetID, amountToOffer - takerFee);
                Transferred(fillerAddress, offer.OfferAssetID, amountToOffer - takerFee);
            }

            // Update native token exchange rate
            if (offer.OfferAssetID == NativeToken)
            {
                // Adding volume to the current trading period
                var bucketNumber = CurrentBucket();

                // Increase native token total by amountToOffer
                var nativeKey = NativeVolumeKey(offer.WantAssetID, bucketNumber);
                var nativeVolume = Storage.Get(Context(), nativeKey).AsBigInteger();
                Storage.Put(Context(), nativeKey, nativeVolume + amountToOffer);

                // Increase other token total by amountToFill                
                var otherKey = ForeignVolumeKey(offer.WantAssetID, bucketNumber);
                var otherVolume = Storage.Get(Context(), otherKey).AsBigInteger();
                Storage.Put(Context(), nativeKey, otherVolume + amountToFill);
            }
            if (offer.WantAssetID == NativeToken)
            {
                // Adding volume to the current trading period
                var bucketNumber = CurrentBucket();

                // Increase native token total by amountToOffer
                var nativeKey = NativeVolumeKey(offer.OfferAssetID, bucketNumber);
                var nativeVolume = Storage.Get(Context(), nativeKey).AsBigInteger();
                Storage.Put(Context(), nativeKey, nativeVolume + amountToFill);

                // Increase other token total by amountToFill                
                var otherKey = ForeignVolumeKey(offer.OfferAssetID, bucketNumber);
                var otherVolume = Storage.Get(Context(), otherKey).AsBigInteger();
                Storage.Put(Context(), nativeKey, otherVolume + amountToOffer);
            }

            // Update available amount
            offer.AvailableAmount = offer.AvailableAmount - amountToOffer;

            // Store updated offer
            StoreOffer(offerHash, offer);

            // Notify clients
            Filled(fillerAddress, offerHash, amountToFill, offer.OfferAssetID, offer.OfferAmount, offer.WantAssetID, offer.WantAmount);
            return true;
        }

        private static bool CancelOffer(byte[] offerHash)
        {
            // Check that the offer exists
            Offer offer = GetOffer(offerHash);
            if (offer.MakerAddress == Empty) return false;

            // Check that transaction is signed by the canceller
            if (!Runtime.CheckWitness(offer.MakerAddress)) return false;

            // Move funds to withdrawal address
            TransferAssetTo(offer.MakerAddress, offer.OfferAssetID, offer.AvailableAmount);

            // Remove offer
            RemoveOffer(offerHash, offer);

            // Notify runtime
            Cancelled(offerHash);
            return true;
        }

        private static bool VerifyWithdrawal(byte[] holderAddress, byte[] assetID, BigInteger amount)
        {
            // Check that there are asset value > 0 in balance
            var balance = GetBalance(holderAddress, assetID);
            if (balance < amount) return false;

            return true;
        }

        private static bool WithdrawAssets(byte[] holderAddress, byte[] assetID, BigInteger amount)
        {
            // Transfer token
            var args = new object[] { ExecutionEngine.ExecutingScriptHash, holderAddress, amount };
            var contract = (NEP5Contract)assetID.ToDelegate();
            bool transferSuccessful = (bool)contract("transfer", args);
            if (!transferSuccessful)
            {
                Runtime.Log("Failed to transfer NEP-5 tokens!");
                return false;
            }

            // Reduce balance in storage
            if (!ReduceBalance(holderAddress, assetID, amount)) return false;

            // Notify clients
            Withdrawn(holderAddress, assetID, amount);

            return true;
        }

        private static bool PrepareAssetWithdrawal(byte[] holderAddress)
        {
            // Check that transaction is signed by the asset holder
            if (!Runtime.CheckWitness(holderAddress)) return false;

            // Get the key which marks start of withdrawal process
            var withdrawalKey = WithdrawalKey(holderAddress);

            // Check if already withdrawing
            if (Storage.Get(Context(), withdrawalKey) != Empty) return false;

            // Set blockheight from which to check for double withdrawals later on
            Storage.Put(Context(), withdrawalKey, Blockchain.GetHeight());

            Runtime.Log("Prepared for asset withdrawal");

            return true;
        }

        private static bool StakeTokens(byte[] stakerAddress, BigInteger amount)
        {
            // Check that transaction is signed by the staker
            if (!Runtime.CheckWitness(stakerAddress)) return false;

            // Stake tokens from contract balance
            if (!ReduceBalance(stakerAddress, NativeToken, amount)) return false;

            // Get the next available bucket for staking
            BigInteger bucketNumber = CurrentBucket();

            // Get staking keys
            var stakedAmountKey = StakedAmountKey(stakerAddress);
            var stakedTimeKey = StakedTimeKey(stakerAddress);

            // Get staking values
            var stakedAmount = Storage.Get(Context(), stakedAmountKey).AsBigInteger();
            var stakedTime = Storage.Get(Context(), stakedTimeKey).AsBigInteger();
            var stakedTotal = GetTotalStaked(bucketNumber);

            // Don't allow re-staking - must claim and cancel first
            if (stakedAmount > 0 || stakedTime > 0) return false;

            // Update individual staked amount
            Storage.Put(Context(), stakedAmountKey, amount);

            // Update start time of staking
            Storage.Put(Context(), stakedTimeKey, bucketNumber);

            // Update total amount staked in the next bucket
            Storage.Put(Context(), StakedTotalKey(bucketNumber), stakedTotal + amount);

            return true;
        }

        private static bool ClaimFees(byte[] claimerAddress, byte[][] assetIDs, BigInteger bucketNumber)
        {
            // Get staking details
            var stakedAmount = Storage.Get(Context(), StakedAmountKey(claimerAddress)).AsBigInteger();
            var stakedTime = Storage.Get(Context(), StakedTimeKey(claimerAddress)).AsBigInteger();
            var stakedTotal = GetTotalStaked(bucketNumber);

            // Check that the claim is valid
            BigInteger currentBucketNumber = CurrentBucket();
            if (stakedAmount <= 0 || stakedTime < bucketNumber || bucketNumber >= currentBucketNumber) return false;

            // Move fees from fee addr to claimer addr
            foreach (byte[] assetID in assetIDs)
            {
                var feeAddress = FeeAddressFor(bucketNumber);
                var feesKey = StoreKey(feeAddress, assetID);
                var totalFees = Storage.Get(Context(), feesKey).AsBigInteger();
                var claimableAmount = totalFees * stakedAmount / stakedTotal;
                if (claimableAmount > 0)
                {
                    TransferAssetTo(claimerAddress, assetID, claimableAmount);
                    if (!ReduceBalance(feeAddress, assetID, claimableAmount)) return false;
                }
            }

            // Update staked time
            Storage.Put(Context(), StakedTimeKey(claimerAddress), bucketNumber + 1);

            return true;
        }

        private static bool CancelStake(byte[] stakerAddress)
        {
            // Get the next available bucket for staking
            BigInteger bucketNumber = CurrentBucket();

            // Save staked amount and then remove it
            var stakedAmountKey = StakedAmountKey(stakerAddress);
            var stakedAmount = Storage.Get(Context(), stakedAmountKey).AsBigInteger();
            Storage.Delete(Context(), stakedAmountKey);

            // Clean up associated timing
            var stakedTimeKey = StakedTime.Concat(stakerAddress);
            Storage.Delete(Context(), stakedTimeKey);

            // Reduce total staked
            var stakedTotalKey = StakedTotal.Concat(bucketNumber.AsByteArray());
            var stakedTotal = Storage.Get(Context(), stakedTotalKey).AsBigInteger();
            Storage.Put(Context(), stakedTotalKey, stakedTotal - stakedAmount);

            // Allow withdrawing of previously staked asset
            TransferAssetTo(stakerAddress, NativeToken, stakedAmount);

            return true;
        }

        private static bool SetFees(BigInteger takerFee, BigInteger makerFee)
        {
            if (takerFee > maxFee || makerFee > maxFee) return false;
            if (takerFee < 0 || makerFee < 0) return false;

            Storage.Put(Context(), "takerFee", takerFee);
            Storage.Put(Context(), "makerFee", makerFee);

            return true;
        }

        private static bool SetFeeAddress(byte[] feeAddress)
        {
            if (feeAddress.Length != 20) return false;
            Storage.Put(Context(), "feeAddress", feeAddress);

            return true;
        }

        private static bool VerifySentAmount(byte[] originator, byte[] assetID, BigInteger amount)
        {
            // Verify that the offer really has the indicated assets available
            if (assetID.Length == 32)
            {
                // Check the current transaction for the system assets
                var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
                var outputs = currentTxn.GetOutputs();
                ulong sentAmount = 0;
                foreach (var o in outputs)
                {
                    if (o.AssetId == assetID && o.ScriptHash == ExecutionEngine.ExecutingScriptHash)
                    {
                        sentAmount += (ulong)o.Value;
                    }
                }

                // Get previously consumed amount wihtin same transaction
                var consumedAmount = Storage.Get(Context(), currentTxn.Hash.Concat(assetID)).AsBigInteger();

                // Check that the sent amount is still sufficient
                if (sentAmount - consumedAmount < amount)
                {
                    Runtime.Log("Not enough of asset sent");
                    return false;
                }

                // Update the consumed amount for this txn
                Storage.Put(Context(), currentTxn.Hash.Concat(assetID), consumedAmount + amount);

                // TODO: how to cleanup?
                return true;
            }
            else if (assetID.Length == 20)
            {
                // Just transfer immediately or fail as this is the last step in verification
                var args = new object[] { originator, ExecutionEngine.ExecutingScriptHash, amount };
                var Contract = (NEP5Contract)assetID.ToDelegate();
                var transferSuccessful = (bool)Contract("transfer", args);
                return transferSuccessful;
            }

            // Unknown asset category
            return false;
        }

        private static Offer GetOffer(byte[] hash)
        {
            // Check that offer exists
            var offerData = Storage.Get(Context(), hash);
            if (offerData == Empty) return new Offer(); // invalid offer hash

            // Deserialize offer
            var index = 0;

            var makerAddress = offerData.Range(index, 20);
            index += 20;

            var offerAssetIDLength = 20;
            var wantAssetIDLength = 20;
            var typeLength = 2;
            var intLength = 8;
            var orderHashLength = 32;

            if (offerData.Range(index, typeLength) == SystemAsset) offerAssetIDLength = 32;
            index += typeLength;

            if (offerData.Range(index, typeLength) == SystemAsset) wantAssetIDLength = 32;
            index += typeLength;

            var offerAssetID = offerData.Range(index, offerAssetIDLength);
            index += offerAssetIDLength;

            var wantAssetID = offerData.Range(index, wantAssetIDLength);
            index += wantAssetIDLength;

            var offerAmount = offerData.Range(index, intLength);
            index += intLength;

            var wantAmount = offerData.Range(index, intLength);
            index += intLength;

            var availableAmount = offerData.Range(index, intLength);
            index += intLength;

            var previousOfferHash = offerData.Range(index, orderHashLength);
            index += orderHashLength;

            var nextOfferHash = offerData.Range(index, orderHashLength);
            index += orderHashLength;

            return NewOffer(makerAddress, offerAssetID, offerAmount, wantAssetID, wantAmount, availableAmount, previousOfferHash, nextOfferHash);
        }

        private static void StoreOffer(byte[] offerHash, Offer offer)
        {
            // Remove offer if completely filled
            if (offer.AvailableAmount == 0)
            {
                RemoveOffer(offerHash, offer);
            }
            // Store offer otherwise
            else
            {
                // Serialize offer
                // TODO: we can save storage space by not storing assetCategory / IDs and force clients to walk the list
                var offerData = offer.MakerAddress
                                     .Concat(offer.OfferAssetCategory)
                                     .Concat(offer.WantAssetCategory)
                                     .Concat(offer.OfferAssetID)
                                     .Concat(offer.WantAssetID)
                                     .Concat(offer.OfferAmount.AsByteArray().Concat(Zeroes).Take(8))
                                     .Concat(offer.WantAmount.AsByteArray().Concat(Zeroes).Take(8))
                                     .Concat(offer.AvailableAmount.AsByteArray().Concat(Zeroes).Take(8))
                                     .Concat(offer.PreviousOfferHash)
                                     .Concat(offer.NextOfferHash);

                Storage.Put(Context(), offerHash, offerData);
            }
        }

        private static void AddOffer(byte[] offerHash, Offer offer)
        {
            var tradingPair = TradingPair(offer);
            var previousOfferHash = Storage.Get(Context(), tradingPair);

            // Add edges to the previous HEAD of the linked list for this trading pair
            if (previousOfferHash != Null)
            {
                offer.PreviousOfferHash = previousOfferHash;
                var previousOffer = GetOffer(previousOfferHash);
                previousOffer.NextOfferHash = offerHash;
                StoreOffer(previousOfferHash, previousOffer);
            }

            // Set the HEAD of the linked list for this trading pair as this offer
            Storage.Put(Context(), tradingPair, offerHash);

            // Store the new offer
            StoreOffer(offerHash, offer);
        }

        private static void RemoveOffer(byte[] offerHash, Offer offer)
        {
            // Get the first item (head) in order book
            var tradingPair = TradingPair(offer);
            byte[] head = Storage.Get(Context(), tradingPair);

            // Check if the offer is at the HEAD of the linked list
            if (head == offerHash)
            {
                // There are more offers in this list so set the new HEAD of the linked list to the previous offer
                if (offer.PreviousOfferHash != Null)
                {
                    Storage.Put(Context(), tradingPair, offer.PreviousOfferHash);
                }
                // Otherwise, just remove the whole list since this is the only offer left
                else
                {
                    Storage.Delete(Context(), tradingPair);
                }
            }

            // Combine nodes with an bi-directional edge
            if (offer.NextOfferHash != Null)
            {
                var nextOffer = GetOffer(offer.NextOfferHash);
                nextOffer.PreviousOfferHash = offer.PreviousOfferHash;
                StoreOffer(offer.NextOfferHash, nextOffer);
            }
            if (offer.PreviousOfferHash != Null)
            {
                var previousOffer = GetOffer(offer.PreviousOfferHash);
                previousOffer.NextOfferHash = offer.NextOfferHash;
                StoreOffer(offer.PreviousOfferHash, previousOffer);
            }

            // Delete offer data
            Storage.Delete(Context(), offerHash);
        }

        private static ulong GetAmountForAssetInOutputs(byte[] assetID, TransactionOutput[] outputs)
        {
            ulong amount = 0;
            foreach (var o in outputs)
            {
                if (o.AssetId == assetID && o.ScriptHash != ExecutionEngine.ExecutingScriptHash) amount += (ulong)o.Value;
            }

            return amount;
        }

        private static byte[] GetWithdrawalAddress(Transaction transaction)
        {
            var txnAttributes = transaction.GetAttributes();
            foreach (var attr in txnAttributes)
            {
                // This is the additional verification script which can be used
                // to ensure any withdrawal txns are intended by the owner.
                if (attr.Usage == 0x20) return attr.Data.Take(20);
            }
            return Empty;
        }

        private static bool IsWithdrawingSystemAsset(Transaction transaction)
        {
            // Check that transaction is an Invocation transaction
            if (transaction.Type != 0xd1) return false;

            // Check that the transaction is marked as a SystemAsset withdrawal
            var txnAttributes = transaction.GetAttributes();
            foreach (var attr in txnAttributes)
            {
                if (attr.Usage == 0xa1 && attr.Data.Take(20) == ExecutionEngine.ExecutingScriptHash) return true;
            }

            return false;
        }

        private static void TransferAssetTo(byte[] originator, byte[] assetID, BigInteger amount)
        {
            if (amount < 1)
            {
                Runtime.Log("Amount to transfer is less than 1!");
                return;
            }

            byte[] key = StoreKey(originator, assetID);
            BigInteger currentBalance = Storage.Get(Context(), key).AsBigInteger();
            Storage.Put(Context(), key, currentBalance + amount);
        }

        private static bool ReduceBalance(byte[] address, byte[] assetID, BigInteger amount)
        {
            if (amount < 1)
            {
                Runtime.Log("Amount to reduce is less than 1!");
                return false;
            }

            var key = StoreKey(address, assetID);
            var currentBalance = Storage.Get(Context(), key).AsBigInteger();
            var newBalance = currentBalance - amount;

            if (newBalance < 0)
            {
                Runtime.Log("Not enough balance!");
                return false;
            }

            if (newBalance > 0) Storage.Put(Context(), key, newBalance);
            else Storage.Delete(Context(), key);

            return true;
        }

        private static byte[] StoreKey(byte[] originator, byte[] assetID) => originator.Concat(assetID);
        private static byte[] WithdrawalKey(byte[] originator) => originator.Concat(Withdrawing);
        private static byte[] ForeignVolumeKey(byte[] assetID, BigInteger bucketNumber) => assetID.Concat(Foreign).Concat(bucketNumber.AsByteArray());
        private static byte[] NativeVolumeKey(byte[] assetID, BigInteger bucketNumber) => assetID.Concat(Native).Concat(bucketNumber.AsByteArray());
        private static byte[] StakedAmountKey(byte[] staker) => StakedAmount.Concat(staker);
        private static byte[] StakedTimeKey(byte[] staker) => StakedTime.Concat(staker);
        private static byte[] StakedTotalKey(BigInteger bucketNumber) => StakedTotal.Concat(bucketNumber.AsByteArray());
        private static byte[] FeeAddressFor(BigInteger bucketNumber) => FeesAccumulated.Concat(bucketNumber.AsByteArray());
        private static byte[] TradingPair(Offer o) => o.OfferAssetID.Concat(o.WantAssetID);

        private static byte[] Hash(Offer o, byte[] nonce)
        {
            var bytes = o.MakerAddress
                .Concat(TradingPair(o))
                .Concat(o.OfferAmount.AsByteArray())
                .Concat(o.WantAmount.AsByteArray())
                .Concat(nonce);

            return Hash256(bytes);
        }

        private static BigInteger AmountToOffer(Offer o, BigInteger amount)
        {
            return (o.OfferAmount * amount) / o.WantAmount;
        }
    }
}