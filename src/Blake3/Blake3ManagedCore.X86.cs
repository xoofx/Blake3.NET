// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Blake3;

internal static partial class Blake3ManagedCore
{
    private static int TryHashMany4X86(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        return Sse2.IsSupported && input.Length >= 4 * ChunkLength
            ? Hash4ChunksX86(input, keyWords, chunkCounter, flags, output)
            : 0;
    }

    private static int TryHashMany8X86(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        if (!Sse2.IsSupported || input.Length < 8 * ChunkLength)
        {
            return 0;
        }

        Hash4ChunksX86(input, keyWords, chunkCounter, flags, output);
        Hash4ChunksX86(
            input[(4 * ChunkLength)..],
            keyWords,
            chunkCounter + 4,
            flags,
            output[(4 * 8)..]);
        return 8;
    }

    [SkipLocalsInit]
    private static int Hash4ChunksX86(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        const int Degree = 4;
        var inputWords = MemoryMarshal.Cast<byte, uint>(input[..(Degree * ChunkLength)]);
        Span<Vector128<uint>> chainingValues = stackalloc Vector128<uint>[8];
        Span<Vector128<uint>> message = stackalloc Vector128<uint>[16];
        ref var chainingValue = ref MemoryMarshal.GetReference(chainingValues);
        ref var messageWord = ref MemoryMarshal.GetReference(message);
        ref var inputWord = ref MemoryMarshal.GetReference(inputWords);

        for (var index = 0; index < 8; index++)
        {
            Unsafe.Add(ref chainingValue, index) = Vector128.Create(keyWords[index]);
        }

        var counterLow = Vector128.Create(
            (uint)chunkCounter, (uint)(chunkCounter + 1),
            (uint)(chunkCounter + 2), (uint)(chunkCounter + 3));
        var counterHigh = Vector128.Create(
            (uint)(chunkCounter >> 32), (uint)((chunkCounter + 1) >> 32),
            (uint)((chunkCounter + 2) >> 32), (uint)((chunkCounter + 3) >> 32));

        for (var block = 0; block < ChunkLength / BlockLength; block++)
        {
            LoadAndTranspose4X86(ref inputWord, block * 16, ref messageWord, 256);
            var blockFlags = flags;
            if (block == 0)
            {
                blockFlags |= ChunkStart;
            }

            if (block == (ChunkLength / BlockLength) - 1)
            {
                blockFlags |= ChunkEnd;
            }

            Compress4X86(ref chainingValue, ref messageWord, in counterLow, in counterHigh, blockFlags);
        }

        StoreTransposed4X86(ref chainingValue, output, Degree);
        return Degree;
    }

    [SkipLocalsInit]
    private static bool TryCompress4ParentsX86(
        Span<uint> childChainingValues,
        int parentCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        if (!Sse2.IsSupported || parentCount is < 1 or > 4)
        {
            return false;
        }

        Span<Vector128<uint>> chainingValues = stackalloc Vector128<uint>[8];
        Span<Vector128<uint>> message = stackalloc Vector128<uint>[16];
        ref var chainingValue = ref MemoryMarshal.GetReference(chainingValues);
        ref var messageWord = ref MemoryMarshal.GetReference(message);
        ref var childWord = ref MemoryMarshal.GetReference(childChainingValues);

        for (var index = 0; index < 8; index++)
        {
            Unsafe.Add(ref chainingValue, index) = Vector128.Create(keyWords[index]);
        }

        message.Clear();
        for (nuint parent = 0; parent < (nuint)parentCount; parent++)
        {
            var childOffset = parent * 16;
            Unsafe.Add(ref messageWord, parent) = Vector128.LoadUnsafe(ref childWord, childOffset);
            Unsafe.Add(ref messageWord, parent + 4) = Vector128.LoadUnsafe(ref childWord, childOffset + 4);
            Unsafe.Add(ref messageWord, parent + 8) = Vector128.LoadUnsafe(ref childWord, childOffset + 8);
            Unsafe.Add(ref messageWord, parent + 12) = Vector128.LoadUnsafe(ref childWord, childOffset + 12);
        }

        Transpose4X86(ref messageWord);
        Transpose4X86(ref Unsafe.Add(ref messageWord, 4));
        Transpose4X86(ref Unsafe.Add(ref messageWord, 8));
        Transpose4X86(ref Unsafe.Add(ref messageWord, 12));
        var zero = Vector128<uint>.Zero;
        Compress4X86(ref chainingValue, ref messageWord, in zero, in zero, flags | Parent);
        StoreTransposed4X86(ref chainingValue, childChainingValues, parentCount);
        return true;
    }

