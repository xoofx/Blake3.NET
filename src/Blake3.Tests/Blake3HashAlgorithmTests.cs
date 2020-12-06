// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using NUnit.Framework;

namespace Blake3.Tests
{
    /// <summary>
    /// Tests for <see cref="Hasher"/>
    /// </summary>
    public class Blake3HashAlgorithmTests : Blake3TestsBase
    {
        [Test]
        public void TestComputeHash()
        {
            var hashAlgorithm = new Blake3HashAlgorithm();
            var result = hashAlgorithm.ComputeHash(HasherTests.BigData);
            var hash = Hash.FromBytes(result);
            AssertTextAreEqual(HasherTests.BigExpected, hash.ToString());
        }
    }
}