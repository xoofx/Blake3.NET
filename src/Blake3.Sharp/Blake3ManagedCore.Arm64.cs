// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace Blake3;

internal static partial class Blake3ManagedCore
{
    [SkipLocalsInit]
    private static void CompressScalarArm64(
        ReadOnlySpan<uint> chainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> output)
    {
        ref var chainingValueWord = ref MemoryMarshal.GetReference(chainingValue);
        ref var message = ref MemoryMarshal.GetReference(blockWords);
        ref var outputWord = ref MemoryMarshal.GetReference(output);
        var v0 = chainingValueWord;
        var v1 = Unsafe.Add(ref chainingValueWord, 1);
        var v2 = Unsafe.Add(ref chainingValueWord, 2);
        var v3 = Unsafe.Add(ref chainingValueWord, 3);
        var v4 = Unsafe.Add(ref chainingValueWord, 4);
        var v5 = Unsafe.Add(ref chainingValueWord, 5);
        var v6 = Unsafe.Add(ref chainingValueWord, 6);
        var v7 = Unsafe.Add(ref chainingValueWord, 7);
        var v8 = 0x6A09E667u;
        var v9 = 0xBB67AE85u;
        var v10 = 0x3C6EF372u;
        var v11 = 0xA54FF53Au;
        var v12 = (uint)counter;
        var v13 = (uint)(counter >> 32);
        var v14 = blockLength;
        var v15 = flags;

        var schedule = MessageSchedule;
        for (var round = 0; round < 7; round++)
        {
            RoundScalarArm64(
                ref v0, ref v1, ref v2, ref v3,
                ref v4, ref v5, ref v6, ref v7,
                ref v8, ref v9, ref v10, ref v11,
                ref v12, ref v13, ref v14, ref v15,
                ref message,
                schedule.Slice(round * 16, 16));
        }

        outputWord = v0 ^ v8;
        Unsafe.Add(ref outputWord, 1) = v1 ^ v9;
        Unsafe.Add(ref outputWord, 2) = v2 ^ v10;
        Unsafe.Add(ref outputWord, 3) = v3 ^ v11;
        Unsafe.Add(ref outputWord, 4) = v4 ^ v12;
        Unsafe.Add(ref outputWord, 5) = v5 ^ v13;
        Unsafe.Add(ref outputWord, 6) = v6 ^ v14;
        Unsafe.Add(ref outputWord, 7) = v7 ^ v15;
        if (output.Length >= 16)
        {
            Unsafe.Add(ref outputWord, 8) = v8 ^ chainingValueWord;
            Unsafe.Add(ref outputWord, 9) = v9 ^ Unsafe.Add(ref chainingValueWord, 1);
            Unsafe.Add(ref outputWord, 10) = v10 ^ Unsafe.Add(ref chainingValueWord, 2);
            Unsafe.Add(ref outputWord, 11) = v11 ^ Unsafe.Add(ref chainingValueWord, 3);
            Unsafe.Add(ref outputWord, 12) = v12 ^ Unsafe.Add(ref chainingValueWord, 4);
            Unsafe.Add(ref outputWord, 13) = v13 ^ Unsafe.Add(ref chainingValueWord, 5);
            Unsafe.Add(ref outputWord, 14) = v14 ^ Unsafe.Add(ref chainingValueWord, 6);
            Unsafe.Add(ref outputWord, 15) = v15 ^ Unsafe.Add(ref chainingValueWord, 7);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RoundScalarArm64(
        ref uint v0,
        ref uint v1,
        ref uint v2,
        ref uint v3,
        ref uint v4,
        ref uint v5,
        ref uint v6,
        ref uint v7,
        ref uint v8,
        ref uint v9,
        ref uint v10,
        ref uint v11,
        ref uint v12,
        ref uint v13,
        ref uint v14,
        ref uint v15,
        ref uint message,
        ReadOnlySpan<byte> schedule)
    {
        v0 = v0 + v4 + Unsafe.Add(ref message, schedule[0]);
        v1 = v1 + v5 + Unsafe.Add(ref message, schedule[2]);
        v2 = v2 + v6 + Unsafe.Add(ref message, schedule[4]);
        v3 = v3 + v7 + Unsafe.Add(ref message, schedule[6]);
        v12 = BitOperations.RotateRight(v12 ^ v0, 16);
        v13 = BitOperations.RotateRight(v13 ^ v1, 16);
        v14 = BitOperations.RotateRight(v14 ^ v2, 16);
        v15 = BitOperations.RotateRight(v15 ^ v3, 16);
        v8 += v12;
        v9 += v13;
        v10 += v14;
        v11 += v15;
        v4 = BitOperations.RotateRight(v4 ^ v8, 12);
        v5 = BitOperations.RotateRight(v5 ^ v9, 12);
        v6 = BitOperations.RotateRight(v6 ^ v10, 12);
        v7 = BitOperations.RotateRight(v7 ^ v11, 12);
        v0 = v0 + v4 + Unsafe.Add(ref message, schedule[1]);
        v1 = v1 + v5 + Unsafe.Add(ref message, schedule[3]);
        v2 = v2 + v6 + Unsafe.Add(ref message, schedule[5]);
        v3 = v3 + v7 + Unsafe.Add(ref message, schedule[7]);
        v12 = BitOperations.RotateRight(v12 ^ v0, 8);
        v13 = BitOperations.RotateRight(v13 ^ v1, 8);
        v14 = BitOperations.RotateRight(v14 ^ v2, 8);
        v15 = BitOperations.RotateRight(v15 ^ v3, 8);
        v8 += v12;
        v9 += v13;
        v10 += v14;
        v11 += v15;
        v4 = BitOperations.RotateRight(v4 ^ v8, 7);
        v5 = BitOperations.RotateRight(v5 ^ v9, 7);
        v6 = BitOperations.RotateRight(v6 ^ v10, 7);
        v7 = BitOperations.RotateRight(v7 ^ v11, 7);

        v0 = v0 + v5 + Unsafe.Add(ref message, schedule[8]);
        v1 = v1 + v6 + Unsafe.Add(ref message, schedule[10]);
        v2 = v2 + v7 + Unsafe.Add(ref message, schedule[12]);
        v3 = v3 + v4 + Unsafe.Add(ref message, schedule[14]);
        v15 = BitOperations.RotateRight(v15 ^ v0, 16);
        v12 = BitOperations.RotateRight(v12 ^ v1, 16);
        v13 = BitOperations.RotateRight(v13 ^ v2, 16);
        v14 = BitOperations.RotateRight(v14 ^ v3, 16);
        v10 += v15;
        v11 += v12;
        v8 += v13;
        v9 += v14;
        v5 = BitOperations.RotateRight(v5 ^ v10, 12);
        v6 = BitOperations.RotateRight(v6 ^ v11, 12);
        v7 = BitOperations.RotateRight(v7 ^ v8, 12);
        v4 = BitOperations.RotateRight(v4 ^ v9, 12);
        v0 = v0 + v5 + Unsafe.Add(ref message, schedule[9]);
        v1 = v1 + v6 + Unsafe.Add(ref message, schedule[11]);
        v2 = v2 + v7 + Unsafe.Add(ref message, schedule[13]);
        v3 = v3 + v4 + Unsafe.Add(ref message, schedule[15]);
        v15 = BitOperations.RotateRight(v15 ^ v0, 8);
        v12 = BitOperations.RotateRight(v12 ^ v1, 8);
        v13 = BitOperations.RotateRight(v13 ^ v2, 8);
        v14 = BitOperations.RotateRight(v14 ^ v3, 8);
        v10 += v15;
        v11 += v12;
        v8 += v13;
        v9 += v14;
        v5 = BitOperations.RotateRight(v5 ^ v10, 7);
        v6 = BitOperations.RotateRight(v6 ^ v11, 7);
        v7 = BitOperations.RotateRight(v7 ^ v8, 7);
        v4 = BitOperations.RotateRight(v4 ^ v9, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LoadAndTranspose4(
        ReadOnlySpan<uint> inputWords,
        int wordOffset,
        Span<Vector128<uint>> message)
    {
        ref var inputWord = ref MemoryMarshal.GetReference(inputWords);
        ref var messageWord = ref MemoryMarshal.GetReference(message);
        for (nuint word = 0; word < 16; word += 4)
        {
            var inputOffset = (nuint)wordOffset + word;
            var row0 = Vector128.LoadUnsafe(ref inputWord, inputOffset);
            var row1 = Vector128.LoadUnsafe(ref inputWord, inputOffset + 256);
            var row2 = Vector128.LoadUnsafe(ref inputWord, inputOffset + 512);
            var row3 = Vector128.LoadUnsafe(ref inputWord, inputOffset + 768);
            Transpose4(ref row0, ref row1, ref row2, ref row3);
            Unsafe.Add(ref messageWord, word) = row0;
            Unsafe.Add(ref messageWord, word + 1) = row1;
            Unsafe.Add(ref messageWord, word + 2) = row2;
            Unsafe.Add(ref messageWord, word + 3) = row3;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTransposed4(Span<Vector128<uint>> chainingValues, Span<uint> output)
    {
        ref var chainingValue = ref MemoryMarshal.GetReference(chainingValues);
        ref var outputWord = ref MemoryMarshal.GetReference(output);
        var first0 = Unsafe.Add(ref chainingValue, 0);
        var first1 = Unsafe.Add(ref chainingValue, 1);
        var first2 = Unsafe.Add(ref chainingValue, 2);
        var first3 = Unsafe.Add(ref chainingValue, 3);
        var second0 = Unsafe.Add(ref chainingValue, 4);
        var second1 = Unsafe.Add(ref chainingValue, 5);
        var second2 = Unsafe.Add(ref chainingValue, 6);
        var second3 = Unsafe.Add(ref chainingValue, 7);
        Transpose4(ref first0, ref first1, ref first2, ref first3);
        Transpose4(ref second0, ref second1, ref second2, ref second3);
        first0.StoreUnsafe(ref outputWord);
        second0.StoreUnsafe(ref outputWord, 4);
        first1.StoreUnsafe(ref outputWord, 8);
        second1.StoreUnsafe(ref outputWord, 12);
        first2.StoreUnsafe(ref outputWord, 16);
        second2.StoreUnsafe(ref outputWord, 20);
        first3.StoreUnsafe(ref outputWord, 24);
        second3.StoreUnsafe(ref outputWord, 28);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose4(
        ref Vector128<uint> row0,
        ref Vector128<uint> row1,
        ref Vector128<uint> row2,
        ref Vector128<uint> row3)
    {
        var row01Low = AdvSimd.Arm64.ZipLow(row0, row1);
        var row01High = AdvSimd.Arm64.ZipHigh(row0, row1);
        var row23Low = AdvSimd.Arm64.ZipLow(row2, row3);
        var row23High = AdvSimd.Arm64.ZipHigh(row2, row3);
        row0 = AdvSimd.Arm64.ZipLow(row01Low.AsUInt64(), row23Low.AsUInt64()).AsUInt32();
        row1 = AdvSimd.Arm64.ZipHigh(row01Low.AsUInt64(), row23Low.AsUInt64()).AsUInt32();
        row2 = AdvSimd.Arm64.ZipLow(row01High.AsUInt64(), row23High.AsUInt64()).AsUInt32();
        row3 = AdvSimd.Arm64.ZipHigh(row01High.AsUInt64(), row23High.AsUInt64()).AsUInt32();
    }

    [SkipLocalsInit]
    private static int Hash4ChunksArm64(
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
        for (var index = 0; index < chainingValues.Length; index++)
        {
            chainingValues[index] = Vector128.Create(keyWords[index]);
        }

        var counterLow = Vector128.Create(
            (uint)chunkCounter,
            (uint)(chunkCounter + 1),
            (uint)(chunkCounter + 2),
            (uint)(chunkCounter + 3));
        var counterHigh = Vector128.Create(
            (uint)(chunkCounter >> 32),
            (uint)((chunkCounter + 1) >> 32),
            (uint)((chunkCounter + 2) >> 32),
            (uint)((chunkCounter + 3) >> 32));

        for (var block = 0; block < ChunkLength / BlockLength; block++)
        {
            LoadAndTranspose4(inputWords, block * 16, message);
            var blockFlags = flags;
            if (block == 0)
            {
                blockFlags |= ChunkStart;
            }

            if (block == (ChunkLength / BlockLength) - 1)
            {
                blockFlags |= ChunkEnd;
            }

            Compress4Arm64(ref chainingValue, ref messageWord, in counterLow, in counterHigh, blockFlags);
        }

        StoreTransposed4(chainingValues, output);
        return Degree;
    }

    [SkipLocalsInit]
    private static void Compress4Arm64(
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
        var rotateRight8Mask = Vector128.Create(
            (byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12);

        var schedule = MessageSchedule;
        for (var round = 0; round < 7; round++)
        {
            var row = schedule.Slice(round * 16, 16);
            RoundArm64(
                ref v0, ref v1, ref v2, ref v3,
                ref v4, ref v5, ref v6, ref v7,
                ref v8, ref v9, ref v10, ref v11,
                ref v12, ref v13, ref v14, ref v15,
                ref message,
                row,
                rotateRight8Mask);
        }

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
    private static void RoundArm64(
        ref Vector128<uint> v0,
        ref Vector128<uint> v1,
        ref Vector128<uint> v2,
        ref Vector128<uint> v3,
        ref Vector128<uint> v4,
        ref Vector128<uint> v5,
        ref Vector128<uint> v6,
        ref Vector128<uint> v7,
        ref Vector128<uint> v8,
        ref Vector128<uint> v9,
        ref Vector128<uint> v10,
        ref Vector128<uint> v11,
        ref Vector128<uint> v12,
        ref Vector128<uint> v13,
        ref Vector128<uint> v14,
        ref Vector128<uint> v15,
        ref Vector128<uint> message,
        ReadOnlySpan<byte> schedule,
        Vector128<byte> rotateRight8Mask)
    {
        // Interleave the four independent G functions to hide SIMD instruction latency.
        v0 = v0 + v4 + Unsafe.Add(ref message, schedule[0]);
        v1 = v1 + v5 + Unsafe.Add(ref message, schedule[2]);
        v2 = v2 + v6 + Unsafe.Add(ref message, schedule[4]);
        v3 = v3 + v7 + Unsafe.Add(ref message, schedule[6]);
        v12 = AdvSimd.ReverseElement16(v12 ^ v0);
        v13 = AdvSimd.ReverseElement16(v13 ^ v1);
        v14 = AdvSimd.ReverseElement16(v14 ^ v2);
        v15 = AdvSimd.ReverseElement16(v15 ^ v3);
        v8 += v12;
        v9 += v13;
        v10 += v14;
        v11 += v15;
        v4 = RotateRight12Arm64(v4 ^ v8);
        v5 = RotateRight12Arm64(v5 ^ v9);
        v6 = RotateRight12Arm64(v6 ^ v10);
        v7 = RotateRight12Arm64(v7 ^ v11);
        v0 = v0 + v4 + Unsafe.Add(ref message, schedule[1]);
        v1 = v1 + v5 + Unsafe.Add(ref message, schedule[3]);
        v2 = v2 + v6 + Unsafe.Add(ref message, schedule[5]);
        v3 = v3 + v7 + Unsafe.Add(ref message, schedule[7]);
        v12 = RotateRight8Arm64(v12 ^ v0, rotateRight8Mask);
        v13 = RotateRight8Arm64(v13 ^ v1, rotateRight8Mask);
        v14 = RotateRight8Arm64(v14 ^ v2, rotateRight8Mask);
        v15 = RotateRight8Arm64(v15 ^ v3, rotateRight8Mask);
        v8 += v12;
        v9 += v13;
        v10 += v14;
        v11 += v15;
        v4 = RotateRight7Arm64(v4 ^ v8);
        v5 = RotateRight7Arm64(v5 ^ v9);
        v6 = RotateRight7Arm64(v6 ^ v10);
        v7 = RotateRight7Arm64(v7 ^ v11);

        v0 = v0 + v5 + Unsafe.Add(ref message, schedule[8]);
        v1 = v1 + v6 + Unsafe.Add(ref message, schedule[10]);
        v2 = v2 + v7 + Unsafe.Add(ref message, schedule[12]);
        v3 = v3 + v4 + Unsafe.Add(ref message, schedule[14]);
        v15 = AdvSimd.ReverseElement16(v15 ^ v0);
        v12 = AdvSimd.ReverseElement16(v12 ^ v1);
        v13 = AdvSimd.ReverseElement16(v13 ^ v2);
        v14 = AdvSimd.ReverseElement16(v14 ^ v3);
        v10 += v15;
        v11 += v12;
        v8 += v13;
        v9 += v14;
        v5 = RotateRight12Arm64(v5 ^ v10);
        v6 = RotateRight12Arm64(v6 ^ v11);
        v7 = RotateRight12Arm64(v7 ^ v8);
        v4 = RotateRight12Arm64(v4 ^ v9);
        v0 = v0 + v5 + Unsafe.Add(ref message, schedule[9]);
        v1 = v1 + v6 + Unsafe.Add(ref message, schedule[11]);
        v2 = v2 + v7 + Unsafe.Add(ref message, schedule[13]);
        v3 = v3 + v4 + Unsafe.Add(ref message, schedule[15]);
        v15 = RotateRight8Arm64(v15 ^ v0, rotateRight8Mask);
        v12 = RotateRight8Arm64(v12 ^ v1, rotateRight8Mask);
        v13 = RotateRight8Arm64(v13 ^ v2, rotateRight8Mask);
        v14 = RotateRight8Arm64(v14 ^ v3, rotateRight8Mask);
        v10 += v15;
        v11 += v12;
        v8 += v13;
        v9 += v14;
        v5 = RotateRight7Arm64(v5 ^ v10);
        v6 = RotateRight7Arm64(v6 ^ v11);
        v7 = RotateRight7Arm64(v7 ^ v8);
        v4 = RotateRight7Arm64(v4 ^ v9);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight12Arm64(Vector128<uint> value) =>
        AdvSimd.ShiftRightAndInsert(AdvSimd.ShiftLeftLogical(value, 20), value, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight8Arm64(Vector128<uint> value, Vector128<byte> mask) =>
        Vector128.Shuffle(value.AsByte(), mask).AsUInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight7Arm64(Vector128<uint> value) =>
        AdvSimd.ShiftRightAndInsert(AdvSimd.ShiftLeftLogical(value, 25), value, 7);
}
