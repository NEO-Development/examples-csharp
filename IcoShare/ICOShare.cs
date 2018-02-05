using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace IcoShare
{
    public class IcoShare {
        public IcoShare() { this.StartTime = new int(); }

        /// <summary>
        /// Auto generated address for users to send contribution
        /// Uniquq Id
        /// </summary>
        public byte[] IncomingAddress { get; private set; }
        /// <summary>
        /// Epoch
        /// </summary>
        public int StartTime { get; private set; }

        /// <summary>
        /// Epoch
        /// </summary>
        public int EndTime { get; set; } 
        /// <summary>
        /// IcoShare creator defined values
        /// </summary>
        public int MinContribution { get; set; }
        public int MaxContribution { get; set; }
        /// <summary>
        /// ICO bundle value, defied by ICO owner but selected by ICOShare creator
        /// </summary>
        public int ContributionBundle { get; set; }
        /// <summary>
        /// Original ICO address
        /// </summary>
        public byte[] OutgoingAddress { get; set; }
    }

    public class Contribution
    {
        public byte[] SenderAddress { get; set; }
        public byte[] IcoShareAddress { get; set; }
        public int Amount { get; set; }
    }
    
    public class ICOShare : SmartContract
    {
        //Token Settings
        public static readonly byte[] Owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();

        public static readonly char POSTFIX_A = 'A';
        public static readonly char POSTFIX_STARTDATE = 'S';
        public static readonly char POSTFIX_ENDDATE = 'E';

        #region Helper 
        private bool IsOwner()
        {
            return Runtime.CheckWitness(Owner);
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

        private static byte[] IntToBytes(BigInteger value)
        {
            byte[] buffer = value.ToByteArray();
            return buffer;
        }
        private static BigInteger BytesToInt(byte[] array)
        {
            return array.AsBigInteger() + 0;
        }
        #endregion

        #region Private 
        private bool CompleteIcoShare(byte[] icoShareAddress)
        {
            throw new NotImplementedException();
            //BigInteger totalContribution = GetOnStorageKey(icoShareAddress).AsBigInteger();
            //byte[] outgoingAddress = GetOnPostfix(icoShareAddress, )
        }
        #endregion

        public static void Main()
        {
            Storage.Put(Storage.CurrentContext, "Hello", "World");
        }

        public byte[] StartNewIcoShare(byte[] outgoingAddress, int endTime,
            BigInteger contributionBundle, BigInteger minContribution, BigInteger maxContribution)
        {
            //Check Maximum
            //Auto generate new address
            byte[] incomingAddress = "123".AsByteArray();



            //Return incoming address
            return incomingAddress;
        }

        

        public BigInteger CurrentContribution(byte[] incomingAddress)
        {
            throw new NotImplementedException();
        }

        //TODO : Automatic??
        public bool FinalizeIcoShare()
        {
            //Only by backend

            //Check owner

            //Finalize 

            throw new NotImplementedException();
        }
    }
}
