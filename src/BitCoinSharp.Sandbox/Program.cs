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
            NetworkParameters par = new NetworkParameters();            
            EcKey key = new EcKey();
            byte[] pBytes = new byte[1];
            int a = 7;
            pBytes[0] = (byte)a;
            Transaction t = new Transaction(par, pBytes, 1);
            Console.ReadLine();
        }
    }
}