    private static bool TryCompressParentsX86(
        Span<uint> childChainingValues,
        int parentCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        if (!Sse2.IsSupported || parentCount is < 1 or > 8)
        {
            return false;
        }

        var firstCount = Math.Min(parentCount, 4);
        TryCompress4ParentsX86(childChainingValues, firstCount, keyWords, flags);
        if (parentCount > 4)
        {
            TryCompress4ParentsX86(childChainingValues[(4 * 16)..], parentCount - 4, keyWords, flags);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LoadAndTranspose4X86(
        ref uint inputWord,
        int wordOffset,
        ref Vector128<uint> messageWord,
        int laneStride)
    {
        for (nuint word = 0; word < 16; word += 4)
        {
            var inputOffset = (nuint)wordOffset + word;
            Unsafe.Add(ref messageWord, word) = Vector128.LoadUnsafe(ref inputWord, inputOffset);
            Unsafe.Add(ref messageWord, word + 1) = Vector128.LoadUnsafe(ref inputWord, inputOffset + (nuint)laneStride);
            Unsafe.Add(ref messageWord, word + 2) = Vector128.LoadUnsafe(ref inputWord, inputOffset + (2u * (nuint)laneStride));
            Unsafe.Add(ref messageWord, word + 3) = Vector128.LoadUnsafe(ref inputWord, inputOffset + (3u * (nuint)laneStride));
            Transpose4X86(ref Unsafe.Add(ref messageWord, word));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTransposed4X86(
        ref Vector128<uint> chainingValue,
        Span<uint> output,
        int count)
    {
        ref var outputWord = ref MemoryMarshal.GetReference(output);
        Transpose4X86(ref chainingValue);
        Transpose4X86(ref Unsafe.Add(ref chainingValue, 4));
        for (nuint lane = 0; lane < (nuint)count; lane++)
        {
            Unsafe.Add(ref chainingValue, lane).StoreUnsafe(ref outputWord, lane * 8);
            Unsafe.Add(ref chainingValue, lane + 4).StoreUnsafe(ref outputWord, (lane * 8) + 4);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose4X86(ref Vector128<uint> first)
    {
        var row0 = first;
        var row1 = Unsafe.Add(ref first, 1);
        var row2 = Unsafe.Add(ref first, 2);
        var row3 = Unsafe.Add(ref first, 3);
        var row01Low = Sse2.UnpackLow(row0, row1);
        var row01High = Sse2.UnpackHigh(row0, row1);
        var row23Low = Sse2.UnpackLow(row2, row3);
        var row23High = Sse2.UnpackHigh(row2, row3);
        first = Sse2.UnpackLow(row01Low.AsUInt64(), row23Low.AsUInt64()).AsUInt32();
        Unsafe.Add(ref first, 1) = Sse2.UnpackHigh(row01Low.AsUInt64(), row23Low.AsUInt64()).AsUInt32();
        Unsafe.Add(ref first, 2) = Sse2.UnpackLow(row01High.AsUInt64(), row23High.AsUInt64()).AsUInt32();
        Unsafe.Add(ref first, 3) = Sse2.UnpackHigh(row01High.AsUInt64(), row23High.AsUInt64()).AsUInt32();
    }

    [SkipLocalsInit]
    private static void Compress4X86(
        ref Vector128<uint> chainingValue,
        ref Vector128<uint> message,
        in Vector128<uint> counterLow,
        in Vector128<uint> counterHigh,
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
        var v8 = Vector128.Create(0x6A09E667u);
        var v9 = Vector128.Create(0xBB67AE85u);
        var v10 = Vector128.Create(0x3C6EF372u);
        var v11 = Vector128.Create(0xA54FF53Au);
        var v12 = counterLow;
        var v13 = counterHigh;
        var v14 = Vector128.Create((uint)BlockLength);
        var v15 = Vector128.Create(flags);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 0, 1);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 2, 3);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 4, 5);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 6, 7);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 8, 9);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 10, 11);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 12, 13);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 14, 15);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 2, 6);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 3, 10);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 7, 0);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 4, 13);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 1, 11);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 12, 5);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 9, 14);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 15, 8);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 3, 4);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 10, 12);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 13, 2);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 7, 14);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 6, 5);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 9, 0);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 11, 15);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 8, 1);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 10, 7);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 12, 9);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 14, 3);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 13, 15);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 4, 0);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 11, 2);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 5, 8);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 1, 6);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 12, 13);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 9, 11);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 15, 10);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 14, 8);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 7, 2);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 5, 3);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 0, 1);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 6, 4);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 9, 14);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 11, 5);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 8, 12);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 15, 1);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 13, 3);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 0, 10);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 2, 6);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 4, 7);

        Mix4X86(ref v0, ref v4, ref v8, ref v12, ref message, 11, 15);
        Mix4X86(ref v1, ref v5, ref v9, ref v13, ref message, 5, 0);
        Mix4X86(ref v2, ref v6, ref v10, ref v14, ref message, 1, 9);
        Mix4X86(ref v3, ref v7, ref v11, ref v15, ref message, 8, 6);
        Mix4X86(ref v0, ref v5, ref v10, ref v15, ref message, 14, 10);
        Mix4X86(ref v1, ref v6, ref v11, ref v12, ref message, 2, 12);
        Mix4X86(ref v2, ref v7, ref v8, ref v13, ref message, 3, 4);
        Mix4X86(ref v3, ref v4, ref v9, ref v14, ref message, 7, 13);

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
    private static void Mix4X86(
        ref Vector128<uint> a,
        ref Vector128<uint> b,
        ref Vector128<uint> c,
        ref Vector128<uint> d,
        ref Vector128<uint> message,
        int messageX,
        int messageY)
    {
        a = a + b + Unsafe.Add(ref message, messageX);
        d ^= a;
        // Shift-based rotates avoid keeping shuffle masks live across the fully unrolled rounds,
        // which substantially reduces register spills in the x64 JIT's four-way kernel.
        d = Sse2.Or(Sse2.ShiftRightLogical(d, 16), Sse2.ShiftLeftLogical(d, 16));
        c += d;
        b = RotateRight12(b ^ c);
        a = a + b + Unsafe.Add(ref message, messageY);
        d ^= a;
        d = Sse2.Or(Sse2.ShiftRightLogical(d, 8), Sse2.ShiftLeftLogical(d, 24));
        c += d;
        b = RotateRight7(b ^ c);
    }
}
