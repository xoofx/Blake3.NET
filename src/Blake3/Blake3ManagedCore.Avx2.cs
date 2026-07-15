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
    [SkipLocalsInit]
    private static int Hash8ChunksAvx2(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        const int Degree = 8;
        var inputWords = MemoryMarshal.Cast<byte, uint>(input[..(Degree * ChunkLength)]);
        Span<Vector256<uint>> chainingValues = stackalloc Vector256<uint>[8];
        Span<Vector256<uint>> message = stackalloc Vector256<uint>[16];
        ref var chainingValue = ref MemoryMarshal.GetReference(chainingValues);
        ref var messageWord = ref MemoryMarshal.GetReference(message);
        ref var inputWord = ref MemoryMarshal.GetReference(inputWords);

        for (var index = 0; index < 8; index++)
        {
            Unsafe.Add(ref chainingValue, index) = Vector256.Create(keyWords[index]);
        }

        var counterLow = Vector256.Create(
            (uint)chunkCounter, (uint)(chunkCounter + 1),
            (uint)(chunkCounter + 2), (uint)(chunkCounter + 3),
            (uint)(chunkCounter + 4), (uint)(chunkCounter + 5),
            (uint)(chunkCounter + 6), (uint)(chunkCounter + 7));
        var counterHigh = Vector256.Create(
            (uint)(chunkCounter >> 32), (uint)((chunkCounter + 1) >> 32),
            (uint)((chunkCounter + 2) >> 32), (uint)((chunkCounter + 3) >> 32),
            (uint)((chunkCounter + 4) >> 32), (uint)((chunkCounter + 5) >> 32),
            (uint)((chunkCounter + 6) >> 32), (uint)((chunkCounter + 7) >> 32));

        for (var block = 0; block < ChunkLength / BlockLength; block++)
        {
            LoadAndTranspose8(ref inputWord, block * 16, ref messageWord, 256);
            var blockFlags = flags;
            if (block == 0)
            {
                blockFlags |= ChunkStart;
            }

            if (block == (ChunkLength / BlockLength) - 1)
            {
                blockFlags |= ChunkEnd;
            }

            Compress8(ref chainingValue, ref messageWord, in counterLow, in counterHigh, blockFlags);
        }

        StoreTransposed8(ref chainingValue, output, Degree);
        return Degree;
    }

    [SkipLocalsInit]
    private static bool TryCompress8Parents(
        Span<uint> childChainingValues,
        int parentCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        if (!Avx2.IsSupported || parentCount is < 1 or > 8)
        {
            return false;
        }

        Span<Vector256<uint>> chainingValues = stackalloc Vector256<uint>[8];
        Span<Vector256<uint>> message = stackalloc Vector256<uint>[16];
        ref var chainingValue = ref MemoryMarshal.GetReference(chainingValues);
        ref var messageWord = ref MemoryMarshal.GetReference(message);
        ref var childWord = ref MemoryMarshal.GetReference(childChainingValues);

        for (var index = 0; index < 8; index++)
        {
            Unsafe.Add(ref chainingValue, index) = Vector256.Create(keyWords[index]);
        }

        message.Clear();
        for (nuint parent = 0; parent < (nuint)parentCount; parent++)
        {
            var childOffset = parent * 16;
            Unsafe.Add(ref messageWord, parent) = Vector256.LoadUnsafe(ref childWord, childOffset);
            Unsafe.Add(ref messageWord, parent + 8) = Vector256.LoadUnsafe(ref childWord, childOffset + 8);
        }

        Transpose8(ref messageWord);
        Transpose8(ref Unsafe.Add(ref messageWord, 8));
        var zero = Vector256<uint>.Zero;
        Compress8(ref chainingValue, ref messageWord, in zero, in zero, flags | Parent);
        StoreTransposed8(ref chainingValue, childChainingValues, parentCount);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LoadAndTranspose8(
        ref uint inputWord,
        int wordOffset,
        ref Vector256<uint> messageWord,
        int laneStride)
    {
        for (nuint lane = 0; lane < 8; lane++)
        {
            var inputOffset = (nuint)wordOffset + (lane * (nuint)laneStride);
            Unsafe.Add(ref messageWord, lane) = Vector256.LoadUnsafe(ref inputWord, inputOffset);
            Unsafe.Add(ref messageWord, lane + 8) = Vector256.LoadUnsafe(ref inputWord, inputOffset + 8);
        }

        Transpose8(ref messageWord);
        Transpose8(ref Unsafe.Add(ref messageWord, 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTransposed8(
        ref Vector256<uint> chainingValue,
        Span<uint> output,
        int count)
    {
        ref var outputWord = ref MemoryMarshal.GetReference(output);
        Transpose8(ref chainingValue);
        for (nuint lane = 0; lane < (nuint)count; lane++)
        {
            Unsafe.Add(ref chainingValue, lane).StoreUnsafe(ref outputWord, lane * 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Transpose8(ref Vector256<uint> first)
    {
        var v0 = first;
        var v1 = Unsafe.Add(ref first, 1);
        var v2 = Unsafe.Add(ref first, 2);
        var v3 = Unsafe.Add(ref first, 3);
        var v4 = Unsafe.Add(ref first, 4);
        var v5 = Unsafe.Add(ref first, 5);
        var v6 = Unsafe.Add(ref first, 6);
        var v7 = Unsafe.Add(ref first, 7);

        var ab0 = Avx2.UnpackLow(v0, v1);
        var ab2 = Avx2.UnpackHigh(v0, v1);
        var cd0 = Avx2.UnpackLow(v2, v3);
        var cd2 = Avx2.UnpackHigh(v2, v3);
        var ef0 = Avx2.UnpackLow(v4, v5);
        var ef2 = Avx2.UnpackHigh(v4, v5);
        var gh0 = Avx2.UnpackLow(v6, v7);
        var gh2 = Avx2.UnpackHigh(v6, v7);

        var abcd0 = Avx2.UnpackLow(ab0.AsUInt64(), cd0.AsUInt64()).AsUInt32();
        var abcd1 = Avx2.UnpackHigh(ab0.AsUInt64(), cd0.AsUInt64()).AsUInt32();
        var abcd2 = Avx2.UnpackLow(ab2.AsUInt64(), cd2.AsUInt64()).AsUInt32();
        var abcd3 = Avx2.UnpackHigh(ab2.AsUInt64(), cd2.AsUInt64()).AsUInt32();
        var efgh0 = Avx2.UnpackLow(ef0.AsUInt64(), gh0.AsUInt64()).AsUInt32();
        var efgh1 = Avx2.UnpackHigh(ef0.AsUInt64(), gh0.AsUInt64()).AsUInt32();
        var efgh2 = Avx2.UnpackLow(ef2.AsUInt64(), gh2.AsUInt64()).AsUInt32();
        var efgh3 = Avx2.UnpackHigh(ef2.AsUInt64(), gh2.AsUInt64()).AsUInt32();

        first = Avx2.Permute2x128(abcd0, efgh0, 0x20);
        Unsafe.Add(ref first, 1) = Avx2.Permute2x128(abcd1, efgh1, 0x20);
        Unsafe.Add(ref first, 2) = Avx2.Permute2x128(abcd2, efgh2, 0x20);
        Unsafe.Add(ref first, 3) = Avx2.Permute2x128(abcd3, efgh3, 0x20);
        Unsafe.Add(ref first, 4) = Avx2.Permute2x128(abcd0, efgh0, 0x31);
        Unsafe.Add(ref first, 5) = Avx2.Permute2x128(abcd1, efgh1, 0x31);
        Unsafe.Add(ref first, 6) = Avx2.Permute2x128(abcd2, efgh2, 0x31);
        Unsafe.Add(ref first, 7) = Avx2.Permute2x128(abcd3, efgh3, 0x31);
    }

    [SkipLocalsInit]
    private static void Compress8(
        ref Vector256<uint> chainingValue,
        ref Vector256<uint> message,
        in Vector256<uint> counterLow,
        in Vector256<uint> counterHigh,
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
        var v8 = Vector256.Create(0x6A09E667u);
        var v9 = Vector256.Create(0xBB67AE85u);
        var v10 = Vector256.Create(0x3C6EF372u);
        var v11 = Vector256.Create(0xA54FF53Au);
        var v12 = counterLow;
        var v13 = counterHigh;
        var v14 = Vector256.Create((uint)BlockLength);
        var v15 = Vector256.Create(flags);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 0, 1);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 2, 3);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 4, 5);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 6, 7);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 8, 9);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 10, 11);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 12, 13);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 14, 15);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 2, 6);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 3, 10);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 7, 0);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 4, 13);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 1, 11);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 12, 5);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 9, 14);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 15, 8);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 3, 4);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 10, 12);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 13, 2);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 7, 14);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 6, 5);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 9, 0);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 11, 15);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 8, 1);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 10, 7);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 12, 9);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 14, 3);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 13, 15);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 4, 0);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 11, 2);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 5, 8);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 1, 6);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 12, 13);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 9, 11);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 15, 10);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 14, 8);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 7, 2);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 5, 3);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 0, 1);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 6, 4);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 9, 14);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 11, 5);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 8, 12);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 15, 1);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 13, 3);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 0, 10);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 2, 6);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 4, 7);

        Mix8(ref v0, ref v4, ref v8, ref v12, ref message, 11, 15);
        Mix8(ref v1, ref v5, ref v9, ref v13, ref message, 5, 0);
        Mix8(ref v2, ref v6, ref v10, ref v14, ref message, 1, 9);
        Mix8(ref v3, ref v7, ref v11, ref v15, ref message, 8, 6);
        Mix8(ref v0, ref v5, ref v10, ref v15, ref message, 14, 10);
        Mix8(ref v1, ref v6, ref v11, ref v12, ref message, 2, 12);
        Mix8(ref v2, ref v7, ref v8, ref v13, ref message, 3, 4);
        Mix8(ref v3, ref v4, ref v9, ref v14, ref message, 7, 13);

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
    private static void Mix8(
        ref Vector256<uint> a,
        ref Vector256<uint> b,
        ref Vector256<uint> c,
        ref Vector256<uint> d,
        ref Vector256<uint> message,
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
    private static Vector256<uint> RotateRight16(Vector256<uint> value) => Avx512F.VL.IsSupported
        ? Avx512F.VL.RotateRight(value, 16)
        : Avx2.Shuffle(value.AsByte(), RotateRight16Mask256).AsUInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> RotateRight12(Vector256<uint> value) => Avx512F.VL.IsSupported
        ? Avx512F.VL.RotateRight(value, 12)
        : Avx2.Or(Avx2.ShiftRightLogical(value, 12), Avx2.ShiftLeftLogical(value, 20));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> RotateRight8(Vector256<uint> value) => Avx512F.VL.IsSupported
        ? Avx512F.VL.RotateRight(value, 8)
        : Avx2.Shuffle(value.AsByte(), RotateRight8Mask256).AsUInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> RotateRight7(Vector256<uint> value) => Avx512F.VL.IsSupported
        ? Avx512F.VL.RotateRight(value, 7)
        : Avx2.Or(Avx2.ShiftRightLogical(value, 7), Avx2.ShiftLeftLogical(value, 25));
}
