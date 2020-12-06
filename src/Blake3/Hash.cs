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
                var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref hash, 1));
                for (int i = 0; i < Size; i++)
                {
                    var b = data[i];
                    span[i * 2] = Hex[(b >> 4) & 0xF];
                    span[i * 2 + 1] = Hex[b & 0xF];
                }
            });
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

        private static ReadOnlySpan<char> Hex => new ReadOnlySpan<char>(new char[]
        {
            '0',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9',
            'a',
            'b',
            'c',
            'd',
            'e',
            'f',
        });
    }
}