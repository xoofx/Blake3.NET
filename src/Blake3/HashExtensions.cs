using System;
using System.Runtime.InteropServices;

namespace Blake3
{
    public static class HashExtensions
    {
        public static Span<byte> AsSpan(ref this Hash has)
        {
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref has, 1));
        }
    }
}