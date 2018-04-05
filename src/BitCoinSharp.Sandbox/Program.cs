using BitCoinSharp.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitCoinSharp.Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            NetworkParameters prod = NetworkParameters.TestNet();
            EcKey key = new EcKey();
            byte[] hashPubKey = Utils.Sha256Hash160(key.PubKey);
            Address addr = new Address(prod, hashPubKey);

            var tx2 = TestUtils.CreateFakeTx(prod, Utils.ToNanoCoins(1, 0),
                                 new EcKey().ToAddress(prod));
        }
    }
}
