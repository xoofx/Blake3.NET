// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blake3
{
    /// <summary>
    /// An output of the default size, 32 bytes, which provides constant-time equality checking.
    /// </summary>
    /// <remarks>
    /// This hash is returned by <see cref="Hasher.Hash(System.ReadOnlySpan{byte})"/>.
    /// This hash struct provides structural equality.
    /// </remarks>
    public struct Hash : IEquatable<Hash>
    {
        /// <summary>
        /// The size of this hash is 32 bytes.
        /// </summary>
        public const int Size = 32;

        // Use explicit fields to avoid garbage at debugging time
#pragma warning disable 169
        private byte _byte1;
        private byte _byte2;
        private byte _byte3;
        private byte _byte4;
        private byte _byte5;
        private byte _byte6;
        private byte _byte7;
        private byte _byte8;
        private byte _byte9;
        private byte _byte10;
        private byte _byte11;
        private byte _byte12;
        private byte _byte13;
        private byte _byte14;
        private byte _byte15;
        private byte _byte16;
        private byte _byte17;
        private byte _byte18;
        private byte _byte19;
        private byte _byte20;
        private byte _byte21;
        private byte _byte22;
        private byte _byte23;
        private byte _byte24;
        private byte _byte25;
        private byte _byte26;
        private byte _byte27;
        private byte _byte28;
        private byte _byte29;
        private byte _byte30;
        private byte _byte31;
        private byte _byte32;
#pragma warning restore 169

        /// <summary>
        /// Copies bytes to this hash. The input data must be 32 bytes.
        /// </summary>
        /// <param name="data">A 32-byte buffer.</param>
        public void CopyFromBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length != 32) ThrowArgumentOutOfRange(data.Length);
            data.CopyTo(this.AsSpan());
        }

        /// <summary>
        /// Creates a hash from an input data that must be 32 bytes.
        /// </summary>
        /// <param name="data">A 32-byte buffer.</param>
        /// <returns>The 32-byte hash.</returns>
        [SkipLocalsInit]
        public static Hash FromBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length != 32) ThrowArgumentOutOfRange(data.Length);
            var hash = new Hash();
            hash.CopyFromBytes(data);
            return hash;
        }

        public bool Equals(Hash other)
        {
            return this.AsSpan().SequenceCompareTo(other.AsSpan()) == 0;
        }

        public override bool Equals(object obj)
        {
            return obj is Hash other && Equals(other);
        }

        public override int GetHashCode()
        {
            var values = MemoryMarshal.Cast<byte, int>(this.AsSpan());
            int hashcode = 0;
            for (int i = 0; i < values.Length; i++)
            {
                hashcode = (hashcode * 397) ^ values[i];
            }
            return hashcode;
        }

        public override string ToString()
        {
            return string.Create(Size * 2, this, (span, hash) =>
            {
                var data = hash.AsSpan();
                for (int i = 0; i < data.Length; i++)
                {
                    var b = data[i];
                    span[i * 2] = (char)Hex[(b >> 4) & 0xF];
                    span[i * 2 + 1] = (char)Hex[b & 0xF];
                }
            });
        }

        /// <summary>
        /// Creates a span from a hash. The span returned has to follow the same lifetime than the hash referenced.
        /// </summary>
        /// <returns>The hash of the span</returns>
        /// <remarks>This method is unsafe because you could return a Span from a local variable Hash that could be no longer valid on the stack.
        /// Use this Span with the same variable scope of the original Hash.
        /// It is safe to use this method if the referenced Hash is a field of a managed type.
        /// </remarks>
        [UnscopedRef]
        public Span<byte> AsSpan()
        {
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1));
        }

        public static bool operator ==(Hash left, Hash right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Hash left, Hash right)
        {
            return !left.Equals(right);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentOutOfRange(int size)
        {
            throw new ArgumentOutOfRangeException("data", $"Invalid size {size} of the data. Expecting 32");
        }

        private static ReadOnlySpan<byte> Hex => new ReadOnlySpan<byte>(new byte[]
        {
            (byte)'0',
            (byte)'1',
            (byte)'2',
            (byte)'3',
            (byte)'4',
            (byte)'5',
            (byte)'6',
            (byte)'7',
            (byte)'8',
            (byte)'9',
            (byte)'a',
            (byte)'b',
            (byte)'c',
            (byte)'d',
            (byte)'e',
            (byte)'f',
        });
    }
}