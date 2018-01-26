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
        public int BundleContribution { get; set; }
        /// <summary>
        /// Original ICO address
        /// </summary>
        public byte[] OutgoingAddress { get; set; }
    }

    public class ICOShare : SmartContract
    {
        public static readonly char POSTFIX_A = 'A';
        public static readonly char POSTFIX_STARTDATE = 'S';
        public static readonly char POSTFIX_ENDDATE = 'E';

        public static void Main()
        {
            Storage.Put(Storage.CurrentContext, "Hello", "World");
        }

        public byte[] StartNewIcoShare(byte[] outgoingAddress, int endTime,
            BigInteger bundleContribution, BigInteger minContribution, BigInteger maxContribution)
        {
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
