// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using NUnit.Framework;

namespace Blake3.Tests
{
    /// <summary>
    /// Tests for <see cref="Blake3HashAlgorithm"/>
    /// </summary>
    public class Blake3HashAlgorithmTests : Blake3TestsBase
    {
        [Test]
        public void TestComputeHash()
        {
            using var hashAlgorithm = new Blake3HashAlgorithm();
            var result = hashAlgorithm.ComputeHash(HasherTests.BigData);
            var hash = Hash.FromBytes(result);
            AssertTextAreEqual(HasherTests.BigExpected, hash.ToString());
        }

        [Test]
        public void TestComputeHash_Span_And_TryHashFinal()
        {
            using var hashAlgorithm = new Blake3HashAlgorithm();

            var data = HasherTests.BigData;

            Span<byte> destination = stackalloc byte[Hash.Size];
            Assert.True(hashAlgorithm.TryComputeHash(data, destination, out var written));
            Assert.AreEqual(Hash.Size, written);

            var hash = Hash.FromBytes(destination);
            AssertTextAreEqual(HasherTests.BigExpected, hash.ToString());
        }

        [Test]
        public void TestTryHashFinal_BufferTooSmall()
        {
            using var hashAlgorithm = new Blake3HashAlgorithm();

            Span<byte> destination = stackalloc byte[Hash.Size - 1];
            Assert.False(hashAlgorithm.TryComputeHash(HasherTests.BigData, destination, out var written));
            Assert.AreEqual(0, written);
        }

        [Test]
        public void TestChunking_And_InitializeReuse()
        {
            var data = HasherTests.BigData;

            using var hashAlgorithm = new Blake3HashAlgorithm();

            // Chunked updates
            hashAlgorithm.TransformBlock(data, 0, 7, null, 0);
            hashAlgorithm.TransformBlock(data, 7, 13, null, 0);
            hashAlgorithm.TransformBlock(data, 20, data.Length - 20, null, 0);
            hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            var hash1 = Hash.FromBytes(hashAlgorithm.Hash);
            AssertTextAreEqual(HasherTests.BigExpected, hash1.ToString());

            // Reuse
            hashAlgorithm.Initialize();
            var result = hashAlgorithm.ComputeHash(data);
            var hash2 = Hash.FromBytes(result);
            AssertTextAreEqual(HasherTests.BigExpected, hash2.ToString());
        }
    }
}