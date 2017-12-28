using Neo.SmartContract.Framework.Services.Neo;

namespace Neo.SmartContract
{
    public class Lock : Framework.SmartContract
    {
        public static bool Main(byte[] signature)
        {
            Header header = Blockchain.GetHeader(Blockchain.GetHeight());

            var limitTime = 1514383200;

            //string limitTimeStr = limitTime.ToString();
            //string currnt = header.Timestamp.ToString();

            int a = (int)header.Timestamp;
            //string currnt = a.ToString();

            Runtime.Log("Current : ");
            //Runtime.Log(header.Timestamp.ToString());
            Runtime.Log("Limit time : ");
            Runtime.Log("1514383200");

            if (header.Timestamp < limitTime)
            {
                Runtime.Notify("Rejected");
                return false;
            }

            Runtime.Notify("Transfered");
            
            return VerifySignature(new byte[] { 3, 26, 108, 111, 187, 223, 2, 202, 53, 23, 69, 250, 134, 185, 186, 90, 148, 82, 215, 133, 172, 79, 127, 194, 183, 84, 140, 162, 164, 108, 79, 207, 74 }, signature);
        }
    }
}
