using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace VersionedContract
{ 
    public class VersionedContract : SmartContract
    {
        //Magic string 
        private static string IsActivatedKey { get { return "IsActivatedKey"; } }
        
        private static string Version = "0.0.1";
        public static readonly byte[] Owner = Helper.ToScriptHash("AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y"); 

        public static object Main(string Event, params object[] args)
        {
            switch (Event)
            {
                case "invoke":      return Invoke();
                case "register":    return ChangeStatus((bool)args[0], (byte[])args[1]);
                case "version":     return GetVersion();
                default:            return false;
            }
        }

        private static string GetVersion()
        {
            return Version;
        }

        private static bool Invoke()
        {
            //Check if contract is active 
            var isActivated = Storage.Get(Storage.CurrentContext, IsActivatedKey).AsString();
            if (isActivated == "" && isActivated == "false")
            {
                Runtime.Log("Contract is inactive!");
                return false;
            }

            //TODO : Invoke logic
            Runtime.Log("Invoked.");
            return true;
        }

        private static bool ChangeStatus(bool activated, byte[] signature)
        {
            //Verify
            bool verified = VerifySignature(Owner, signature);
            if (!verified) return false;

            //Change status
            string IsActivated = "false";
            if (activated) IsActivated = "true";

            Storage.Put(Storage.CurrentContext, IsActivatedKey, IsActivated);
            Runtime.Log("IsActivated : " + IsActivated);

            return true;
        }
    }
}
