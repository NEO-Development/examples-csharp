using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;

namespace OperationsContract
{

    public class OperationsContract : SmartContract
    {
        public static class OperationTypes
        {
            public static string None = nameof(None);
            public static string Add = nameof(Add);
            public static string Read = nameof(Read);
        }

        
        public static void Main(string operation, string key, string value)
        {
            value = "123"; // 123.ToString();
            
            Runtime.Log("Operataion : " + operation);

            if (operation == "Add")
            {
                var message = key; message += " | "; message += value;
                Runtime.Log(message);

                Storage.Put(Storage.CurrentContext, key, value);
            }
            else if (operation == "Read")
            {
                byte[] bytes = Storage.Get(Storage.CurrentContext, key);
                string data = bytes.AsString();

                var message = key; message += " : "; message += data;
                Runtime.Log(message);
            }
            else
            {
                Runtime.Log(OperationTypes.None);
            }
        }
    }
}
