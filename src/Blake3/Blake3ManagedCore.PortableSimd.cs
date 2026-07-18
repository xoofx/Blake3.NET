// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Blake3;

internal static partial class Blake3ManagedCore
{
    private static readonly Vector128<byte> RotateRight8Mask128 =
        Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12);

    private static readonly Vector128<byte> RotateRight16Mask128 =
        Vector128.Create((byte)2, 3, 0, 1, 6, 7, 4, 5, 10, 11, 8, 9, 14, 15, 12, 13);

    private static readonly Vector256<byte> RotateRight8Mask256 =
        Vector256.Create(RotateRight8Mask128, RotateRight8Mask128);

    private static readonly Vector256<byte> RotateRight16Mask256 =
        Vector256.Create(RotateRight16Mask128, RotateRight16Mask128);

    [SkipLocalsInit]
    private static void CompressVector128(
        ReadOnlySpan<uint> chainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> output)
    {
        var row0 = Vector128.Create(chainingValue[0], chainingValue[1], chainingValue[2], chainingValue[3]);
        var row1 = Vector128.Create(chainingValue[4], chainingValue[5], chainingValue[6], chainingValue[7]);
        var row2 = Vector128.Create(0x6A09E667u, 0xBB67AE85u, 0x3C6EF372u, 0xA54FF53Au);
        var row3 = Vector128.Create((uint)counter, (uint)(counter >> 32), blockLength, flags);

        var schedule = MessageSchedule;
        for (var round = 0; round < 7; round++)
        {
            var row = schedule.Slice(round * 16, 16);
            var messageX = Vector128.Create(
                blockWords[row[0]], blockWords[row[2]], blockWords[row[4]], blockWords[row[6]]);
            var messageY = Vector128.Create(
                blockWords[row[1]], blockWords[row[3]], blockWords[row[5]], blockWords[row[7]]);
            Mix(ref row0, ref row1, ref row2, ref row3, messageX, messageY);

            row1 = Vector128.Shuffle(row1, Vector128.Create(1u, 2u, 3u, 0u));
            row2 = Vector128.Shuffle(row2, Vector128.Create(2u, 3u, 0u, 1u));
            row3 = Vector128.Shuffle(row3, Vector128.Create(3u, 0u, 1u, 2u));

            messageX = Vector128.Create(
                blockWords[row[8]], blockWords[row[10]], blockWords[row[12]], blockWords[row[14]]);
            messageY = Vector128.Create(
                blockWords[row[9]], blockWords[row[11]], blockWords[row[13]], blockWords[row[15]]);
            Mix(ref row0, ref row1, ref row2, ref row3, messageX, messageY);

            row1 = Vector128.Shuffle(row1, Vector128.Create(3u, 0u, 1u, 2u));
            row2 = Vector128.Shuffle(row2, Vector128.Create(2u, 3u, 0u, 1u));
            row3 = Vector128.Shuffle(row3, Vector128.Create(1u, 2u, 3u, 0u));
        }

        (row0 ^ row2).CopyTo(output);
        (row1 ^ row3).CopyTo(output[4..]);
        if (output.Length >= 16)
        {
            (row2 ^ Vector128.Create(chainingValue[0], chainingValue[1], chainingValue[2], chainingValue[3])).CopyTo(output[8..]);
            (row3 ^ Vector128.Create(chainingValue[4], chainingValue[5], chainingValue[6], chainingValue[7])).CopyTo(output[12..]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(
        ref Vector128<uint> row0,
        ref Vector128<uint> row1,
        ref Vector128<uint> row2,
        ref Vector128<uint> row3,
        Vector128<uint> messageX,
        Vector128<uint> messageY)
    {
        row0 = row0 + row1 + messageX;
        row3 = RotateRight16(row3 ^ row0);
        row2 += row3;
        row1 = RotateRight12(row1 ^ row2);
        row0 = row0 + row1 + messageY;
        row3 = RotateRight8(row3 ^ row0);
        row2 += row3;
        row1 = RotateRight7(row1 ^ row2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight16(Vector128<uint> value)
    {
        if (Avx512F.VL.IsSupported)
        {
            return Avx512F.VL.RotateRight(value, 16);
        }

        if (Ssse3.IsSupported)
        {
            return Ssse3.Shuffle(value.AsByte(), RotateRight16Mask128).AsUInt32();
        }

        return Vector128.ShiftRightLogical(value, 16) | Vector128.ShiftLeft(value, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight12(Vector128<uint> value) => Avx512F.VL.IsSupported
        ? Avx512F.VL.RotateRight(value, 12)
        : Vector128.ShiftRightLogical(value, 12) | Vector128.ShiftLeft(value, 20);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight8(Vector128<uint> value)
    {
        if (Avx512F.VL.IsSupported)
        {
            return Avx512F.VL.RotateRight(value, 8);
        }

        if (Ssse3.IsSupported)
        {
            return Ssse3.Shuffle(value.AsByte(), RotateRight8Mask128).AsUInt32();
        }

        return Vector128.ShiftRightLogical(value, 8) | Vector128.ShiftLeft(value, 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight7(Vector128<uint> value) => Avx512F.VL.IsSupported
        ? Avx512F.VL.RotateRight(value, 7)
        : Vector128.ShiftRightLogical(value, 7) | Vector128.ShiftLeft(value, 25);

    /// <summary>
    /// Hashes a platform-sized group of complete chunks in parallel. This is eight chunks on
    /// current 256-bit runtimes and four chunks on 128-bit runtimes.
    /// </summary>
    /// <remarks>
    /// Dispatches to Arm64, AVX2, or SSE2 kernels when available, then falls back to the portable
    /// <see cref="Vector{T}"/> implementation below.
    /// </remarks>
    [SkipLocalsInit]
    internal static int TryHashVectorChunks(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        var degree = Vector<uint>.Count;
        if (!Vector.IsHardwareAccelerated ||
            degree is not (4 or 8) ||
            input.Length < degree * ChunkLength ||
            output.Length < degree * 8)
        {
            return 0;
        }

        if (degree == 4 && AdvSimd.Arm64.IsSupported)
        {
            return Hash4ChunksArm64(input, keyWords, chunkCounter, flags, output);
        }

        if (degree == 4 && Sse2.IsSupported)
        {
            return TryHashMany4X86(input, keyWords, chunkCounter, flags, output);
        }

        if (degree == 8 && Avx2.IsSupported)
        {
            return Hash8ChunksAvx2(input, keyWords, chunkCounter, flags, output);
        }

        if (degree == 8 && Sse2.IsSupported)
        {
            return TryHashMany8X86(input, keyWords, chunkCounter, flags, output);
        }

        var inputWords = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(input[..(degree * ChunkLength)]);
        Span<Vector<uint>> chainingValues = stackalloc Vector<uint>[8];
        Span<Vector<uint>> message = stackalloc Vector<uint>[16];
        Span<uint> lanes = stackalloc uint[Vector<uint>.Count];
        for (var index = 0; index < chainingValues.Length; index++)
        {
            chainingValues[index] = new Vector<uint>(keyWords[index]);
        }

        for (var lane = 0; lane < degree; lane++)
        {
            var counter = chunkCounter + (ulong)lane;
            lanes[lane] = (uint)counter;
        }

        var counterLow = new Vector<uint>(lanes);
        for (var lane = 0; lane < degree; lane++)
        {
            var counter = chunkCounter + (ulong)lane;
            lanes[lane] = (uint)(counter >> 32);
        }

        var counterHigh = new Vector<uint>(lanes);
        for (var block = 0; block < ChunkLength / BlockLength; block++)
        {
            var wordOffset = block * 16;
            for (var word = 0; word < 16; word++)
            {
                for (var lane = 0; lane < degree; lane++)
                {
                    lanes[lane] = inputWords[wordOffset + word + (lane * 256)];
                }

                message[word] = new Vector<uint>(lanes);
            }

            var blockFlags = flags;
            if (block == 0)
            {
                blockFlags |= ChunkStart;
            }

            if (block == (ChunkLength / BlockLength) - 1)
            {
                blockFlags |= ChunkEnd;
            }

            CompressVector(chainingValues, message, counterLow, counterHigh, new Vector<uint>(blockFlags));
        }

        Span<uint> transposedOutput = stackalloc uint[8 * Vector<uint>.Count];
        for (var word = 0; word < 8; word++)
        {
            chainingValues[word].CopyTo(transposedOutput.Slice(word * degree, degree));
        }

        for (var lane = 0; lane < degree; lane++)
        {
            for (var word = 0; word < 8; word++)
            {
                output[(lane * 8) + word] = transposedOutput[(word * degree) + lane];
            }
        }

        return degree;
    }

    [SkipLocalsInit]
    private static void CompressVector(
        Span<Vector<uint>> chainingValues,
        ReadOnlySpan<Vector<uint>> message,
        Vector<uint> counterLow,
        Vector<uint> counterHigh,
        Vector<uint> flags)
    {
        var v0 = chainingValues[0];
        var v1 = chainingValues[1];
        var v2 = chainingValues[2];
        var v3 = chainingValues[3];
        var v4 = chainingValues[4];
        var v5 = chainingValues[5];
        var v6 = chainingValues[6];
        var v7 = chainingValues[7];
        var v8 = new Vector<uint>(0x6A09E667u);
        var v9 = new Vector<uint>(0xBB67AE85u);
        var v10 = new Vector<uint>(0x3C6EF372u);
        var v11 = new Vector<uint>(0xA54FF53Au);
        var v12 = counterLow;
        var v13 = counterHigh;
        var v14 = new Vector<uint>(BlockLength);
        var v15 = flags;

        var schedule = MessageSchedule;
        for (var round = 0; round < 7; round++)
        {
            var row = schedule.Slice(round * 16, 16);
            Mix(ref v0, ref v4, ref v8, ref v12, message[row[0]], message[row[1]]);
            Mix(ref v1, ref v5, ref v9, ref v13, message[row[2]], message[row[3]]);
            Mix(ref v2, ref v6, ref v10, ref v14, message[row[4]], message[row[5]]);
            Mix(ref v3, ref v7, ref v11, ref v15, message[row[6]], message[row[7]]);
            Mix(ref v0, ref v5, ref v10, ref v15, message[row[8]], message[row[9]]);
            Mix(ref v1, ref v6, ref v11, ref v12, message[row[10]], message[row[11]]);
            Mix(ref v2, ref v7, ref v8, ref v13, message[row[12]], message[row[13]]);
            Mix(ref v3, ref v4, ref v9, ref v14, message[row[14]], message[row[15]]);
        }

        chainingValues[0] = v0 ^ v8;
        chainingValues[1] = v1 ^ v9;
        chainingValues[2] = v2 ^ v10;
        chainingValues[3] = v3 ^ v11;
        chainingValues[4] = v4 ^ v12;
        chainingValues[5] = v5 ^ v13;
        chainingValues[6] = v6 ^ v14;
        chainingValues[7] = v7 ^ v15;
    }

    [SkipLocalsInit]
    private static void CompressParentsVector(
        Span<uint> childChainingValues,
        int parentCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        var degree = Vector<uint>.Count;
        Span<Vector<uint>> chainingValues = stackalloc Vector<uint>[8];
        Span<Vector<uint>> message = stackalloc Vector<uint>[16];
        Span<uint> lanes = stackalloc uint[Vector<uint>.Count];
        for (var word = 0; word < 8; word++)
        {
            chainingValues[word] = new Vector<uint>(keyWords[word]);
        }

        for (var word = 0; word < 16; word++)
        {
            lanes.Clear();
            for (var lane = 0; lane < parentCount; lane++)
            {
                lanes[lane] = childChainingValues[(lane * 16) + word];
            }

            message[word] = new Vector<uint>(lanes);
        }

        var zero = Vector<uint>.Zero;
        CompressVector(chainingValues, message, zero, zero, new Vector<uint>(flags | Parent));

        Span<uint> transposedOutput = stackalloc uint[8 * Vector<uint>.Count];
        for (var word = 0; word < 8; word++)
        {
            chainingValues[word].CopyTo(transposedOutput.Slice(word * degree, degree));
        }

        for (var lane = 0; lane < parentCount; lane++)
        {
            for (var word = 0; word < 8; word++)
            {
                childChainingValues[(lane * 8) + word] = transposedOutput[(word * degree) + lane];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(
        ref Vector<uint> a,
        ref Vector<uint> b,
        ref Vector<uint> c,
        ref Vector<uint> d,
        Vector<uint> messageX,
        Vector<uint> messageY)
    {
        a = a + b + messageX;
        d = RotateRight16(d ^ a);
        c += d;
        b = RotateRight12(b ^ c);
        a = a + b + messageY;
        d = RotateRight8(d ^ a);
        c += d;
        b = RotateRight7(b ^ c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> RotateRight16(Vector<uint> value)
    {
        if (Vector<uint>.Count == 8 && Avx512F.VL.IsSupported)
        {
            return Vector256.AsVector(Avx512F.VL.RotateRight(Vector256.AsVector256(value), 16));
        }

        if (Vector<uint>.Count == 8 && Avx2.IsSupported)
        {
            return Vector256.AsVector(
                Avx2.Shuffle(Vector256.AsVector256(value).AsByte(), RotateRight16Mask256).AsUInt32());
        }

        if (Vector<uint>.Count == 4 && Avx512F.VL.IsSupported)
        {
            return Vector128.AsVector(Avx512F.VL.RotateRight(Vector128.AsVector128(value), 16));
        }

        if (Vector<uint>.Count == 4 && Ssse3.IsSupported)
        {
            return Vector128.AsVector(
                Ssse3.Shuffle(Vector128.AsVector128(value).AsByte(), RotateRight16Mask128).AsUInt32());
        }

        return Vector.ShiftRightLogical(value, 16) | Vector.ShiftLeft(value, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> RotateRight12(Vector<uint> value)
    {
        if (Vector<uint>.Count == 8 && Avx512F.VL.IsSupported)
        {
            return Vector256.AsVector(Avx512F.VL.RotateRight(Vector256.AsVector256(value), 12));
        }

        if (Vector<uint>.Count == 4 && Avx512F.VL.IsSupported)
        {
            return Vector128.AsVector(Avx512F.VL.RotateRight(Vector128.AsVector128(value), 12));
        }

        return Vector.ShiftRightLogical(value, 12) | Vector.ShiftLeft(value, 20);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> RotateRight8(Vector<uint> value)
    {
        if (Vector<uint>.Count == 8 && Avx512F.VL.IsSupported)
        {
            return Vector256.AsVector(Avx512F.VL.RotateRight(Vector256.AsVector256(value), 8));
        }

        if (Vector<uint>.Count == 8 && Avx2.IsSupported)
        {
            return Vector256.AsVector(
                Avx2.Shuffle(Vector256.AsVector256(value).AsByte(), RotateRight8Mask256).AsUInt32());
        }

        if (Vector<uint>.Count == 4 && Avx512F.VL.IsSupported)
        {
            return Vector128.AsVector(Avx512F.VL.RotateRight(Vector128.AsVector128(value), 8));
        }

        if (Vector<uint>.Count == 4 && Ssse3.IsSupported)
        {
            return Vector128.AsVector(
                Ssse3.Shuffle(Vector128.AsVector128(value).AsByte(), RotateRight8Mask128).AsUInt32());
        }

        return Vector.ShiftRightLogical(value, 8) | Vector.ShiftLeft(value, 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> RotateRight7(Vector<uint> value)
    {
        if (Vector<uint>.Count == 8 && Avx512F.VL.IsSupported)
        {
            return Vector256.AsVector(Avx512F.VL.RotateRight(Vector256.AsVector256(value), 7));
        }

        if (Vector<uint>.Count == 4 && Avx512F.VL.IsSupported)
        {
            return Vector128.AsVector(Avx512F.VL.RotateRight(Vector128.AsVector128(value), 7));
        }

        return Vector.ShiftRightLogical(value, 7) | Vector.ShiftLeft(value, 25);
    }
}
