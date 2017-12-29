using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace ProxyContract
{
    public class ProxyContract : SmartContract
    {

        private static readonly string ContactAddressKey = "ContactAddressKey";
        public static readonly byte[] Owner = { 47, 60, 170, 33, 216, 40, 148, 2, 242, 150, 9, 84, 154, 50, 237, 160, 97, 90, 55, 183 };

        public static object Main(string operation, byte[] signature, params object[] args)
        {
            switch (operation)
            {
                case "updateAddress": return UpdateAddress((string)args[0], signature);
                case "register": return CallExternalContract();
                case "version": return GetVersion();
                default: return false;
            }
        }

        private static string GetVersion()
        {
            //TODO : Invoke external contract to get version 
            Runtime.Log("Not implemented.");

            return "Not implemented.";
        }

        private static bool CallExternalContract()
        {
            //TODO : Invoke external contract
            Runtime.Log("Not implemented.");

            Runtime.Log("External contract invoked.");
            return true;
        }

        private static bool UpdateAddress(string newAddress, byte[] signature)
        {
            //Verify
            bool verified = VerifySignature(Owner, signature);
            if (!verified) return false;

            //Update external contract address
            Storage.Delete(Storage.CurrentContext, ContactAddressKey);
            Storage.Put(Storage.CurrentContext, ContactAddressKey, newAddress);

            Runtime.Notify("External contract address update.", newAddress);
            return true;
        }
    }
}
