using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace ThirdWave
{
    public class ThirdWaveSC : SmartContract
    {
        public static void Main()
        {
        }

        private string Yasa1Name { get { return "Yasa1Name"; } }

        public bool Yasa1(string value1, string value2, string value3)
        {
            if (value1 + value2 == value3) return true;
            return false;
        }
    }
}
