using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace LikeMe
{
    public class LikeMe : SmartContract
    {
        private static string Name() => "LikeMe";
        private static string Symbol() => "LKM";

        public static readonly BigInteger maxRate = 5000;
        public static readonly BigInteger rateRatio = 1;
        public static readonly BigInteger initialRating = 3000;
        
        public static readonly byte[] owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();

        public static object Main(string operation, params object[] args)
        {
            //PUBLIC OPERATIONS  
            if (operation == "rateOf") return RateOf((byte[])args[0]);

            //ADMIN OPERATIONS
            if (!Runtime.CheckWitness(owner)) { Runtime.Notify("Couldn't verify owner."); return false; }

            //Like
            if (operation == "like")
            {
                if (args.Length != 3) return false;
                byte[] from = (byte[])args[0];
                byte[] to = (byte[])args[1];
                BigInteger value = (BigInteger)args[2];
                return Rate(from, to, value, true);
            }

            //Dislike
            if (operation == "dislike")
            {
                if (args.Length != 3) return false;
                byte[] from = (byte[])args[0];
                byte[] to = (byte[])args[1];
                BigInteger value = (BigInteger)args[2];
                return Rate(from, to, value, false);
            }
            
            //SetInitial
            if (operation == "setInitial") return SetInitialRate((byte[])args[0]);

            Runtime.Log("No operation is selected."); 
            return false;
        }
        
        private static byte[] IntToBytes(BigInteger value)
        {
            byte[] buffer = value.ToByteArray();
            return buffer;
        }
        private static BigInteger BytesToInt(byte[] array)
        {
            return array.AsBigInteger() + 0;
        }
        private static BigInteger CalculateRate(BigInteger originatorRate, BigInteger stars)
        {
            return stars * rateRatio;
        }
        private static BigInteger AddRate(BigInteger originatorRate, BigInteger targetRate, BigInteger stars)
        {
            var calculated = CalculateRate(originatorRate, stars);
            BigInteger newRate = targetRate + calculated;

            if (newRate > maxRate) return maxRate;
            return newRate;
        }
        private static BigInteger RemoveRate(BigInteger originatorRate, BigInteger targetRate, BigInteger stars)
        {
            BigInteger newRate = targetRate - CalculateRate(originatorRate, stars);
            if (newRate < 0) newRate = 0;

            return newRate;
        }
        
        /// <summary>
        /// Get rate of someone
        /// Operation name : rateOf
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger RateOf(byte[] address)
        {
            Runtime.Notify("RATE", address, BytesToInt( Storage.Get(Storage.CurrentContext, address)));
            return BytesToInt(Storage.Get(Storage.CurrentContext, address));
        }

        /// <summary>
        /// Sets initial rate for account owner 'to', onlye SC owner can invoke 
        /// Operation name : setInitial
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool SetInitialRate(byte[] to)
        {
            //Check if already has rating 
            var currentRate = Storage.Get(Storage.CurrentContext, to);
            if (currentRate != null) { Runtime.Notify("User already has rating.", BytesToInt(currentRate)); return false; }

            //Put initial rating 
            Storage.Put(Storage.CurrentContext, to, initialRating);
            var rate = Storage.Get(Storage.CurrentContext, to);
            Runtime.Notify("Initial rate given.", to, BytesToInt(rate));
            
            return true;
        }

        /// <summary>
        /// Someone rates someone, like or dislike 
        /// Operation name : like, dislike
        /// </summary>
        /// <param name="from">scripthash of rater</param>
        /// <param name="to">scripthash of rated</param>
        /// <param name="stars">amount of starts to rate</param>
        /// <param name="isLike">is like or dislike</param>
        /// <returns></returns>
        public static bool Rate(byte[] from, byte[] to, BigInteger stars, bool isLike)
        {
            if (stars <= 0) return false;
            if (from == to) return true;

            //Check accounts 
            var originatorRate = RateOf(from);
            var targetRate = RateOf(to);

            if (originatorRate == 0 || targetRate == 0) {
                Runtime.Notify("Null rate!", originatorRate, targetRate);
                return false;
            };

            //Set new rate 
            BigInteger newTargetRate = targetRate;

            //Like / Dislike
            if (isLike) newTargetRate = AddRate(originatorRate, targetRate, stars);
            else newTargetRate = RemoveRate(originatorRate, targetRate, stars);

            //Update rate 
            Storage.Put(Storage.CurrentContext, to, newTargetRate);

            RateOf(to);

            return true;
        }
    }
}
