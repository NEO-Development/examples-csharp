using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace VersionedContract
{
    public class VersionedContract : SmartContract
    {
        private static string IsActivatedKey { get { return "IsActivatedKey"; } }

        public static object Main(string operation, byte[] signature, params object[] args)
        {
            switch (operation)
            {
                case "Invoke":
                    return Invoke((int)args[0]);
                case "register":
                    return ChangeStatus((bool)args[0], signature);
                default:
                    return false;
            }
        }

        private static bool Invoke(int value)
        {
            var isActivated = Storage.Get(Storage.CurrentContext, IsActivatedKey);
            if (isActivated.AsString() == "false") return false;

            //TODO : Invoke logic
            throw new NotImplementedException();
        }

        private static bool ChangeStatus(bool activated, byte[] signature)
        {
            //TODO : Update byte array for your key
            bool verified = VerifySignature(new byte[] { 3, 26, 108, 111, 187, 223, 2, 202, 53, 23, 69, 250, 134, 185, 186, 90, 148, 82, 215, 133, 172, 79, 127, 194, 183, 84, 140, 162, 164, 108, 79, 207, 74 }, signature);
            if (!verified) return false;

            string IsActivated = "false";
            if (activated) IsActivated = "true";

            Storage.Put(Storage.CurrentContext, IsActivatedKey, IsActivated);
            Runtime.Log("IsActivated : " + IsActivated);

            return true;
        }
    }
}
