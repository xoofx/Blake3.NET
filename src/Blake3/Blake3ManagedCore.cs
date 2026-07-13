// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Blake3;

/// <summary>
/// Portable implementation of the BLAKE3 compression function.
/// </summary>
internal static class Blake3ManagedCore
{
    internal const int OutputLength = 32;
    internal const int KeyLength = 32;
    internal const int BlockLength = 64;
    internal const int ChunkLength = 1024;
    internal const int MaximumDepth = 54;

    internal const uint ChunkStart = 1 << 0;
    internal const uint ChunkEnd = 1 << 1;
    internal const uint Parent = 1 << 2;
    internal const uint Root = 1 << 3;
    internal const uint KeyedHash = 1 << 4;
    internal const uint DeriveKeyContext = 1 << 5;
    internal const uint DeriveKeyMaterial = 1 << 6;

    internal static ReadOnlySpan<uint> InitializationVector =>
    [
        0x6A09E667u, 0xBB67AE85u, 0x3C6EF372u, 0xA54FF53Au,
        0x510E527Fu, 0x9B05688Cu, 0x1F83D9ABu, 0x5BE0CD19u,
    ];

    // The seven rows are precomputed applications of the BLAKE3 message permutation.
    private static ReadOnlySpan<byte> MessageSchedule =>
    [
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15,
         2,  6,  3, 10,  7,  0,  4, 13,  1, 11, 12,  5,  9, 14, 15,  8,
         3,  4, 10, 12, 13,  2,  7, 14,  6,  5,  9,  0, 11, 15,  8,  1,
        10,  7, 12,  9, 14,  3, 13, 15,  4,  0, 11,  2,  5,  8,  1,  6,
        12, 13,  9, 11, 15, 10, 14,  8,  7,  2,  5,  3,  0,  1,  6,  4,
         9, 14, 11,  5,  8, 12, 15,  1, 13,  3,  0, 10,  2,  6,  4,  7,
        11, 15,  5,  0,  1,  9,  8,  6, 14, 10,  2, 12,  3,  4,  7, 13,
    ];

    private static readonly Vector512<uint> LaneOffsets16 =
        Vector512.Create(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u, 9u, 10u, 11u, 12u, 13u, 14u, 15u);

