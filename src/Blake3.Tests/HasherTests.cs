using System;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Blake3.Tests
{
    /// <summary>
    /// Tests for <see cref="Hasher"/>
    /// </summary>
    public class Tests
    {
        [Test]
        public void HashSimpleString()
        {
            var data = Encoding.UTF8.GetBytes("abcd");
            using var hasher = Hasher.New();
            hasher.Update(data);
            var hash = hasher.Finalize();

            var expected = "8c9c9881805d1a847102d7a42e58b990d088dd88a84f7314d71c838107571f2b";

            AssertEqual(expected, hash.ToString());
            AssertEqual(expected, Hasher.HashData(data).ToString());
        }

        private static void AssertEqual(string expected, string result)
        {
            if (expected != result)
            {
                Console.WriteLine($"Expected: {expected}");
                Console.WriteLine($"  Result: {result}");
            }
            Assert.AreEqual(expected, result);
        }
    }
}