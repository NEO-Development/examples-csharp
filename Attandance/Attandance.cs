using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;

namespace Attandance
{
    public class Attandance : SmartContract
    {
        //Token Settings
        public static string Name() => "name of the token";
        public static string Symbol() => "SymbolOfTheToken";
        public static readonly byte[] Owner = { 47, 60, 170, 33, 216, 40, 148, 2, 242, 150, 9, 84, 154, 50, 237, 160, 97, 90, 55, 183 };
        public static byte Decimals() => 8;
        private const ulong neo_decimals = 100000000;

        //ICO Settings
        private const int InitialAtandanceFactor = 10;
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private const ulong basic_rate = 100;
        private const int ico_start_time = 1506787200;
        private const int ico_end_time = 1538323200; //TODO : Parametrik 

        public static readonly char POSTFIX_ATTANDANCE = 'A';
        public static readonly char POSTFIX_CURRENT = 'B';

        public static readonly byte[] STRKEY_TOTALSUPPLY = "TOTAL_SUPPLY".AsByteArray();
        public static readonly byte[] STRKEY_ATTFACTOR = "ATTANDANCE_FACTOR".AsByteArray();
        public static readonly byte[] STRKEY_CURRENTCONT = "CURRENT_CONTRIBUTION".AsByteArray();
        
        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> Refund;

        public static object Main(string operation, params object[] args)
        {
            if (operation == "deploy") return Deploy();
            if (operation == "mintTokens") return MintTokens();
            if (operation == "totalSupply") return TotalSupply();
            if (operation == "name") return Name();
            if (operation == "symbol") return Symbol();
            if (operation == "transfer")
            {
                if (args.Length != 3) return false;
                byte[] from = (byte[])args[0];
                byte[] to = (byte[])args[1];
                BigInteger value = (BigInteger)args[2];
                return Transfer(from, to, value);
            }
            if (operation == "balanceOf")
            {
                if (args.Length != 1) return 0;
                byte[] account = (byte[])args[0];
                return BalanceOf(account);
            }
            if (operation == "decimals") return Decimals();

            //you can choice refund or not refund
            byte[] sender = GetSender();
            ulong contribute_value = GetContributeValue();
            if (contribute_value > 0 && sender.Length != 0)
            {
                Refund(sender, contribute_value);
            }
            return false;
        }

        #region PRIVATE METHODS 
        // get smart contract script hash
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        // check whether asset is neo and get sender script hash
        private static byte[] GetSender()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] reference = tx.GetReferences();
            // you can choice refund or not refund
            foreach (TransactionOutput output in reference)
            {
                if (output.AssetId == neo_asset_id) return output.ScriptHash;
            }
            return new byte[0];
        }

        // get all you contribute neo amount
        private static ulong GetContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Neo
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == neo_asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }

        // The function CurrentSwapRate() returns the current exchange rate
        // between ico tokens and neo during the token swap period
        private static ulong CurrentSwapRate()
        {
            const int ico_duration = ico_end_time - ico_start_time;
            uint now = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp + 15;
            int time = (int)now - ico_start_time;
            if (time < 0)
            {
                return 0;
            }
            else if (time < ico_duration)
            {
                return basic_rate;
            }
            else
            {
                return 0;
            }
        }
        
        private static BigInteger CurrentSwapToken(byte[] sender, ulong swap_rate)
        {
            var sendersAttandance = GetOnPostfix(sender, POSTFIX_ATTANDANCE).AsBigInteger();
            var attandanceFactor = GetOnStorageKey(STRKEY_ATTFACTOR).AsBigInteger();

            BigInteger token = swap_rate + attandanceFactor * sendersAttandance;

            return token;
        }

        private static byte[] GetOnPostfix(byte[] key, char postfix)
        {
            string k = key.AsString() + postfix;
            return Storage.Get(Storage.CurrentContext, k);
        }
        private static byte[] GetOnStorageKey(byte[] storageKey)
        {
            return Storage.Get(Storage.CurrentContext, storageKey);
        }
        private static void PutOnPostfix(byte[] key, byte[] value, char postfix)
        {
            string k = key.AsString() + postfix;
            Storage.Put(Storage.CurrentContext, k, value);
            Runtime.Notify("PUT", value);
        }
        
        #endregion

        #region PUBLIC METHODS
        // initialization parameters, only once
        // 初始化参数
        public static bool Deploy()
        {
            if (!Runtime.CheckWitness(Owner)) return false;

            Storage.Put(Storage.CurrentContext, STRKEY_ATTFACTOR, InitialAtandanceFactor);
            return true;
        }
        // function that is always called when someone wants to transfer tokens.
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (from == to) return true;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return false;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            Transferred(from, to, value);
            return true;
        }
        
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }
        
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        // The function MintTokens is only usable by the chosen wallet
        // contract to mint a number of tokens proportional to the
        // amount of neo sent to the wallet contract. The function
        // can only be called during the tokenswap period
        public static bool MintTokens()
        {
            byte[] sender = GetSender();
            // contribute asset is not neo
            if (sender.Length == 0)
            {
                return false;
            }

            ulong contributeValue = GetContributeValue();
            var currentContribution = GetOnStorageKey(STRKEY_CURRENTCONT).AsBigInteger();

            //Crowdfunding is not active, refunded
            if (currentContribution == 0)
            {
                Refund(sender, contributeValue);
                return false;
            }

            //Crowdfunding failure, no refund
            if (contributeValue != currentContribution)
            {
                return false;
            }

            // the current exchange rate between ico tokens and neo during the token swap period
            ulong swap_rate = CurrentSwapRate();
            // crowdfunding failure
            if (swap_rate == 0)
            {
                Refund(sender, contributeValue);
                return false;
            }
            // you can get current swap token amount
            BigInteger token = CurrentSwapToken(sender, swap_rate);
            if (token == 0)
            {
                return false;
            }

            //Crowdfunding success
            //Update user's balance
            BigInteger balance = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
            Storage.Put(Storage.CurrentContext, sender, token + balance);
            //Update total suply
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, STRKEY_TOTALSUPPLY).AsBigInteger();
            Storage.Put(Storage.CurrentContext, STRKEY_TOTALSUPPLY, token + totalSupply);
            Transferred(null, sender, token);

            //Update sender's attandance
            var senderAttanance = GetOnPostfix(sender, POSTFIX_ATTANDANCE).AsBigInteger();
            PutOnPostfix(sender, (senderAttanance+1).AsByteArray(), POSTFIX_ATTANDANCE);

            return true;
        }
        
        public static bool SetContributionValue(string contributionValue)
        {
            if (!Runtime.CheckWitness(Owner)) return false;

            if (contributionValue == "")
            {
                Storage.Delete(Storage.CurrentContext, STRKEY_CURRENTCONT);
                return true;
            }

            Storage.Put(Storage.CurrentContext, STRKEY_CURRENTCONT, contributionValue);
            return true;
        }

        public static bool SetAttandanceFactor(BigInteger factor)
        {
            if (!Runtime.CheckWitness(Owner)) return false;

            Storage.Put(Storage.CurrentContext, STRKEY_ATTFACTOR, factor);
            return true;
        }

        #endregion
    }
}