    [SkipLocalsInit]
    internal static void Compress(
        ReadOnlySpan<uint> chainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> output)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            CompressVector128(chainingValue, blockWords, counter, blockLength, flags, output);
        }
        else
        {
            CompressScalar(chainingValue, blockWords, counter, blockLength, flags, output);
        }
    }

    [SkipLocalsInit]
    internal static void HashSingleChunk(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.Length > ChunkLength)
        {
            throw new ArgumentOutOfRangeException(nameof(input));
        }

        Span<uint> chainingValue = stackalloc uint[8];
        Span<uint> blockWords = stackalloc uint[16];
        Span<uint> compressionOutput = stackalloc uint[16];
        InitializationVector.CopyTo(chainingValue);
        var blocksCompressed = 0;

        while (input.Length > BlockLength)
        {
            BytesToWords(input[..BlockLength], blockWords);
            Compress(
                chainingValue,
                blockWords,
                0,
                BlockLength,
                blocksCompressed == 0 ? ChunkStart : 0,
                compressionOutput);
            compressionOutput[..8].CopyTo(chainingValue);
            blocksCompressed++;
            input = input[BlockLength..];
        }

        Span<byte> finalBlock = stackalloc byte[BlockLength];
        finalBlock.Clear();
        input.CopyTo(finalBlock);
        BytesToWords(finalBlock, blockWords);
        var flags = ChunkEnd | Root;
        if (blocksCompressed == 0)
        {
            flags |= ChunkStart;
        }

        Span<byte> outputBlock = stackalloc byte[BlockLength];
        ulong outputCounter = 0;
        while (!output.IsEmpty)
        {
            Compress(chainingValue, blockWords, outputCounter, (uint)input.Length, flags, compressionOutput);
            WordsToBytes(compressionOutput, outputBlock);
            var take = Math.Min(output.Length, outputBlock.Length);
            outputBlock[..take].CopyTo(output);
            output = output[take..];
            outputCounter++;
        }
    }

    [SkipLocalsInit]
    private static void CompressScalar(
        ReadOnlySpan<uint> chainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> output)
    {
        Span<uint> state = stackalloc uint[16];
        chainingValue.CopyTo(state);
        InitializationVector[..4].CopyTo(state[8..]);
        state[12] = (uint)counter;
        state[13] = (uint)(counter >> 32);
        state[14] = blockLength;
        state[15] = flags;

        var schedule = MessageSchedule;
        for (var round = 0; round < 7; round++)
        {
            var row = schedule.Slice(round * 16, 16);
            Round(state, blockWords, row);
        }

        for (var index = 0; index < 8; index++)
        {
            output[index] = state[index] ^ state[index + 8];
            output[index + 8] = state[index + 8] ^ chainingValue[index];
        }
    }

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
        (row2 ^ Vector128.Create(chainingValue[0], chainingValue[1], chainingValue[2], chainingValue[3])).CopyTo(output[8..]);
        (row3 ^ Vector128.Create(chainingValue[4], chainingValue[5], chainingValue[6], chainingValue[7])).CopyTo(output[12..]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BytesToWords(ReadOnlySpan<byte> bytes, Span<uint> words)
    {
        for (var index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(index * sizeof(uint), sizeof(uint)));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WordsToBytes(ReadOnlySpan<uint> words, Span<byte> bytes)
    {
        for (var index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(index * sizeof(uint), sizeof(uint)), words[index]);
        }
    }

    /// <summary>
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
        if (!Vector512.IsHardwareAccelerated || input.Length < Degree * ChunkLength || output.Length < Degree * 8)
        {
            return false;
        }

        var inputWords = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(input[..(Degree * ChunkLength)]);
        Span<Vector512<uint>> chainingValues = stackalloc Vector512<uint>[8];
        Span<Vector512<uint>> message = stackalloc Vector512<uint>[16];
        for (var index = 0; index < chainingValues.Length; index++)
        {
            chainingValues[index] = Vector512.Create(keyWords[index]);
        }

        var counterLow = Vector512.Create((uint)chunkCounter) + LaneOffsets16;
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
            for (var word = 0; word < 16; word++)
            {
                message[word] = LoadTransposed16(inputWords, wordOffset + word);
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

            Compress16(chainingValues, message, counterLow, counterHigh, Vector512.Create(blockFlags));
        }

        Span<uint> transposedOutput = stackalloc uint[Degree * 8];
        for (var word = 0; word < 8; word++)
        {
            chainingValues[word].CopyTo(transposedOutput.Slice(word * Degree, Degree));
        }

        for (var lane = 0; lane < Degree; lane++)
        {
            for (var word = 0; word < 8; word++)
            {
                output[(lane * 8) + word] = transposedOutput[(word * Degree) + lane];
            }
        }

        return true;
    }

    /// <summary>
    /// Hashes a platform-sized group of complete chunks in parallel. This is eight chunks on
    /// current 256-bit runtimes and four chunks on 128-bit runtimes.
    /// </summary>
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

    /// <summary>Reduces a power-of-two group of chunk CVs to one subtree CV.</summary>
    internal static void ReduceChunkChainingValues(
        Span<uint> chainingValues,
        int chunkCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        if (chunkCount is < 2 or > 16 || !System.Numerics.BitOperations.IsPow2(chunkCount))
        {
            throw new ArgumentOutOfRangeException(nameof(chunkCount));
        }

        while (chunkCount > 2)
        {
            var parentCount = chunkCount / 2;
            if (parentCount <= Vector<uint>.Count)
            {
                CompressParentsVector(chainingValues, parentCount, keyWords, flags);
            }
            else
            {
                CompressParentsScalar(chainingValues, parentCount, keyWords, flags);
            }

            chunkCount = parentCount;
        }

        Span<uint> compressionOutput = stackalloc uint[16];
        Compress(keyWords, chainingValues[..16], 0, BlockLength, flags | Parent, compressionOutput);
        compressionOutput[..8].CopyTo(chainingValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Round(Span<uint> state, ReadOnlySpan<uint> message, ReadOnlySpan<byte> schedule)
    {
        Mix(state, 0, 4, 8, 12, message[schedule[0]], message[schedule[1]]);
        Mix(state, 1, 5, 9, 13, message[schedule[2]], message[schedule[3]]);
        Mix(state, 2, 6, 10, 14, message[schedule[4]], message[schedule[5]]);
        Mix(state, 3, 7, 11, 15, message[schedule[6]], message[schedule[7]]);
        Mix(state, 0, 5, 10, 15, message[schedule[8]], message[schedule[9]]);
        Mix(state, 1, 6, 11, 12, message[schedule[10]], message[schedule[11]]);
        Mix(state, 2, 7, 8, 13, message[schedule[12]], message[schedule[13]]);
        Mix(state, 3, 4, 9, 14, message[schedule[14]], message[schedule[15]]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(Span<uint> state, int a, int b, int c, int d, uint messageX, uint messageY)
    {
        state[a] = unchecked(state[a] + state[b] + messageX);
        state[d] = BitOperations.RotateRight(state[d] ^ state[a], 16);
        state[c] = unchecked(state[c] + state[d]);
        state[b] = BitOperations.RotateRight(state[b] ^ state[c], 12);
        state[a] = unchecked(state[a] + state[b] + messageY);
        state[d] = BitOperations.RotateRight(state[d] ^ state[a], 8);
        state[c] = unchecked(state[c] + state[d]);
        state[b] = BitOperations.RotateRight(state[b] ^ state[c], 7);
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
        row3 = RotateRight(row3 ^ row0, 16);
        row2 += row3;
        row1 = RotateRight(row1 ^ row2, 12);
        row0 = row0 + row1 + messageY;
        row3 = RotateRight(row3 ^ row0, 8);
        row2 += row3;
        row1 = RotateRight(row1 ^ row2, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight(Vector128<uint> value, byte count) =>
        Vector128.ShiftRightLogical(value, count) | Vector128.ShiftLeft(value, 32 - count);

    [SkipLocalsInit]
    private static void Compress16(
        Span<Vector512<uint>> chainingValues,
        ReadOnlySpan<Vector512<uint>> message,
        Vector512<uint> counterLow,
        Vector512<uint> counterHigh,
        Vector512<uint> flags)
    {
        var v0 = chainingValues[0];
        var v1 = chainingValues[1];
        var v2 = chainingValues[2];
        var v3 = chainingValues[3];
        var v4 = chainingValues[4];
        var v5 = chainingValues[5];
        var v6 = chainingValues[6];
        var v7 = chainingValues[7];
        var v8 = Vector512.Create(0x6A09E667u);
        var v9 = Vector512.Create(0xBB67AE85u);
        var v10 = Vector512.Create(0x3C6EF372u);
        var v11 = Vector512.Create(0xA54FF53Au);
        var v12 = counterLow;
        var v13 = counterHigh;
        var v14 = Vector512.Create((uint)BlockLength);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(
        ref Vector512<uint> a,
        ref Vector512<uint> b,
        ref Vector512<uint> c,
        ref Vector512<uint> d,
        Vector512<uint> messageX,
        Vector512<uint> messageY)
    {
        a = a + b + messageX;
        d = RotateRight(d ^ a, 16);
        c += d;
        b = RotateRight(b ^ c, 12);
        a = a + b + messageY;
        d = RotateRight(d ^ a, 8);
        c += d;
        b = RotateRight(b ^ c, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> RotateRight(Vector512<uint> value, byte count) =>
        Vector512.ShiftRightLogical(value, count) | Vector512.ShiftLeft(value, 32 - count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> LoadTransposed16(ReadOnlySpan<uint> input, int wordOffset) =>
        Vector512.Create(
            input[wordOffset], input[wordOffset + 256], input[wordOffset + 512], input[wordOffset + 768],
            input[wordOffset + 1024], input[wordOffset + 1280], input[wordOffset + 1536], input[wordOffset + 1792],
            input[wordOffset + 2048], input[wordOffset + 2304], input[wordOffset + 2560], input[wordOffset + 2816],
            input[wordOffset + 3072], input[wordOffset + 3328], input[wordOffset + 3584], input[wordOffset + 3840]);

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

    [SkipLocalsInit]
    private static void CompressParentsScalar(
        Span<uint> childChainingValues,
        int parentCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        Span<uint> compressionOutput = stackalloc uint[16];
        for (var parent = 0; parent < parentCount; parent++)
        {
            Compress(
                keyWords,
                childChainingValues.Slice(parent * 16, 16),
                0,
                BlockLength,
                flags | Parent,
                compressionOutput);
            compressionOutput[..8].CopyTo(childChainingValues.Slice(parent * 8, 8));
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
        d = RotateRight(d ^ a, 16);
        c += d;
        b = RotateRight(b ^ c, 12);
        a = a + b + messageY;
        d = RotateRight(d ^ a, 8);
        c += d;
        b = RotateRight(b ^ c, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> RotateRight(Vector<uint> value, int count) =>
        Vector.ShiftRightLogical(value, count) | Vector.ShiftLeft(value, 32 - count);
}
