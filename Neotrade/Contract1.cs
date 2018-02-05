using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class NeoTrade : Framework.SmartContract
    {
        private static readonly byte[] AdminAccount = { 230, 232, 140, 74, 123, 18, 79, 162, 107, 202, 137, 132, 9, 79, 65, 209, 113, 134, 111, 230 };
        private static readonly byte[] NEO = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private static readonly byte[] GAS = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

        // Setup a NEP5 token that can be traded with no fee charged
        public static string Name() => "NeoTrade Coin";
        public static string Symbol() => "NTC";
        public static byte Decimals() => 8;

        private const ulong factor = 100000000;
        private const ulong totalAmountNTC = 50000000 * factor;                                  // total supply of NTC tokens
        private const ulong icoBaseExchangeRate = 1000 * factor;                                  // number of tokens to trade per NEO during ico
        private const int icoDuration = 30;                                                       // number of days to run ico
        private const int icoDurationSeconds = icoDuration * 86400;
        private const int icoStartTimestamp = 1510753945;
        private const int icoEndTimestamp = icoStartTimestamp + icoDurationSeconds;

        // transaction fee charged on each order confirmation (not implemented in this version)
        private const float transactionFee = 0.25F;


        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> Refund;

        [DisplayName("refundAsset")]
        public static event Action<byte[], byte[], BigInteger> RefundAsset;

        // byte[]sender, byte[] recipient, byte[] currency, biginteger amount
        [DisplayName("transferAsset")]
        public static event Action<byte[], byte[], byte[], BigInteger> TransferAsset;

        // byte[]sender, string orderType, byte[] orderAddress, byte[] fromCurrency, biginteger fromAmount, byte[] toCurrency, biginteger toAmount
        [DisplayName("createOrder")]
        public static event Action<byte[], string, byte[], byte[], BigInteger, byte[], BigInteger> CreateOrderLog;

        // byte[] orderAddress
        [DisplayName("cancelOrder")]
        public static event Action<byte[]> CancelOrderLog;

        //[DisplayName("faucet")]
        //public static event Action<byte[]> Faucet;


        public static Object Main(string operation, params object[] args)
        {
            Runtime.Notify("Main() operation", operation);

            if (Runtime.Trigger == TriggerType.Application)
            {
                // contract has received an InvocationTransaction
                Runtime.Notify("Main() Runtime.Application operation", operation);

                /*** BEGIN NEOTRADE METHODS ***/
                if (operation == "setTXHelper")
                {
                    // admin function to set the txHelper scriptHash
                    if (!RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return SetTXHelper((byte[])args[0]);
                }

                if (operation == "balanceOfCurrency")
                {
                    // check a users current balance of a specific currency
                    if (!RequireArgumentLength(args, 2))
                    {
                        return false;
                    }
                    return GetBalanceOfCurrency((byte[])args[0], (byte[])args[1]);
                }

                if (operation == "setBalanceOfCurrency")
                {
                    // protected method used to set balance for NEP5 transfers
                    if (!RequireArgumentLength(args, 5))
                    {
                        return false;
                    }
                    return SetBalanceOfNEP5Currency((byte[])args[0], (string)args[1], (BigInteger)args[2]);
                }

                if (operation == "withdrawCurrency")
                {
                    // user is requesting funds to be withdrawn from contract
                    if (!RequireArgumentLength(args, 3))
                    {
                        return false;
                    }

                    return WithdrawCurrency((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                }

                if (operation == "depositAsset")
                {
                    // neo or gas is being deposited via invocation
                    return DepositAsset(args);
                }

                if (operation == "createOrder")
                {
                    // user is creating a buy or sell order
                    Runtime.Notify("createOrder() Args length", args.Length); // expect 7
                    return CreateOrder(args);
                }

                if (operation == "performTrade")
                {
                    Runtime.Notify("PerformTrade() Args length", args.Length); // expect 2
                    return PerformTrade(args);
                }

                if (operation == "cancelOrder")
                {
                    Runtime.Notify("cancelOrder() Args length", args.Length); // expect 2
                    return CancelOrder(args);
                }

                /*** END NEOTRADE METHODS ***/

                /*** BEGIN NEP5 SUPPORT METHODS ***/
                if (operation == "balanceOf")
                {
                    // check the balance of an account
                    if (!RequireArgumentLength(args, 1))
                    {
                        return 0;
                    }
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "transfer")
                {
                    // transfer NEP5 token to another account
                    if (!RequireArgumentLength(args, 3))
                    {
                        for (int i = 0; i < args.Length; i++)
                        {
                            Runtime.Notify("Main() transfer arg", args[i]);
                        }
                        return false;
                    }
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }

                switch (operation)
                {
                    case "decimals":
                        return Decimals();
                    case "mintTokens":
                        return MintTokens();
                    case "name":
                        return Name();
                    case "symbol":
                        return Symbol();
                    case "totalSupply":
                        return TotalSupply();
                    case "initSC":
                        // not really a nep5 method - placing here is as good as anywhere
                        return InitSC();
                }
                /*** END NEP5 SUPPORT METHODS ***/

                Runtime.Notify("finishing TriggerType.Application block (unhandled method)", operation);
            }
            else if (Runtime.Trigger == TriggerType.Verification)
            {
                // contract has received a ContractTransaction
                Runtime.Notify("Main() TriggerType.Verification", TriggerType.Verification);

                // todo: implement balance checking of NEO/GAS
                return VerifyOwnerAccount();
            }

            return false;
        }

        /**
         * retrieve an indexName comprised of address+currency
         */
        public static byte[] GetCurrencyIndexName(byte[] address, byte[] currency)
        {
            return address.Concat(currency);
        }

        /**
         * run the trade the user has requested
         */
        public static bool PerformTrade(object[] args)
        {
            byte[] sender = (byte[])args[0];                    // order owner address
            byte[] orderAddress = (byte[])args[1];              // what is the order address

            if (!VerifyWitness(sender))
            {
                Runtime.Notify("PerformTrade() VerifyWitness failed", sender);
                return false;
            }

            object[] orderDetails = GetOrderDetails(orderAddress);
            byte[] orderOwner = (byte[])orderDetails[1];
            byte[] orderFromCurrency = (byte[])orderDetails[3];
            BigInteger orderFromAmount = (BigInteger)orderDetails[0];
            byte[] orderToCurrency = (byte[])orderDetails[4];
            BigInteger orderToAmount = (BigInteger)orderDetails[5];

            BigInteger remainingBalance = (BigInteger)orderDetails[0];
            BigInteger purchaserBalance = GetBalanceOfCurrency(sender, orderToCurrency);

            if (remainingBalance > 0 && purchaserBalance >= orderToAmount)
            {
                CurrencyWithdraw(sender, orderToCurrency, orderToAmount);       // take funds from purchasers balance
                CurrencyDeposit(orderOwner, orderToCurrency, orderToAmount);    // give funds to order owners balance

                CurrencyDeposit(sender, orderFromCurrency, orderFromAmount);

                RemoveOrderStorage(orderAddress);
                return true;
            }

            return false;
        }

        /**
         * owner has requested to cancel their order. refund their unfilled funds
         * <param name="args">0=sender,1=orderAddress</param>
         */
        public static bool CancelOrder(object[] args)
        {
            byte[] sender = (byte[])args[0];                    // order owner address
            byte[] orderAddress = (byte[])args[1];              // what is the order address

            if (!VerifyWitness(sender))
            {
                Runtime.Notify("CancelOrder() VerifyWitness failed", sender);
                return false;
            }

            object[] orderDetails = GetOrderDetails(orderAddress);
            byte[] orderOwner = (byte[])orderDetails[1];

            // save the address of the order creator so it can be cancelled
            if (orderOwner != sender)
            {
                // sender is not the owner, don't do anything
                Runtime.Notify("CancelOrder() orderOwner != sender", sender, orderOwner);
                return false;
            }

            BigInteger remainingBalance = (BigInteger)orderDetails[0];
            if (remainingBalance > 0)
            {
                // order has not been filled, refund held balance to owner
                Runtime.Notify("CancelOrder() attempting to release held funds", remainingBalance);
                ReleaseCurrencyFromOrder(sender, (byte[])orderDetails[3], remainingBalance);
            }

            // cleanup order data
            RemoveOrderStorage(orderAddress);
            return true;
        }

        /**
         * helper method to remove order details from contract storage
         */
        public static void RemoveOrderStorage(byte[] orderAddress)
        {
            byte[] orderTypeAddress = orderAddress.Concat("orderTypeAddress".AsByteArray());
            byte[] orderOwnerAddress = orderAddress.Concat("orderOwnerAddress".AsByteArray());              // who owns the order
            byte[] orderFromCurrencyAddress = orderAddress.Concat("orderFromCurrency".AsByteArray());       // what they are trading
            byte[] orderToCurrencyAddress = orderAddress.Concat("orderToCurrency".AsByteArray());           // what they are wanting
            byte[] orderToCurrencyAmount = orderAddress.Concat("orderToCurrencyAmount".AsByteArray());      // how much they are wanting
            Storage.Delete(Storage.CurrentContext, orderTypeAddress);
            Storage.Delete(Storage.CurrentContext, orderOwnerAddress);
            Storage.Delete(Storage.CurrentContext, orderFromCurrencyAddress);
            Storage.Delete(Storage.CurrentContext, orderToCurrencyAddress);
            Storage.Delete(Storage.CurrentContext, orderToCurrencyAmount);
            Storage.Delete(Storage.CurrentContext, orderAddress);

            // broadcast the cancellation to the network
            CancelOrderLog(orderAddress);
        }

        /**
         * helper method to load details relevant to an order
         * <returns>
         * object[0] = BigInteger amountUnfilled
         * object[1] = byte[] orderOwnerAddress
         * object[2] = string orderType BUY|SELL
         * object[3] = byte[] orderFromCurrency
         * object[4] = byte[] orderToCurrency
         * object[5] = BigInteger exchange rate
         * </returns>
         */
        public static object[] GetOrderDetails(byte[] orderAddress)
        {
            byte[] orderTypeAddress = orderAddress.Concat("orderTypeAddress".AsByteArray());
            byte[] orderOwnerAddress = orderAddress.Concat("orderOwnerAddress".AsByteArray());              // who owns the order
            byte[] orderFromCurrencyAddress = orderAddress.Concat("orderFromCurrency".AsByteArray());       // what they are trading
            byte[] orderToCurrencyAddress = orderAddress.Concat("orderToCurrency".AsByteArray());           // what they are wanting
            byte[] orderToCurrencyAmount = orderAddress.Concat("orderToCurrencyAmount".AsByteArray());      // how much they are wanting

            object[] orderDetails = new object[6];

            orderDetails[0] = Storage.Get(Storage.CurrentContext, orderAddress).AsBigInteger();                            // how much they are trading
            orderDetails[1] = Storage.Get(Storage.CurrentContext, orderOwnerAddress);
            orderDetails[2] = Storage.Get(Storage.CurrentContext, orderTypeAddress).AsString();
            orderDetails[3] = Storage.Get(Storage.CurrentContext, orderFromCurrencyAddress);
            orderDetails[4] = Storage.Get(Storage.CurrentContext, orderToCurrencyAddress);
            orderDetails[5] = Storage.Get(Storage.CurrentContext, orderToCurrencyAmount).AsBigInteger();
            return orderDetails;
        }

        public static bool CreateOrder(object[] args)
        {
            byte[] sender = (byte[])args[0];                    // who has sent this create order request
            byte[] fromCurrency = (byte[])args[1];              // what currency are they trading
            BigInteger fromAmount = (BigInteger)args[2];        // what amount of currency
            byte[] toCurrency = (byte[])args[3];                // what currency do they want in exchange
            BigInteger toAmount = (BigInteger)args[4];          // what amount of currency do they want 
            string orderType = (string)args[5];                 // is this a buy or sell order
            byte[] orderKey = (byte[])args[6];                  // random id for this order

            if (!VerifyWitness(sender))
            {
                Runtime.Notify("CreateOrder() VerifyWitness failed", sender);
                return false;
            }

            BigInteger fromBalance = GetBalanceOfCurrency(sender, fromCurrency);
            if (fromBalance < fromAmount)
            {
                // user doesn't have enough funds to create this order
                Runtime.Notify("CreateOrder() Insufficient funds: fromBalance", fromBalance);
                Runtime.Notify("CreateOrder() Insufficient funds: fromBalance", fromCurrency);
                return false;
            }

            byte[] orderAddress = sender.Concat(fromCurrency).Concat(toCurrency).Concat(orderKey);

            Runtime.Notify("CreateOrder() sender", sender);
            Runtime.Notify("CreateOrder() fromCurrency", fromCurrency);
            Runtime.Notify("CreateOrder() fromAmount", fromAmount);
            Runtime.Notify("CreateOrder() toCurrency", toCurrency);
            Runtime.Notify("CreateOrder() toAmount", toAmount);
            Runtime.Notify("CreateOrder() orderType", orderType);
            Runtime.Notify("CreateOrder() orderKey", orderKey);
            Runtime.Notify("CreateOrder() orderAddress", orderAddress);

            if (orderType == "SELL")
            {
                Runtime.Notify("CreateOrder() Ordertype was SELL", orderType);
            }
            else if (orderType == "BUY")
            {
                Runtime.Notify("CreateOrder() Ordertype was BUY", orderType);
            }
            else
            {
                Runtime.Notify("CreateOrder() Invalid transaction type (must be BUY|SELL)", orderType);
                return false;
            }

            // CreateOrder byte[]sender, string orderType, byte[] orderAddress, byte[] fromCurrency, biginteger fromAmount, byte[] toCurrency, biginteger toAmount
            if (HoldCurrencyForOrder(sender, fromCurrency, fromAmount))
            {
                Runtime.Notify("CreateOrder() order was created", orderAddress);
                byte[] orderTypeAddress = orderAddress.Concat("orderTypeAddress".AsByteArray());
                byte[] orderOwnerAddress = orderAddress.Concat("orderOwnerAddress".AsByteArray());
                byte[] orderFromCurrencyAddress = orderAddress.Concat("orderFromCurrency".AsByteArray());
                byte[] orderToCurrencyAddress = orderAddress.Concat("orderToCurrency".AsByteArray());
                byte[] orderToCurrencyAmount = orderAddress.Concat("orderToCurrencyAmount".AsByteArray());

                // save the address of the order creator so it can be cancelled
                Storage.Put(Storage.CurrentContext, orderTypeAddress, orderType);
                Storage.Put(Storage.CurrentContext, orderOwnerAddress, sender);
                Storage.Put(Storage.CurrentContext, orderFromCurrencyAddress, fromCurrency);
                Storage.Put(Storage.CurrentContext, orderToCurrencyAddress, toCurrency);
                Storage.Put(Storage.CurrentContext, orderToCurrencyAmount, toAmount);
                Storage.Put(Storage.CurrentContext, orderAddress, fromAmount);

                CreateOrderLog(sender, orderType, orderAddress, fromCurrency, fromAmount, toCurrency, toAmount);
                return true;
            }

            return false;
        }

        /**
         * attempt to deposit assets to the senders account balance
         * <param name="args"></param>
         */
        public static bool DepositAsset(object[] args)
        {
            Runtime.Notify("DepositAsset() args.Length", args.Length);

            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput reference = tx.GetReferences()[0];

            if (reference.AssetId != NEO && reference.AssetId != GAS)
            {
                // transferred asset is not neo or gas, do nothing
                Runtime.Notify("DepositAsset() reference.AssetID is not NEO|GAS", reference.AssetId);
                return false;
            }

            TransactionOutput[] outputs = tx.GetOutputs();
            byte[] sender = reference.ScriptHash;                                   // the sender of funds, balance will be credited here
            byte[] receiver = ExecutionEngine.ExecutingScriptHash;                  // scriptHash of SC
            ulong receivedNEO = 0;
            ulong receivedGAS = 0;

            Runtime.Notify("DepositAsset() recipient of funds", ExecutionEngine.ExecutingScriptHash);

            // Calculate the total amount of NEO|GAS transferred to the smart contract address
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == receiver)
                {
                    // only add funds to total received value if receiver is the recipient of the output
                    ulong receivedValue = (ulong)output.Value;
                    Runtime.Notify("DepositAsset() Received Deposit type", reference.AssetId);
                    if (reference.AssetId == NEO)
                    {
                        Runtime.Notify("DepositAsset() adding NEO to total", receivedValue);
                        receivedNEO += receivedValue;
                    }
                    else if (reference.AssetId == GAS)
                    {
                        Runtime.Notify("DepositAsset() adding GAS to total", receivedValue);
                        receivedGAS += receivedValue;
                    }
                }
            }

            Runtime.Notify("DepositAsset() receivedNEO", receivedNEO);
            Runtime.Notify("DepositAsset() receivedGAS", receivedGAS);

            if (receivedNEO > 0)
            {
                CurrencyDeposit(sender, NEO, receivedNEO);
                TransferAsset(null, sender, NEO, receivedNEO);
            }

            if (receivedGAS > 0)
            {
                CurrencyDeposit(sender, GAS, receivedGAS);
                TransferAsset(null, sender, GAS, receivedGAS);
            }

            return true;
        }

        /**
         * funds are released back to the owner of an order when it is cancelled
         */
        public static bool ReleaseCurrencyFromOrder(byte[] destinationAddress, byte[] currency, BigInteger releaseAmount)
        {
            Runtime.Notify("ReleaseCurrencyFromOrder() destinationAddress", destinationAddress);
            Runtime.Notify("ReleaseCurrencyFromOrder() currency", currency);
            Runtime.Notify("ReleaseCurrencyFromOrder() releaseAmount", releaseAmount);

            CurrencyDeposit(destinationAddress, currency, releaseAmount);
            return true;
        }

        /**
         * when an order is created, the currency will be taken from the user until order is filled or cancelled
         */
        public static bool HoldCurrencyForOrder(byte[] destinationAddress, byte[] currency, BigInteger withdrawAmount)
        {
            if (!Runtime.CheckWitness(destinationAddress))
            {
                // ensure transaction is signed properly
                Runtime.Notify("HoldCurrencyForOrder() CheckWitness failed", destinationAddress);
                return false;
            }

            Runtime.Notify("HoldCurrencyForOrder() destinationAddress", destinationAddress);
            Runtime.Notify("HoldCurrencyForOrder() currency", currency);
            Runtime.Notify("HoldCurrencyForOrder() withdrawAmount", withdrawAmount);

            BigInteger currentBalance = GetBalanceOfCurrency(destinationAddress, currency);

            if (currentBalance <= 0 || currentBalance < withdrawAmount)
            {
                Runtime.Notify("HoldCurrencyForOrder() insufficient funds", currentBalance);
                return false;
            }

            CurrencyWithdraw(destinationAddress, currency, withdrawAmount);
            return true;
        }

        /**
         * deduct funds from a users balance
         * <param name="destinationAddress">address to take balance from</param>
         * <param name="currency">currency type</param>
         * <param name="withdrawAmount">currency type</param>
         */
        public static bool WithdrawCurrency(byte[] destinationAddress, byte[] currency, BigInteger withdrawAmount)
        {
            if (!Runtime.CheckWitness(destinationAddress))
            {
                // ensure transaction is signed properly
                Runtime.Notify("WithdrawCurrency() CheckWitness failed", destinationAddress);
                return false;
            }

            Runtime.Notify("WithdrawCurrency() destinationAddress", destinationAddress);
            Runtime.Notify("WithdrawCurrency() currency", currency);
            Runtime.Notify("WithdrawCurrency() withdrawAmount", withdrawAmount);

            BigInteger currentBalance = GetBalanceOfCurrency(destinationAddress, currency);

            if (currentBalance <= 0 || currentBalance < withdrawAmount)
            {
                Runtime.Notify("WithdrawCurrency() insufficient funds", currentBalance);
                return false;
            }

            CurrencyWithdraw(destinationAddress, currency, withdrawAmount);
            RefundAsset(destinationAddress, currency, withdrawAmount);
            return true;
        }

        /**
         * retrieve the currency balance for address
         * <param name="address">address to check balance for</param>
         * <param name="currency">currency type</param>
         */
        public static BigInteger GetBalanceOfCurrency(byte[] address, byte[] currency)
        {
            byte[] indexName = GetCurrencyIndexName(address, currency);
            Runtime.Notify("GetBalanceOfCurrency() indexName", indexName);

            BigInteger currentBalance = Storage.Get(Storage.CurrentContext, indexName).AsBigInteger();
            Runtime.Notify("GetBalanceOfCurrency() currency", currency);
            Runtime.Notify("GetBalanceOfCurrency() currentBalance", currentBalance);
            return currentBalance;
        }

        /**
         * remove takeFunds of currency type to address current baalance
         * <param name="address">address to set balance for</param>
         * <param name="currency">currency being deposited</param>
         * <param name="takeFunds">how much currency</param>
         */
        public static void CurrencyWithdraw(byte[] address, byte[] currency, BigInteger takeFunds)
        {
            byte[] indexName = GetCurrencyIndexName(address, currency);
            BigInteger currentBalance = GetBalanceOfCurrency(address, currency);
            Runtime.Notify("CurrencyWithdraw() indexName", indexName);
            Runtime.Notify("CurrencyWithdraw() currentBalance", currentBalance);
            Runtime.Notify("CurrencyWithdraw() takeFunds", takeFunds);

            BigInteger updateBalance = currentBalance - takeFunds;

            if (updateBalance <= 0)
            {
                Runtime.Notify("CurrencyWithdraw() removing balance reference", updateBalance);
                Storage.Delete(Storage.CurrentContext, indexName);
            }
            else
            {
                Runtime.Notify("CurrencyWithdraw() setting balance", updateBalance);
                Storage.Put(Storage.CurrentContext, indexName, updateBalance);
            }
        }

        /**
         * add newBalance of currency type to address current balance
         * <param name="address">address to set balance for</param>
         * <param name="currency">currency being deposited</param>
         * <param name="newFunds">how much currency</param>
         */
        public static void CurrencyDeposit(byte[] address, byte[] currency, BigInteger newFunds)
        {
            byte[] indexName = GetCurrencyIndexName(address, currency);
            BigInteger currentBalance = GetBalanceOfCurrency(address, currency);
            Runtime.Notify("CurrencyDeposit() indexName", indexName);
            Runtime.Notify("CurrencyDeposit() currentBalance", currentBalance);
            Runtime.Notify("CurrencyDeposit() newFunds", newFunds);

            BigInteger updateBalance = currentBalance + newFunds;

            if (updateBalance <= 0)
            {
                Runtime.Notify("CurrencyDeposit() removing balance reference", updateBalance);
                Storage.Delete(Storage.CurrentContext, indexName);
            }
            else
            {
                Runtime.Notify("CurrencyDeposit() setting balance", updateBalance);
                Storage.Put(Storage.CurrentContext, indexName, updateBalance);
            }
        }

        /**
         * set the balance of a NEP5 token for a user
         * <param name="address">address to set balance for</param>
         * <param name="currency">currency type - will be scriptHash of NEP5 contract</param>
         * <param name="newFunds">how much currency</param>
         */
        public static bool SetBalanceOfNEP5Currency(byte[] address, string currency, BigInteger newFunds)
        {
            if (!VerifyOwnerAccount())
            {
                // only the contract owner can set the balance of nep5 tokens
                Runtime.Notify("SetBalanceOfNEP5Currency() VerifyOwnerAccount failed", false);
                return false;
            }

            Runtime.Notify("SetBalanceOfNEP5Currency() calling CurrencyDeposit()", address);
            CurrencyDeposit(address, currency.AsByteArray(), newFunds);
            return true;
        }

        /**
         * helper method to test number of args provided
         */
        public static bool RequireArgumentLength(object[] args, int numArgs)
        {
            Runtime.Notify("RequireArgumentLength() required / received", numArgs, args.Length);
            return args.Length == numArgs;
        }

        /**
         * retrieve the script hash for the contract admin account
         * <returns>scriptHash of admin account</returns>
         */
        public static byte[] GetAdminAccount()
        {
            return AdminAccount;
        }

        /**
         * retrieve the script hash for the transaction helper account
         * <returns>scriptHash of TXHelper account</returns>
         */
        public static byte[] GetTXHelper()
        {
            byte[] helperAddress = Storage.Get(Storage.CurrentContext, "TXHelper");
            Runtime.Notify("GetTXHelper() helperAddress", helperAddress);
            return helperAddress;
        }

        /**
         * update the script hash for the transaction helper account
         * <param name="helperAccount">byte array containing the scriptHash of the transaction helper</param>
         * <returns>true if account was updated</returns>
         */
        public static bool SetTXHelper(byte[] helperAccount)
        {
            if (!VerifyOwnerAccount())
            {
                // only the contract owner can set the transfer helper account
                Runtime.Log("SetTXHelper() VerifyOwnerAccount failed");
                return false;
            }

            if (helperAccount.Length != 20)
            {
                // helper account must be a scriptHash (20 chars)
                Runtime.Notify("SetTXHelper() Invalid Length (expected 20) received", helperAccount.Length);
                return false;
            }

            Storage.Put(Storage.CurrentContext, "TXHelper", helperAccount);
            return true;
        }

        /**
         * verify that the witness (invocator) is valid
         * <param name="verifiedAddress">known good address to compare with invocator</param>
         * <returns>true if account was verified</returns>
         */
        public static bool VerifyWitness(byte[] verifiedAddress)
        {
            bool isWitness = Runtime.CheckWitness(verifiedAddress);

            Runtime.Notify("VerifyWitness() verifiedAddress", verifiedAddress);
            Runtime.Notify("VerifyWitness() isWitness", isWitness);

            return isWitness;
        }

        /**
         * verify that the invocator is actually the owner account
         * <returns>true if account was verified</returns>
         */
        public static bool VerifyOwnerAccount()
        {
            Runtime.Notify("VerifyOwnerAccount() Owner", GetAdminAccount());
            return VerifyWitness(GetAdminAccount());
            //Runtime.Notify("VerifyOwnerAccount() OwnerPubKey", OwnerPubKey);
            //byte[] signature = operation.AsByteArray();
            //bool sigVerify = VerifySignature(arg0, OwnerPubKey);
            //Runtime.Notify("Verification() sigVerify", sigVerify);
        }

        /**
         * post deployment initialisation
         * can only be run once by contract owner
         * <returns>true if init was performed</returns>
         */
        public static bool InitSC()
        {
            if (!VerifyOwnerAccount())
            {
                // owner authentication failed
                Runtime.Log("InitSC() VerifyOwnerAccount failed");
                return false;
            }

            BigInteger totalSupply = TotalSupply();
            if (totalSupply > 0)
            {
                // contract has already been initialised
                Runtime.Log("InitSC() SC has already been initialised");
                return false;
            }

            // set txHelper to be admin account 
            SetTXHelper(GetAdminAccount());

            Runtime.Notify("InitSC() Creating Admin Token", 1);
            ulong deployAmount = 1 * factor;
            SetBalanceOf(GetAdminAccount(), deployAmount);
            SetTotalSupply(deployAmount);
            Transferred(null, GetAdminAccount(), deployAmount);
            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        // BEGIN NEP5 implementation
        //////////////////////////////////////////////////////////////////////////////////////////
        /**
         * create tokens upon receipt of neo
         */
        public static bool MintTokens()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput reference = tx.GetReferences()[0];
            if (reference.AssetId != NEO)
            {
                // transferred asset is not neo, do nothing
                Runtime.Notify("MintTokens() reference.AssetID is not NEO", reference.AssetId);
                return false;
            }

            byte[] sender = reference.ScriptHash;
            TransactionOutput[] outputs = tx.GetOutputs();
            byte[] receiver = ExecutionEngine.ExecutingScriptHash;
            ulong receivedNEO = 0;
            Runtime.Notify("DepositAsset() recipient of funds", ExecutionEngine.ExecutingScriptHash);

            // Gets the total amount of Neo transferred to the smart contract address
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == receiver)
                {
                    receivedNEO += (ulong)output.Value;
                }
            }

            Runtime.Notify("MintTokens() receivedNEO", receivedNEO);

            if (receivedNEO <= 0)
            {
                Runtime.Log("MintTokens() receivedNEO was <= 0");
                return false;
            }

            ulong exchangeRate = CurrentSwapRate();
            Runtime.Notify("MintTokens() exchangeRate", exchangeRate);

            if (exchangeRate == 0)
            {
                // ico has ended, or the token supply is exhausted
                Refund(sender, receivedNEO);
                Runtime.Log("MintTokens() exchangeRate was == 0");

                return false;
            }

            ulong numMintedTokens = receivedNEO * exchangeRate / 100000000;

            Runtime.Notify("MintTokens() receivedNEO", receivedNEO);
            Runtime.Notify("MintTokens() numMintedTokens", numMintedTokens);

            SetBalanceOf(sender, BalanceOf(sender) + numMintedTokens);
            SetTotalSupply(numMintedTokens);
            Transferred(null, sender, numMintedTokens);
            return true;
        }

        /**
         * set the total supply value
         */
        private static void SetTotalSupply(ulong newlyMintedTokens)
        {
            BigInteger currentTotalSupply = TotalSupply();
            Runtime.Notify("SetTotalSupply() newlyMintedTokens", newlyMintedTokens);
            Runtime.Notify("SetTotalSupply() currentTotalSupply", currentTotalSupply);
            Runtime.Notify("SetTotalSupply() newlyMintedTokens + currentTotalSupply", newlyMintedTokens + currentTotalSupply);

            Storage.Put(Storage.CurrentContext, "totalSupply", currentTotalSupply + newlyMintedTokens);
        }

        /**
         * how many tokens have been issued
         */
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        /**
         * transfer value between from and to accounts
         */
        public static bool Transfer(byte[] from, byte[] to, BigInteger transferValue)
        {
            Runtime.Notify("Transfer() transferValue", transferValue);
            if (transferValue <= 0)
            {
                // don't accept stupid values
                Runtime.Notify("Transfer() transferValue was <= 0", transferValue);
                return false;
            }
            if (!Runtime.CheckWitness(from))
            {
                // ensure transaction is signed properly
                Runtime.Notify("Transfer() CheckWitness failed", from);
                return false;
            }
            if (from == to)
            {
                // don't waste resources when from==to
                Runtime.Notify("Transfer() from == to failed", to);
                return true;
            }
            BigInteger fromBalance = BalanceOf(from);                   // retrieve balance of originating account
            if (fromBalance < transferValue)
            {
                Runtime.Notify("Transfer() fromBalance < transferValue", fromBalance);
                // don't transfer if funds not available
                return false;
            }

            SetBalanceOf(from, fromBalance - transferValue);            // remove balance from originating account
            SetBalanceOf(to, BalanceOf(to) + transferValue);            // set new balance for destination account

            Transferred(from, to, transferValue);
            return true;
        }

        /**
         * set newBalance for address
         */
        private static void SetBalanceOf(byte[] address, BigInteger newBalance)
        {
            if (newBalance <= 0)
            {
                Runtime.Notify("SetBalanceOf() removing balance reference", newBalance);
                Storage.Delete(Storage.CurrentContext, address);
            }
            else
            {
                Runtime.Notify("SetBalanceOf() setting balance", newBalance);
                Storage.Put(Storage.CurrentContext, address, newBalance);
            }
        }

        /**
         * retrieve the number of tokens stored in address
         */
        public static BigInteger BalanceOf(byte[] address)
        {
            BigInteger currentBalance = Storage.Get(Storage.CurrentContext, address).AsBigInteger();
            Runtime.Notify("BalanceOf() currentBalance", currentBalance);
            return currentBalance;
        }

        /**
         * determine whether or not the ico is still running and provide a bonus rate for the first 3 weeks
         */
        private static ulong CurrentSwapRate()
        {
            if (TotalSupply() >= totalAmountNTC)
            {
                // supply has been exhausted
                return 0;
            }

            uint currentTimestamp = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;
            int timeRunning = (int)currentTimestamp - icoStartTimestamp;
            Runtime.Notify("CurrentSwapRate() timeRunning", timeRunning);

            if (currentTimestamp > icoEndTimestamp || timeRunning < 0)
            {
                // ico period has not started or is ended
                return 0;
            }

            ulong bonusRate = 0;

            if (timeRunning < 604800)
            {
                // first week gives 30% bonus
                bonusRate = 30;
            }
            else if (timeRunning < 1209600)
            {
                // second week gives 20% bonus
                bonusRate = 20;
            }
            else if (timeRunning < 1814400)
            {
                // third week gives 10% bonus
                bonusRate = 10;
            }

            ulong swapRate = (icoBaseExchangeRate * (100 + bonusRate)) / 100;

            Runtime.Notify("CurrentSwapRate() bonusRate", bonusRate);
            Runtime.Notify("CurrentSwapRate() swapRate", swapRate);
            return swapRate;
        }
        //////////////////////////////////////////////////////////////////////////////////////////
        // END NEP5 implementation
        //////////////////////////////////////////////////////////////////////////////////////////
    }
}