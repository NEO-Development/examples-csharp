using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace VersionedContract
{
    public class VersionedContract : SmartContract
    {
        //Magic string 
        private static string IsActivatedKey { get { return "IsActivatedKey"; } }

        private static string Version = "0.0.1";
        public static readonly byte[] Owner = { 47, 60, 170, 33, 216, 40, 148, 2, 242, 150, 9, 84, 154, 50, 237, 160, 97, 90, 55, 183 };

        public static object Main(string operation, byte[] signature, params object[] args)
        {
            switch (operation)
            {
                case "invoke": return Invoke();
                case "register": return ChangeStatus((bool)args[0], signature);
                case "version": return GetVersion();
                default: return false;
            }
        }

        private static string GetVersion()
        {
            return Version;
        }

        private static bool Invoke()
        {
            //Check if contract is active 
            var isActivated = Storage.Get(Storage.CurrentContext, IsActivatedKey);
            if (isActivated.AsString() == "false")
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
