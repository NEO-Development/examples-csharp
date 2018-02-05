using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace Neo.SmartContract
{
    public class HelloWorld : Framework.SmartContract
    {
        private static byte[] MultiKey(params byte[][] keys)
        {
            string temp = keys[0].AsString();

            for (int i = 1; i < keys.Length; i++)
            {
                temp = temp + "_";
                temp = temp + keys[i];
            }

            return temp.AsByteArray();
        }

        public static void Main()
        {
            string sampel = "testAddress_test_test_test_test_test";

            var list = sampel.Substring(12, sampel.Length);
            for (int i = 0; i < list.Length - 4; )
            {
                Runtime.Notify(list.Substring(i, 3));
                list = list.Substring(4, list.Length);
            }

            byte[] key1 = "key1".AsByteArray();
            byte[] key2 = "key2".AsByteArray();
            var key = MultiKey(key1, key2);

            Storage.Put(Storage.CurrentContext, key, "new value");
            var value = Storage.Get(Storage.CurrentContext, MultiKey(key1, key2));
            
            Runtime.Notify(value);
        }
    }
}
