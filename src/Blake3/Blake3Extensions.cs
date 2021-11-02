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
        /// Creates a span from a hash. The span returned has to follow the same lifetime than the hash referenced.
        /// </summary>
        /// <param name="hash">The hash to create a span from.</param>
        /// <returns>The hash of the span</returns>
        /// <remarks>This method is unsafe because you could return a Span from a local variable Hash that could be no longer valid on the stack.
        /// Use this Span with the same variable scope of the original Hash.
        /// It is safe to use this method if the referenced Hash is a field of a managed type.
        /// </remarks>
        public static Span<byte> AsSpanUnsafe(ref this Hash hash)
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