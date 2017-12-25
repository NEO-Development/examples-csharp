using Neo.SmartContract.Framework.Services.Neo;

namespace Neo.SmartContract
{
    public class Lock : Framework.SmartContract
    {
        public static bool Main(uint timestamp)
        {
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            if (timestamp > header.Timestamp) return false;
            return true; //VerifySignature(signature, pubkey);
        }
    }
}
