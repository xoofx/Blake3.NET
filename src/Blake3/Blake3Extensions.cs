// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Blake3
{
    /// <summary>
    /// Extensions for Blake3 structs.
    /// </summary>
    public static class Blake3Extensions
    {
        /// <summary>
        /// Creates a span from a hash.
        /// </summary>
        /// <param name="hash">The hash to create a span from.</param>
        /// <returns>The hash of the span</returns>
        public static Span<byte> AsSpan(ref this Hash hash)
        {
#if NETSTANDARD2_0
            unsafe
            {
                fixed (void* pHash = &hash)
                    return new Span<byte>(pHash, sizeof(Hash));
            }
#else
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref hash, 1));
#endif
        }
    }
}