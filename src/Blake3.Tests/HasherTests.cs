using System;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Blake3.Tests
{
    /// <summary>
    /// Tests for <see cref="Hasher"/>
    /// </summary>
    public class HasherTests : Blake3TestsBase
    {
        private const string SimpleInput = "BLAKE3";
        public const string SimpleExpected = "f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4";
        public const string SimpleKeyedExpected = "52a1c5369af0590e26ccbb31d052485addcfe2599e858711579fb25aa878c6b8";
        public const string SimpleDeriveKeyExpected = "aed725e67e41969964e90fc83f44e17efab90f159a375d3bd213714df2db5ea4";
        public const string BigExpected = "64479cf7293960210547db8d982359e0c4ce054525ed7086cf93030828fc0533";
        public static readonly byte[] SimpleData = Encoding.UTF8.GetBytes(SimpleInput);
        public static readonly byte[] BigData = Enumerable.Range(0, 1024 * 1024).Select(x => (byte) x).ToArray();

        [Test]
        public void TestHashSimple()
        {
            AssertTextAreEqual(SimpleExpected, Hasher.Hash(SimpleData).ToString());
        }

        [Test]
        public void TestHashBig()
        {
            AssertTextAreEqual(BigExpected, Hasher.Hash(BigData).ToString());
        }

        [Test]
        public void TestUpdateSimple()
        {
            using var hasher = Hasher.New();
            hasher.Update(SimpleData);
            var hash = hasher.Finalize();
            AssertTextAreEqual(SimpleExpected, hash.ToString());
        }

        [Test]
        public void TestUpdateSimpleKeyed()
        {
            using var hasher = Hasher.NewKeyed(new ReadOnlySpan<byte>(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray()));
            hasher.Update(SimpleData);
            var hash = hasher.Finalize();
            AssertTextAreEqual(SimpleKeyedExpected, hash.ToString());
        }

        [Test]
        public void TestUpdateSimpleDeriveKey()
        {
            using var hasher = Hasher.NewDeriveKey(new ReadOnlySpan<byte>(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray()));
            hasher.Update(SimpleData);
            var hash = hasher.Finalize();
            AssertTextAreEqual(SimpleDeriveKeyExpected, hash.ToString());
        }

        [Test]
        public void TestUpdateBig()
        {
            using var hasher = Hasher.New();
            hasher.Update(BigData);
            var hash = hasher.Finalize();
            AssertTextAreEqual(BigExpected, hash.ToString());
        }

        [Test]
        public void TestUpdateJoinBig()
        {
            using var hasher = Hasher.New();
            hasher.UpdateWithJoin(BigData);
            var hash = hasher.Finalize();
            AssertTextAreEqual(BigExpected, hash.ToString());
        }
    }
}