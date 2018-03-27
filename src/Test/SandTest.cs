using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Utilities.Encoders;

namespace BitCoinSharp.Test
{
    [TestFixture]
    public class SandTest
    {
        private static readonly NetworkParameters _params = NetworkParameters.TestNet();
        static SandTest()
        {

        }

        [Test]
        public void TestSandWork()
        {
            Assert.IsTrue(true);
        }
    }
}
