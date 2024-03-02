// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Security.Cryptography;

namespace Blake3;

/// <summary>
/// Implementation of <see cref="HashAlgorithm"/> for BLAKE3.
/// </summary>
public class Blake3HashAlgorithm : HashAlgorithm
{
    private Hasher _hasher;

    public Blake3HashAlgorithm()
    {
        _hasher = Hasher.New();
    }
        
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _hasher.Dispose();
    }
        
    protected override void HashCore(byte[] array, int ibStart, int cbSize)
    {
        _hasher.Update(new ReadOnlySpan<byte>(array, ibStart, cbSize));
    }

    protected override byte[] HashFinal()
    {
        var hash = new byte[Blake3.Hash.Size];
        _hasher.Finalize(hash);
        return hash;
    }

    public override void Initialize()
    {
        _hasher.Reset();
    }
}