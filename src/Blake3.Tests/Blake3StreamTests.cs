using System.IO;
using NUnit.Framework;

namespace Blake3.Tests
{
    /// <summary>
    /// Tests for <see cref="Hasher"/>
    /// </summary>
    public class Blake3StreamTests : Blake3TestsBase
    {
        [Test]
        public void TestHashRead()
        {
            var stream = new MemoryStream(HasherTests.BigData);
            using var blake3Stream = new Blake3Stream(stream);
            blake3Stream.Read(new byte[HasherTests.BigData.Length]);
            AssertTextAreEqual(HasherTests.BigExpected, blake3Stream.ComputeHash().ToString());
        }

        [Test]
        public void TestHashWrite()
        {
            var stream = new MemoryStream();
            using var blake3Stream = new Blake3Stream(stream);
            blake3Stream.Write(HasherTests.BigData);
            AssertTextAreEqual(HasherTests.BigExpected, blake3Stream.ComputeHash().ToString());
        }
    }
}