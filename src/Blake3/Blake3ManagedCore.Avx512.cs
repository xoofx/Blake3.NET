// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Blake3;

internal static partial class Blake3ManagedCore
{    /// <summary>
    /// Hashes sixteen complete, contiguous chunks in parallel when 512-bit vectors are available.
    /// The output is lane-major: eight chaining-value words for each input chunk.
    /// </summary>
    [SkipLocalsInit]
    internal static bool TryHash16Chunks(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        const int Degree = 16;
        if (!Avx512F.IsSupported || input.Length < Degree * ChunkLength || output.Length < Degree * 8)
        {
            return false;
        }

        var inputWords = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(input[..(Degree * ChunkLength)]);
        Span<Vector512<uint>> chainingValues = stackalloc Vector512<uint>[8];
        Span<Vector512<uint>> message = stackalloc Vector512<uint>[16];
        ref var chainingValue = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(chainingValues);
        ref var messageWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(message);
        ref var inputWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(inputWords);
        for (var index = 0; index < chainingValues.Length; index++)
        {
            chainingValues[index] = Vector512.Create(keyWords[index]);
        }

        var counterLow = Vector512.Create((uint)chunkCounter) +
            Vector512.Create(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u, 9u, 10u, 11u, 12u, 13u, 14u, 15u);
        var counterHigh = Vector512.Create((uint)(chunkCounter >> 32));
        if ((uint)chunkCounter > uint.MaxValue - 15)
        {
            counterHigh = Vector512.Create(
                (uint)(chunkCounter >> 32), (uint)((chunkCounter + 1) >> 32),
                (uint)((chunkCounter + 2) >> 32), (uint)((chunkCounter + 3) >> 32),
                (uint)((chunkCounter + 4) >> 32), (uint)((chunkCounter + 5) >> 32),
                (uint)((chunkCounter + 6) >> 32), (uint)((chunkCounter + 7) >> 32),
                (uint)((chunkCounter + 8) >> 32), (uint)((chunkCounter + 9) >> 32),
                (uint)((chunkCounter + 10) >> 32), (uint)((chunkCounter + 11) >> 32),
                (uint)((chunkCounter + 12) >> 32), (uint)((chunkCounter + 13) >> 32),
                (uint)((chunkCounter + 14) >> 32), (uint)((chunkCounter + 15) >> 32));
        }

        for (var block = 0; block < ChunkLength / BlockLength; block++)
        {
            var wordOffset = block * 16;
            messageWord = Vector512.LoadUnsafe(ref inputWord, (nuint)wordOffset);
            Unsafe.Add(ref messageWord, 1) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 256));
            Unsafe.Add(ref messageWord, 2) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 512));
            Unsafe.Add(ref messageWord, 3) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 768));
            Unsafe.Add(ref messageWord, 4) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 1024));
            Unsafe.Add(ref messageWord, 5) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 1280));
            Unsafe.Add(ref messageWord, 6) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 1536));
            Unsafe.Add(ref messageWord, 7) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 1792));
            Unsafe.Add(ref messageWord, 8) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 2048));
            Unsafe.Add(ref messageWord, 9) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 2304));
            Unsafe.Add(ref messageWord, 10) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 2560));
            Unsafe.Add(ref messageWord, 11) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 2816));
            Unsafe.Add(ref messageWord, 12) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 3072));
            Unsafe.Add(ref messageWord, 13) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 3328));
            Unsafe.Add(ref messageWord, 14) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 3584));
            Unsafe.Add(ref messageWord, 15) = Vector512.LoadUnsafe(ref inputWord, (nuint)(wordOffset + 3840));

            Transpose16(ref messageWord);

            var blockFlags = flags;
            if (block == 0)
            {
                blockFlags |= ChunkStart;
            }

            if (block == (ChunkLength / BlockLength) - 1)
            {
                blockFlags |= ChunkEnd;
            }

            Compress16(
                ref chainingValue,
                ref messageWord,
                in counterLow,
                in counterHigh,
                blockFlags);
        }

        messageWord = chainingValue;
        Unsafe.Add(ref messageWord, 1) = Unsafe.Add(ref chainingValue, 1);
        Unsafe.Add(ref messageWord, 2) = Unsafe.Add(ref chainingValue, 2);
        Unsafe.Add(ref messageWord, 3) = Unsafe.Add(ref chainingValue, 3);
        Unsafe.Add(ref messageWord, 4) = Unsafe.Add(ref chainingValue, 4);
        Unsafe.Add(ref messageWord, 5) = Unsafe.Add(ref chainingValue, 5);
        Unsafe.Add(ref messageWord, 6) = Unsafe.Add(ref chainingValue, 6);
        Unsafe.Add(ref messageWord, 7) = Unsafe.Add(ref chainingValue, 7);
        message[8..].Clear();
        Transpose16(ref messageWord);

        ref var outputWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(output);
        messageWord.GetLower().StoreUnsafe(ref outputWord);
        Unsafe.Add(ref messageWord, 1).GetLower().StoreUnsafe(ref outputWord, 8);
        Unsafe.Add(ref messageWord, 2).GetLower().StoreUnsafe(ref outputWord, 16);
        Unsafe.Add(ref messageWord, 3).GetLower().StoreUnsafe(ref outputWord, 24);
        Unsafe.Add(ref messageWord, 4).GetLower().StoreUnsafe(ref outputWord, 32);
        Unsafe.Add(ref messageWord, 5).GetLower().StoreUnsafe(ref outputWord, 40);
        Unsafe.Add(ref messageWord, 6).GetLower().StoreUnsafe(ref outputWord, 48);
        Unsafe.Add(ref messageWord, 7).GetLower().StoreUnsafe(ref outputWord, 56);
        Unsafe.Add(ref messageWord, 8).GetLower().StoreUnsafe(ref outputWord, 64);
        Unsafe.Add(ref messageWord, 9).GetLower().StoreUnsafe(ref outputWord, 72);
        Unsafe.Add(ref messageWord, 10).GetLower().StoreUnsafe(ref outputWord, 80);
        Unsafe.Add(ref messageWord, 11).GetLower().StoreUnsafe(ref outputWord, 88);
        Unsafe.Add(ref messageWord, 12).GetLower().StoreUnsafe(ref outputWord, 96);
        Unsafe.Add(ref messageWord, 13).GetLower().StoreUnsafe(ref outputWord, 104);
        Unsafe.Add(ref messageWord, 14).GetLower().StoreUnsafe(ref outputWord, 112);
        Unsafe.Add(ref messageWord, 15).GetLower().StoreUnsafe(ref outputWord, 120);

        return true;
    }


    [SkipLocalsInit]
    private static bool TryCompress16Parents(
        Span<uint> childChainingValues,
        int parentCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        const int Degree = 16;
        if (!Avx512F.IsSupported || parentCount is < 1 or > Degree)
        {
            return false;
        }

        Span<Vector512<uint>> chainingValues = stackalloc Vector512<uint>[8];
        Span<Vector512<uint>> message = stackalloc Vector512<uint>[16];
        ref var chainingValue = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(chainingValues);
        ref var messageWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(message);
        ref var childWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(childChainingValues);
        for (var word = 0; word < chainingValues.Length; word++)
        {
            chainingValues[word] = Vector512.Create(keyWords[word]);
        }

        for (var parent = 0; parent < parentCount; parent++)
        {
            Unsafe.Add(ref messageWord, parent) =
                Vector512.LoadUnsafe(ref childWord, (nuint)(parent * 16));
        }

        message[parentCount..].Clear();
        Transpose16(ref messageWord);
        var zero = Vector512<uint>.Zero;
        Compress16(ref chainingValue, ref messageWord, in zero, in zero, flags | Parent);

        messageWord = chainingValue;
        Unsafe.Add(ref messageWord, 1) = Unsafe.Add(ref chainingValue, 1);
        Unsafe.Add(ref messageWord, 2) = Unsafe.Add(ref chainingValue, 2);
        Unsafe.Add(ref messageWord, 3) = Unsafe.Add(ref chainingValue, 3);
        Unsafe.Add(ref messageWord, 4) = Unsafe.Add(ref chainingValue, 4);
        Unsafe.Add(ref messageWord, 5) = Unsafe.Add(ref chainingValue, 5);
        Unsafe.Add(ref messageWord, 6) = Unsafe.Add(ref chainingValue, 6);
        Unsafe.Add(ref messageWord, 7) = Unsafe.Add(ref chainingValue, 7);
        message[8..].Clear();
        Transpose16(ref messageWord);

        ref var outputWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(childChainingValues);
        for (var parent = 0; parent < parentCount; parent++)
        {
            Unsafe.Add(ref messageWord, parent).GetLower()
                .StoreUnsafe(ref outputWord, (nuint)(parent * 8));
        }

        return true;
    }

    [SkipLocalsInit]
    private static void Compress16(
        ref Vector512<uint> chainingValue,
        ref Vector512<uint> messageWord,
        in Vector512<uint> counterLow,
        in Vector512<uint> counterHigh,
        uint flags)
    {
        var v0 = chainingValue;
        var v1 = Unsafe.Add(ref chainingValue, 1);
        var v2 = Unsafe.Add(ref chainingValue, 2);
        var v3 = Unsafe.Add(ref chainingValue, 3);
        var v4 = Unsafe.Add(ref chainingValue, 4);
        var v5 = Unsafe.Add(ref chainingValue, 5);
        var v6 = Unsafe.Add(ref chainingValue, 6);
        var v7 = Unsafe.Add(ref chainingValue, 7);
        var v8 = Vector512.Create(0x6A09E667u);
        var v9 = Vector512.Create(0xBB67AE85u);
        var v10 = Vector512.Create(0x3C6EF372u);
        var v11 = Vector512.Create(0xA54FF53Au);
        var v12 = counterLow;
        var v13 = counterHigh;
        var v14 = Vector512.Create((uint)BlockLength);
        var v15 = Vector512.Create(flags);

        // Fully unroll the fixed seven-round schedule. Apart from avoiding the loop itself, using
        // ref offsets here lets the JIT emit direct message loads without schedule or bounds checks.
        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 0, 1);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 2, 3);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 4, 5);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 6, 7);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 8, 9);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 10, 11);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 12, 13);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 14, 15);

        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 2, 6);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 3, 10);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 7, 0);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 4, 13);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 1, 11);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 12, 5);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 9, 14);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 15, 8);

        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 3, 4);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 10, 12);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 13, 2);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 7, 14);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 6, 5);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 9, 0);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 11, 15);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 8, 1);

        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 10, 7);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 12, 9);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 14, 3);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 13, 15);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 4, 0);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 11, 2);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 5, 8);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 1, 6);

        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 12, 13);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 9, 11);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 15, 10);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 14, 8);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 7, 2);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 5, 3);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 0, 1);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 6, 4);

        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 9, 14);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 11, 5);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 8, 12);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 15, 1);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 13, 3);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 0, 10);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 2, 6);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 4, 7);

        Mix(ref v0, ref v4, ref v8, ref v12, ref messageWord, 11, 15);
        Mix(ref v1, ref v5, ref v9, ref v13, ref messageWord, 5, 0);
        Mix(ref v2, ref v6, ref v10, ref v14, ref messageWord, 1, 9);
        Mix(ref v3, ref v7, ref v11, ref v15, ref messageWord, 8, 6);
        Mix(ref v0, ref v5, ref v10, ref v15, ref messageWord, 14, 10);
        Mix(ref v1, ref v6, ref v11, ref v12, ref messageWord, 2, 12);
        Mix(ref v2, ref v7, ref v8, ref v13, ref messageWord, 3, 4);
        Mix(ref v3, ref v4, ref v9, ref v14, ref messageWord, 7, 13);

        chainingValue = v0 ^ v8;
        Unsafe.Add(ref chainingValue, 1) = v1 ^ v9;
        Unsafe.Add(ref chainingValue, 2) = v2 ^ v10;
        Unsafe.Add(ref chainingValue, 3) = v3 ^ v11;
        Unsafe.Add(ref chainingValue, 4) = v4 ^ v12;
        Unsafe.Add(ref chainingValue, 5) = v5 ^ v13;
        Unsafe.Add(ref chainingValue, 6) = v6 ^ v14;
        Unsafe.Add(ref chainingValue, 7) = v7 ^ v15;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(
        ref Vector512<uint> a,
        ref Vector512<uint> b,
        ref Vector512<uint> c,
        ref Vector512<uint> d,
        ref Vector512<uint> message,
        int messageX,
        int messageY)
    {
        a = a + b + Unsafe.Add(ref message, messageX);
        d = RotateRight16(d ^ a);
        c += d;
        b = RotateRight12(b ^ c);
        a = a + b + Unsafe.Add(ref message, messageY);
        d = RotateRight8(d ^ a);
        c += d;
        b = RotateRight7(b ^ c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> RotateRight16(Vector512<uint> value) => Avx512F.RotateRight(value, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> RotateRight12(Vector512<uint> value) => Avx512F.RotateRight(value, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> RotateRight8(Vector512<uint> value) => Avx512F.RotateRight(value, 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> RotateRight7(Vector512<uint> value) => Avx512F.RotateRight(value, 7);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Transpose16(ref Vector512<uint> first)
    {
        var v0 = first;
        var v1 = Unsafe.Add(ref first, 1);
        var v2 = Unsafe.Add(ref first, 2);
        var v3 = Unsafe.Add(ref first, 3);
        var v4 = Unsafe.Add(ref first, 4);
        var v5 = Unsafe.Add(ref first, 5);
        var v6 = Unsafe.Add(ref first, 6);
        var v7 = Unsafe.Add(ref first, 7);
        var v8 = Unsafe.Add(ref first, 8);
        var v9 = Unsafe.Add(ref first, 9);
        var v10 = Unsafe.Add(ref first, 10);
        var v11 = Unsafe.Add(ref first, 11);
        var v12 = Unsafe.Add(ref first, 12);
        var v13 = Unsafe.Add(ref first, 13);
        var v14 = Unsafe.Add(ref first, 14);
        var v15 = Unsafe.Add(ref first, 15);

        var ab0 = Avx512F.UnpackLow(v0, v1);
        var ab2 = Avx512F.UnpackHigh(v0, v1);
        var cd0 = Avx512F.UnpackLow(v2, v3);
        var cd2 = Avx512F.UnpackHigh(v2, v3);
        var ef0 = Avx512F.UnpackLow(v4, v5);
        var ef2 = Avx512F.UnpackHigh(v4, v5);
        var gh0 = Avx512F.UnpackLow(v6, v7);
        var gh2 = Avx512F.UnpackHigh(v6, v7);
        var ij0 = Avx512F.UnpackLow(v8, v9);
        var ij2 = Avx512F.UnpackHigh(v8, v9);
        var kl0 = Avx512F.UnpackLow(v10, v11);
        var kl2 = Avx512F.UnpackHigh(v10, v11);
        var mn0 = Avx512F.UnpackLow(v12, v13);
        var mn2 = Avx512F.UnpackHigh(v12, v13);
        var op0 = Avx512F.UnpackLow(v14, v15);
        var op2 = Avx512F.UnpackHigh(v14, v15);

        var abcd0 = Avx512F.UnpackLow(ab0.AsUInt64(), cd0.AsUInt64()).AsUInt32();
        var abcd1 = Avx512F.UnpackHigh(ab0.AsUInt64(), cd0.AsUInt64()).AsUInt32();
        var abcd2 = Avx512F.UnpackLow(ab2.AsUInt64(), cd2.AsUInt64()).AsUInt32();
        var abcd3 = Avx512F.UnpackHigh(ab2.AsUInt64(), cd2.AsUInt64()).AsUInt32();
        var efgh0 = Avx512F.UnpackLow(ef0.AsUInt64(), gh0.AsUInt64()).AsUInt32();
        var efgh1 = Avx512F.UnpackHigh(ef0.AsUInt64(), gh0.AsUInt64()).AsUInt32();
        var efgh2 = Avx512F.UnpackLow(ef2.AsUInt64(), gh2.AsUInt64()).AsUInt32();
        var efgh3 = Avx512F.UnpackHigh(ef2.AsUInt64(), gh2.AsUInt64()).AsUInt32();
        var ijkl0 = Avx512F.UnpackLow(ij0.AsUInt64(), kl0.AsUInt64()).AsUInt32();
        var ijkl1 = Avx512F.UnpackHigh(ij0.AsUInt64(), kl0.AsUInt64()).AsUInt32();
        var ijkl2 = Avx512F.UnpackLow(ij2.AsUInt64(), kl2.AsUInt64()).AsUInt32();
        var ijkl3 = Avx512F.UnpackHigh(ij2.AsUInt64(), kl2.AsUInt64()).AsUInt32();
        var mnop0 = Avx512F.UnpackLow(mn0.AsUInt64(), op0.AsUInt64()).AsUInt32();
        var mnop1 = Avx512F.UnpackHigh(mn0.AsUInt64(), op0.AsUInt64()).AsUInt32();
        var mnop2 = Avx512F.UnpackLow(mn2.AsUInt64(), op2.AsUInt64()).AsUInt32();
        var mnop3 = Avx512F.UnpackHigh(mn2.AsUInt64(), op2.AsUInt64()).AsUInt32();

        var abcdefgh0 = Avx512F.Shuffle4x128(abcd0, efgh0, 0x88);
        var abcdefgh1 = Avx512F.Shuffle4x128(abcd1, efgh1, 0x88);
        var abcdefgh2 = Avx512F.Shuffle4x128(abcd2, efgh2, 0x88);
        var abcdefgh3 = Avx512F.Shuffle4x128(abcd3, efgh3, 0x88);
        var abcdefgh4 = Avx512F.Shuffle4x128(abcd0, efgh0, 0xDD);
        var abcdefgh5 = Avx512F.Shuffle4x128(abcd1, efgh1, 0xDD);
        var abcdefgh6 = Avx512F.Shuffle4x128(abcd2, efgh2, 0xDD);
        var abcdefgh7 = Avx512F.Shuffle4x128(abcd3, efgh3, 0xDD);
        var ijklmnop0 = Avx512F.Shuffle4x128(ijkl0, mnop0, 0x88);
        var ijklmnop1 = Avx512F.Shuffle4x128(ijkl1, mnop1, 0x88);
        var ijklmnop2 = Avx512F.Shuffle4x128(ijkl2, mnop2, 0x88);
        var ijklmnop3 = Avx512F.Shuffle4x128(ijkl3, mnop3, 0x88);
        var ijklmnop4 = Avx512F.Shuffle4x128(ijkl0, mnop0, 0xDD);
        var ijklmnop5 = Avx512F.Shuffle4x128(ijkl1, mnop1, 0xDD);
        var ijklmnop6 = Avx512F.Shuffle4x128(ijkl2, mnop2, 0xDD);
        var ijklmnop7 = Avx512F.Shuffle4x128(ijkl3, mnop3, 0xDD);

        first = Avx512F.Shuffle4x128(abcdefgh0, ijklmnop0, 0x88);
        Unsafe.Add(ref first, 1) = Avx512F.Shuffle4x128(abcdefgh1, ijklmnop1, 0x88);
        Unsafe.Add(ref first, 2) = Avx512F.Shuffle4x128(abcdefgh2, ijklmnop2, 0x88);
        Unsafe.Add(ref first, 3) = Avx512F.Shuffle4x128(abcdefgh3, ijklmnop3, 0x88);
        Unsafe.Add(ref first, 4) = Avx512F.Shuffle4x128(abcdefgh4, ijklmnop4, 0x88);
        Unsafe.Add(ref first, 5) = Avx512F.Shuffle4x128(abcdefgh5, ijklmnop5, 0x88);
        Unsafe.Add(ref first, 6) = Avx512F.Shuffle4x128(abcdefgh6, ijklmnop6, 0x88);
        Unsafe.Add(ref first, 7) = Avx512F.Shuffle4x128(abcdefgh7, ijklmnop7, 0x88);
        Unsafe.Add(ref first, 8) = Avx512F.Shuffle4x128(abcdefgh0, ijklmnop0, 0xDD);
        Unsafe.Add(ref first, 9) = Avx512F.Shuffle4x128(abcdefgh1, ijklmnop1, 0xDD);
        Unsafe.Add(ref first, 10) = Avx512F.Shuffle4x128(abcdefgh2, ijklmnop2, 0xDD);
        Unsafe.Add(ref first, 11) = Avx512F.Shuffle4x128(abcdefgh3, ijklmnop3, 0xDD);
        Unsafe.Add(ref first, 12) = Avx512F.Shuffle4x128(abcdefgh4, ijklmnop4, 0xDD);
        Unsafe.Add(ref first, 13) = Avx512F.Shuffle4x128(abcdefgh5, ijklmnop5, 0xDD);
        Unsafe.Add(ref first, 14) = Avx512F.Shuffle4x128(abcdefgh6, ijklmnop6, 0xDD);
        Unsafe.Add(ref first, 15) = Avx512F.Shuffle4x128(abcdefgh7, ijklmnop7, 0xDD);
    }

}
