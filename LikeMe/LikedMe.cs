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

        public static readonly BigInteger maxRate = 5;
        public static readonly BigInteger rateRatio = 1;
        public static readonly string initialRating = "3000";

        //031a6c6fbbdf02ca351745fa86b9ba5a9452d785ac4f7fc2b7548ca2a46c4fcf4a (owner)
        //0226df9dee85d23f2159af39bbcb8551c3485884e00028e8f39dabe67443e4d615 (user a)
        private static readonly byte[] owner =
            new byte[] { 3, 26, 108, 111, 187, 223, 2, 202, 53, 23, 69, 250, 134, 185, 186, 90, 148, 82, 215, 133, 172, 79, 127, 194, 183, 84, 140, 162, 164, 108, 79, 207, 74 };

        public static object Main(string method, params object[] args)
        {
            Runtime.Notify("Invoking : " + method);

            //PUBLIC OPERATIONS  
            if (method == "rateOf") return RateOf((byte[])args[0]);

            //ADMIN OPERATIONS
            if (!Runtime.CheckWitness(owner)) { Runtime.Notify("Couldn't verify owner."); return false; }

            //Like
            if (method == "like")       return Rate((byte[])args[0], (byte[])args[1], BytesToInt((byte[])args[2]), true);
            //Dislike
            if (method == "dislike")    return Rate((byte[])args[0], (byte[])args[1], BytesToInt((byte[])args[2]), false);
            //SetInitial
            if (method == "setInitial") return SetInitialRate((byte[])args[0]);

            Runtime.Log("No operation is selected."); 
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
        
        private static BigInteger CalculateRate(BigInteger originatorRate, BigInteger stars)
        {
            return stars * rateRatio;
        }

        private static BigInteger AddRate(BigInteger originatorRate, BigInteger targetRate, BigInteger stars)
        {
            Runtime.Notify("originatorRate", originatorRate);
            Runtime.Notify("targetRate", targetRate);
            Runtime.Notify("stars", stars);
            
            var temp2 = CalculateRate(originatorRate, stars);
            Runtime.Notify("CalculateRate", temp2);

            BigInteger newRate = targetRate + temp2;
            Runtime.Notify("newRate", newRate);


            if (newRate > maxRate)
            {
                Runtime.Notify("newRate > maxRate");
                newRate = maxRate;
            }


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
        /// Operation name : rateOf
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger RateOf(byte[] address)
        {
            BigInteger currentBalance = Storage.Get(Storage.CurrentContext, address).AsBigInteger();
            Runtime.Notify("RATE", address, currentBalance);
            return currentBalance;
        }

        /// <summary>
        /// Sets initial rate for account owner 'to', onlye SC owner can invoke 
        /// Operation name : setInitial
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool SetInitialRate(byte[] to)
        {
            Runtime.Log("Setting initial rate for : " + to);

            //only owner can set initial 
            if (!Runtime.CheckWitness(owner)) { Runtime.Log("Couldn't verify owner."); return false; }

            //Check if already has rating 
            var currentRate = Storage.Get(Storage.CurrentContext, to);
            if (currentRate != null) { Runtime.Log("User already has rating : " + currentRate); return false; }

            //Put initial rating 
            Storage.Put(Storage.CurrentContext, to, initialRating);
            Runtime.Log("Initial rate given : " + to);
            Runtime.Log("Initial rating : " + initialRating);

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
            
            //Check accounts 
            var originatorRate = RateOf(from);
            var targetRate = RateOf(to);

            if (originatorRate == null || targetRate == null) {
                Runtime.Notify("Null rate!", IntToBytes(originatorRate), IntToBytes(targetRate));
                return false;
            };

            //Set new rate 
            //BigInteger newTargetRate = targetRate;

            if (isLike)
                Storage.Put(Storage.CurrentContext, from, targetRate + stars);
            //newTargetRate = AddRate(originatorRate, targetRate, stars);
            else
                Storage.Put(Storage.CurrentContext, from, targetRate - stars);
            //newTargetRate = RemoveRate(originatorRate, targetRate, stars);

            //Update rate 
            //Storage.Put(Storage.CurrentContext, to, newTargetRate);
            //var newRate = BytesToInt(Storage.Get(Storage.CurrentContext, to));
            //Runtime.Notify("New rate", IntToBytes(newRate));

            RateOf(to);

            return true;
        }
    }
}
