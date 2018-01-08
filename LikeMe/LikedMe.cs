using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace LikeMe
{
    public static class VMExtensions
    {
        public class VMString
        {
            private string _text;

            public VMString(string text)
            {
                _text = text;
            }

            public string SubString(int index)
            {
                return _text.Substring(index);
            }
        }
        public class VMInt
        {
            private int _number;

            public VMInt(int number)
            {
                _number = number;
            }

            public string ToString()
            {
                return _number.ToString();
            }
        }

        public static VMString VM(this string text)
        {
            return new VMString(text);
        }
        public static VMInt VM(this int number)
        {
            return new VMInt(number);
        }
    }

    public class LikedMe : SmartContract
    {
        private static string Name() => "LikedMe";
        private static string Symbol() => "LKM";

        public static readonly int maxRate = 5;
        public static readonly int rateRatio = 1;
        public static readonly string initialRating = "3000";

        private static readonly byte[] owner = { 134, 145, 12, 9, 163, 135, 211, 239, 30, 52, 183, 75, 246, 135, 108, 158, 9, 124, 124, 59 };

        public static object Main(string method, params object[] args)
        {
            if (!Runtime.CheckWitness((byte[])args[0])) return false;

            if (method == "like") return Rate((byte[])args[0], (byte[])args[1], BytesToInt((byte[])args[2]), true);
            if (method == "dislike") return Rate((byte[])args[0], (byte[])args[1], BytesToInt((byte[])args[2]), false);
            if (method == "rateOf") return RateOf((byte[])args[0]);
            if (method == "setInitial") return SetInitialRate((byte[])args[0]);

            return false;
        }

        #region Utility codes 

        private static byte[] IntToBytes(BigInteger value)
        {
            byte[] buffer = value.ToByteArray();
            return buffer;
        }

        private static BigInteger BytesToInt(byte[] array)
        {
            var buffer = new BigInteger(array);
            return buffer;
        }

        /// <summary>
        /// Rate calculation
        /// </summary>
        /// <param name="originatorRate"></param>
        /// <param name="stars"></param>
        /// <returns></returns>
        private static BigInteger CalculateRate(BigInteger originatorRate, BigInteger stars)
        {
            return stars * rateRatio;
        }

        private static BigInteger AddRate(BigInteger originatorRate, BigInteger targetRate, BigInteger stars)
        {
            BigInteger newRate = targetRate + CalculateRate(originatorRate, stars);
            if (newRate > maxRate) newRate = maxRate;

            return newRate;
        }

        private static BigInteger RemoveRate(BigInteger originatorRate, BigInteger targetRate, BigInteger stars)
        {
            BigInteger newRate = targetRate - CalculateRate(originatorRate, stars);
            if (newRate < 0) newRate = 0;

            return newRate;
        }
        #endregion

        /// <summary>
        /// Get rate of someone
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger RateOf(byte[] address)
        {
            BigInteger currentBalance = Storage.Get(Storage.CurrentContext, address).AsBigInteger();
            Runtime.Notify("Rate", address, currentBalance);
            return currentBalance;
        }

        /// <summary>
        /// Sets initial rate for account owner 'to', onlye SC owner can invoke 
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        private static bool SetInitialRate(byte[] to)
        {
            //only owner can set initial 
            if (!Runtime.CheckWitness(owner)) return false;

            //Check if already has rating 
            var currentRate = Storage.Get(Storage.CurrentContext, to);
            if (currentRate != null) return false;

            //Put initial rating 
            Runtime.Notify("PUT", to, initialRating);
            Storage.Put(Storage.CurrentContext, to, initialRating);
            Runtime.Notify("Initial rate given.", to, initialRating, Blockchain.GetHeight());

            return true;
        }

        /// <summary>
        /// Rate someone 
        /// </summary>
        /// <param name="originator"></param>
        /// <param name="to"></param>
        /// <param name="stars"></param>
        /// <param name="isLike"></param>
        /// <returns></returns>
        private static bool Rate(byte[] originator, byte[] to, BigInteger stars, bool isLike)
        {
            //Only originator can rate a user
            if (!Runtime.CheckWitness(originator)) return false;

            //Can not vote themselves
            if (Runtime.CheckWitness(to)) return false;

            //Check accounts 
            var originatorRate = RateOf(originator);
            var targetRate = RateOf(to);

            if (originatorRate == null || targetRate == null) return false;

            //Set new rate 
            BigInteger newTargetRate = originatorRate;

            if (isLike) newTargetRate = AddRate(originatorRate, targetRate, stars);
            else newTargetRate = RemoveRate(originatorRate, targetRate, stars);

            //Update rate 
            Storage.Put(Storage.CurrentContext, to, newTargetRate.AsByteArray());
            Runtime.Notify("Rate given.", originator, to, stars, isLike ? "Like" : "Dislike", Blockchain.GetHeight());

            return true;
        }
    }
}
