using System;
using System.Collections.Generic;
using System.Linq;
using BitCoinSharp.Test;

namespace BitCoinSharp.Examples
{
    public static class Program
    {

        public static void Main(string[] args)
        {
            NetworkParameters _params = NetworkParameters.TestNet();
            var myKey = new EcKey();
            byte[] toAddr = myKey.ToAddress(_params).Hash160;
            var xx = TestUtils.CreateFakeTx(_params, 100, new Address(_params, toAddr));
        }

        public static void Main2(string[] args)
        {
            //https://code.google.com/archive/p/bitcoinsharp/
            //hardcoding args
            args = new string[] { "FetchBlock" };

            if (args == null || args.Length == 0)
            {
                Console.WriteLine("BitCoinSharp.Examples <name> <args>");
                return;
            }

            var examples = new Dictionary<string, Action<string[]>>(StringComparer.InvariantCultureIgnoreCase)
                           {
                               {"DumpWallet", DumpWallet.Run},
                               {"FetchBlock", FetchBlock.Run},
                               {"PingService", PingService.Run},
                               {"PrintPeers", PrintPeers.Run},
                               {"PrivateKeys", PrivateKeys.Run},
                               {"RefreshWallet", RefreshWallet.Run}
                           };

            var name = args[0];
            Action<string[]> run;
            if (!examples.TryGetValue(name, out run))
            {
                Console.WriteLine("Example '{0}' not found", name);
                return;
            }

            run(args.Skip(1).ToArray());
        }
    }
}