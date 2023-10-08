using System.Linq;
using NUnit.Framework;

namespace Blake3.Tests
{
    /// <summary>
    /// Tests for <see cref="Hasher"/>
    /// </summary>
    public class HashTests : Blake3TestsBase
    {
        [Test]
        public unsafe void TestSize()
        {
            Assert.AreEqual(32, sizeof(Hash));
        }

        [Test]
        public void TestToString()
        {
            AssertTextAreEqual(new string('0', 64), new Hash().ToString());
        }

        [Test]
        public void TestFromBytes()
        {
            AssertTextAreEqual("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", Hash.FromBytes(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray()).ToString());
        }

        [Test]
        public void TestEqual()
        {
            var hash1 = Hash.FromBytes(Enumerable.Range(0, 32).Select(x => (byte) x).ToArray());
            var hash2 = Hash.FromBytes(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray());
            Assert.AreEqual(hash1, hash2);

            var hash3 = Hash.FromBytes(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray());
            Assert.AreNotEqual(hash1, hash3);
        }

        [Test]
        public unsafe void TestHashCode()
        {
            var hash1 = Hash.FromBytes(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray());
            Assert.AreEqual(-1229248544, hash1.GetHashCode());
            Assert.AreEqual(0, new Hash().GetHashCode());
        }
    }
}