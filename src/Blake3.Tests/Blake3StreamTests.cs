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
            _ = blake3Stream.Read(new byte[HasherTests.BigData.Length]);
            AssertTextAreEqual(HasherTests.BigExpected, blake3Stream.ComputeHash().ToString());
        }

        private static int FillBuffer(Stream stream, ref byte[] buffer)
        {
           int read;
           int totalRead = 0;
           do
           {
              read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
              totalRead += read;
           } while (read > 0 && totalRead < buffer.Length);

           return totalRead;
        }

        [Test]
        public void TestHashBufferedRead()
        {
           var stream = new MemoryStream(HasherTests.BigData);
           using var blake3Stream = new Blake3Stream(stream);

           const int bufferSize = 8 * 1024;
           var buffer = new byte[bufferSize];
           do
           {
              int bufferDataLength = FillBuffer(blake3Stream, ref buffer);
              if (bufferDataLength == 0)
                 break;
           } while (true);

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
