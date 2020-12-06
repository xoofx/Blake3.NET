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
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref hash, 1));
        }
    }
}