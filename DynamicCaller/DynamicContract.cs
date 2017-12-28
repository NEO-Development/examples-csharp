using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace DynamicCaller
{
    public class DynamicContract : SmartContract
    {
        private static string ContactAddressKey = "ContactAddressKey";

        public static object Main(string operation, byte[] signature, params object[] args)
        {
            switch (operation)
            {
                case "updateAddress":
                    return UpdateAddress((string)args[0], signature);
                case "register":
                    return CallExternalContract((int)args[0]);
                default:
                    return false;
            }
        }

        private static string CallExternalContract(int value)
        {
            //TODO : Invoke external contract

            throw new NotImplementedException();
        }

        private static bool UpdateAddress(string newAddress, byte[] signature)
        {
            //TODO : Update byte array for your key
            bool verified = VerifySignature(new byte[] { 3, 26, 108, 111, 187, 223, 2, 202, 53, 23, 69, 250, 134, 185, 186, 90, 148, 82, 215, 133, 172, 79, 127, 194, 183, 84, 140, 162, 164, 108, 79, 207, 74 }, signature);
            if (!verified) return false;

            Storage.Delete(Storage.CurrentContext, ContactAddressKey);
            Storage.Put(Storage.CurrentContext, ContactAddressKey, newAddress);
            
            return true;
        }
    }
}
