// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Blake3;

/// <summary>
/// Portable implementation of the BLAKE3 compression function.
/// </summary>
internal static partial class Blake3ManagedCore
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

    internal static int SimdChunkDegree
    {
        get
        {
            if (Avx512F.IsSupported)
            {
                return 16;
            }

            var degree = Vector<uint>.Count;
            return Vector.IsHardwareAccelerated && degree is 4 or 8 ? degree : 1;
        }
    }

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

    private static readonly Vector128<byte> RotateRight8Mask128 =
        Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12);

    private static readonly Vector128<byte> RotateRight16Mask128 =
        Vector128.Create((byte)2, 3, 0, 1, 6, 7, 4, 5, 10, 11, 8, 9, 14, 15, 12, 13);

    private static readonly Vector256<byte> RotateRight8Mask256 =
        Vector256.Create(RotateRight8Mask128, RotateRight8Mask128);

    private static readonly Vector256<byte> RotateRight16Mask256 =
        Vector256.Create(RotateRight16Mask128, RotateRight16Mask128);

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
        InitializationVector.CopyTo(chainingValue);
        var blocksCompressed = 0;

        while (input.Length > BlockLength)
        {
            var blockFlags = blocksCompressed == 0 ? ChunkStart : 0;
            if (Sse41.IsSupported)
            {
                // x86 is little-endian, so complete blocks can be loaded directly without staging words.
                Compress(
                    chainingValue,
                    System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(input[..BlockLength]),
                    0,
                    BlockLength,
                    blockFlags,
                    chainingValue);
            }
            else
            {
                BytesToWords(input[..BlockLength], blockWords);
                Compress(chainingValue, blockWords, 0, BlockLength, blockFlags, chainingValue);
            }

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

        if (output.Length == OutputLength)
        {
            Compress(chainingValue, blockWords, 0, (uint)input.Length, flags, chainingValue);
            WordsToBytes(chainingValue, output);
            return;
        }

        Span<uint> compressionOutput = stackalloc uint[16];
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

    /// <summary>
    /// Hashes a multi-chunk input while preserving a SIMD-width frontier of chaining values.
    /// </summary>
    [SkipLocalsInit]
    internal static void HashAllAtOnce(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.Length <= ChunkLength)
        {
            throw new ArgumentOutOfRangeException(nameof(input));
        }

        Span<uint> chainingValues = stackalloc uint[16 * 8];
        var chainingValueCount = CompressSubtreeWide(input, InitializationVector, 0, 0, chainingValues);
        while (chainingValueCount > 2)
        {
            chainingValueCount = CompressParentsParallel(
                chainingValues,
                chainingValueCount,
                InitializationVector,
                0);
        }

        Span<uint> outputWords = stackalloc uint[16];
        Span<byte> outputBlock = stackalloc byte[BlockLength];
        ulong outputBlockCounter = 0;
        while (!output.IsEmpty)
        {
            Compress(
                InitializationVector,
                chainingValues[..16],
                outputBlockCounter,
                BlockLength,
                Parent | Root,
                outputWords);
            WordsToBytes(outputWords, outputBlock);
            var take = Math.Min(output.Length, outputBlock.Length);
            outputBlock[..take].CopyTo(output);
            output = output[take..];
            outputBlockCounter = unchecked(outputBlockCounter + 1);
        }
    }

    [SkipLocalsInit]
    private static int CompressSubtreeWide(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        var simdDegree = SimdChunkDegree;
        if (input.Length <= simdDegree * ChunkLength)
        {
            return CompressChunksParallel(input, keyWords, chunkCounter, flags, output);
        }

        var leftLength = LeftSubtreeLength(input.Length);
        var degree = Math.Max(simdDegree, 2);
        Span<uint> childChainingValues = stackalloc uint[2 * 16 * 8];
        var leftOutput = childChainingValues[..(degree * 8)];
        var rightOutput = childChainingValues.Slice(degree * 8, degree * 8);
        var leftCount = CompressSubtreeWide(
            input[..leftLength],
            keyWords,
            chunkCounter,
            flags,
            leftOutput);
        var rightCount = CompressSubtreeWide(
            input[leftLength..],
            keyWords,
            chunkCounter + (ulong)(leftLength / ChunkLength),
            flags,
            rightOutput);

        if (leftCount == 1)
        {
            leftOutput[..8].CopyTo(output);
            rightOutput[..8].CopyTo(output[8..]);
            return 2;
        }

        var childCount = leftCount + rightCount;
        var outputCount = CompressParentsParallel(childChainingValues, childCount, keyWords, flags);
        childChainingValues[..(outputCount * 8)].CopyTo(output);
        return outputCount;
    }

    [SkipLocalsInit]
    private static int CompressChunksParallel(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        var completeChunkCount = input.Length / ChunkLength;
        var chunkCount = (input.Length + ChunkLength - 1) / ChunkLength;
        var hashedChunkCount = 0;

        if (completeChunkCount == 16 &&
            TryHash16Chunks(input, keyWords, chunkCounter, flags, output))
        {
            hashedChunkCount = 16;
        }
        else if (completeChunkCount >= Vector<uint>.Count)
        {
            hashedChunkCount = TryHashVectorChunks(input, keyWords, chunkCounter, flags, output);
        }

        for (var chunk = hashedChunkCount; chunk < completeChunkCount; chunk++)
        {
            HashChunkChainingValue(
                input.Slice(chunk * ChunkLength, ChunkLength),
                keyWords,
                chunkCounter + (ulong)chunk,
                flags,
                output.Slice(chunk * 8, 8));
        }

        if (completeChunkCount != chunkCount)
        {
            HashChunkChainingValue(
                input[(completeChunkCount * ChunkLength)..],
                keyWords,
                chunkCounter + (ulong)completeChunkCount,
                flags,
                output.Slice(completeChunkCount * 8, 8));
        }

        return chunkCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LeftSubtreeLength(int inputLength)
    {
        var halfLength = (uint)(((long)inputLength + 1) / 2);
        return (int)BitOperations.RoundUpToPowerOf2(halfLength);
    }

    [SkipLocalsInit]
    internal static void HashChunkGroup(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        int chunkCount,
        Span<uint> output)
    {
        if (chunkCount == 1)
        {
            HashChunkChainingValue(input, keyWords, chunkCounter, flags, output);
            return;
        }

        Span<uint> chainingValues = stackalloc uint[16 * 8];
        var usedChainingValues = chainingValues[..(chunkCount * 8)];
        var hashedChunkCount = chunkCount == 16 &&
            TryHash16Chunks(input, keyWords, chunkCounter, flags, usedChainingValues)
                ? 16
                : TryHashVectorChunks(input, keyWords, chunkCounter, flags, usedChainingValues);

        if (hashedChunkCount != chunkCount)
        {
            for (var chunk = 0; chunk < chunkCount; chunk++)
            {
                HashChunkChainingValue(
                    input.Slice(chunk * ChunkLength, ChunkLength),
                    keyWords,
                    chunkCounter + (ulong)chunk,
                    flags,
                    usedChainingValues.Slice(chunk * 8, 8));
            }
        }

        ReduceChunkChainingValues(usedChainingValues, chunkCount, keyWords, flags);
        usedChainingValues[..8].CopyTo(output);
    }

    [SkipLocalsInit]
    private static void HashChunkChainingValue(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<uint> keyWords,
        ulong chunkCounter,
        uint flags,
        Span<uint> output)
    {
        Span<uint> chainingValue = stackalloc uint[8];
        Span<uint> blockWords = stackalloc uint[16];
        keyWords.CopyTo(chainingValue);
        var blocksCompressed = 0;

        while (input.Length > BlockLength)
        {
            var blockFlags = flags;
            if (blocksCompressed == 0)
            {
                blockFlags |= ChunkStart;
            }

            if (Sse41.IsSupported)
            {
                Compress(
                    chainingValue,
                    System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(input[..BlockLength]),
                    chunkCounter,
                    BlockLength,
                    blockFlags,
                    chainingValue);
            }
            else
            {
                BytesToWords(input[..BlockLength], blockWords);
                Compress(chainingValue, blockWords, chunkCounter, BlockLength, blockFlags, chainingValue);
            }

            blocksCompressed++;
            input = input[BlockLength..];
        }

        Span<byte> finalBlock = stackalloc byte[BlockLength];
        finalBlock.Clear();
        input.CopyTo(finalBlock);
        BytesToWords(finalBlock, blockWords);
        var finalFlags = flags | ChunkEnd;
        if (blocksCompressed == 0)
        {
            finalFlags |= ChunkStart;
        }

        Compress(chainingValue, blockWords, chunkCounter, (uint)input.Length, finalFlags, output);
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
            if (output.Length >= 16)
            {
                output[index + 8] = state[index + 8] ^ chainingValue[index];
            }
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
        if (Sse41.IsSupported)
        {
            CompressSse41(chainingValue, blockWords, counter, blockLength, flags, output);
            return;
        }

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

    [SkipLocalsInit]
    private static void CompressSse41(
        ReadOnlySpan<uint> chainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> output)
    {
        ref var chainingValueWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(chainingValue);
        ref var blockWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(blockWords);
        var initialRow0 = Vector128.LoadUnsafe(ref chainingValueWord);
        var initialRow1 = Vector128.LoadUnsafe(ref chainingValueWord, 4);
        var row0 = initialRow0;
        var row1 = initialRow1;
        var row2 = Vector128.Create(0x6A09E667u, 0xBB67AE85u, 0x3C6EF372u, 0xA54FF53Au);
        var row3 = Vector128.Create((uint)counter, (uint)(counter >> 32), blockLength, flags);
        var message0 = Vector128.LoadUnsafe(ref blockWord);
        var message1 = Vector128.LoadUnsafe(ref blockWord, 4);
        var message2 = Vector128.LoadUnsafe(ref blockWord, 8);
        var message3 = Vector128.LoadUnsafe(ref blockWord, 12);

        var permuted0 = Sse.Shuffle(message0.AsSingle(), message1.AsSingle(), 0x88).AsUInt32();
        var permuted1 = Sse.Shuffle(message0.AsSingle(), message1.AsSingle(), 0xDD).AsUInt32();
        Mix(ref row0, ref row1, ref row2, ref row3, permuted0, permuted1);
        DiagonalizeSse41(ref row0, ref row2, ref row3);
        var permuted2 = Sse2.Shuffle(
            Sse.Shuffle(message2.AsSingle(), message3.AsSingle(), 0x88).AsUInt32(),
            0x93);
        var permuted3 = Sse2.Shuffle(
            Sse.Shuffle(message2.AsSingle(), message3.AsSingle(), 0xDD).AsUInt32(),
            0x93);
        Mix(ref row0, ref row1, ref row2, ref row3, permuted2, permuted3);
        UndiagonalizeSse41(ref row0, ref row2, ref row3);
        message0 = permuted0;
        message1 = permuted1;
        message2 = permuted2;
        message3 = permuted3;

        for (var round = 1; round < 7; round++)
        {
            PermuteMessageAndRoundSse41(
                ref row0,
                ref row1,
                ref row2,
                ref row3,
                ref message0,
                ref message1,
                ref message2,
                ref message3);
        }

        ref var outputWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(output);
        (row0 ^ row2).StoreUnsafe(ref outputWord);
        (row1 ^ row3).StoreUnsafe(ref outputWord, 4);
        if (output.Length >= 16)
        {
            (row2 ^ initialRow0).StoreUnsafe(ref outputWord, 8);
            (row3 ^ initialRow1).StoreUnsafe(ref outputWord, 12);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PermuteMessageAndRoundSse41(
        ref Vector128<uint> row0,
        ref Vector128<uint> row1,
        ref Vector128<uint> row2,
        ref Vector128<uint> row3,
        ref Vector128<uint> message0,
        ref Vector128<uint> message1,
        ref Vector128<uint> message2,
        ref Vector128<uint> message3)
    {
        var permuted0 = Sse2.Shuffle(
            Sse.Shuffle(message0.AsSingle(), message1.AsSingle(), 0xD6).AsUInt32(),
            0x39);
        var permuted1 = Sse.Shuffle(message2.AsSingle(), message3.AsSingle(), 0xFA).AsUInt32();
        var temporary = Sse2.Shuffle(message0, 0x0F);
        permuted1 = Sse41.Blend(temporary.AsInt16(), permuted1.AsInt16(), 0xCC).AsUInt32();
        Mix(ref row0, ref row1, ref row2, ref row3, permuted0, permuted1);
        DiagonalizeSse41(ref row0, ref row2, ref row3);

        var permuted2 = Sse2.UnpackLow(message3.AsUInt64(), message1.AsUInt64()).AsUInt32();
        temporary = Sse41.Blend(permuted2.AsInt16(), message2.AsInt16(), 0xC0).AsUInt32();
        permuted2 = Sse2.Shuffle(temporary, 0x78);
        var permuted3 = Sse2.UnpackHigh(message1, message3);
        temporary = Sse2.UnpackLow(message2, permuted3);
        permuted3 = Sse2.Shuffle(temporary, 0x1E);
        Mix(ref row0, ref row1, ref row2, ref row3, permuted2, permuted3);
        UndiagonalizeSse41(ref row0, ref row2, ref row3);

        message0 = permuted0;
        message1 = permuted1;
        message2 = permuted2;
        message3 = permuted3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DiagonalizeSse41(
        ref Vector128<uint> row0,
        ref Vector128<uint> row2,
        ref Vector128<uint> row3)
    {
        row0 = Sse2.Shuffle(row0, 0x93);
        row3 = Sse2.Shuffle(row3, 0x4E);
        row2 = Sse2.Shuffle(row2, 0x39);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UndiagonalizeSse41(
        ref Vector128<uint> row0,
        ref Vector128<uint> row2,
        ref Vector128<uint> row3)
    {
        row0 = Sse2.Shuffle(row0, 0x39);
        row3 = Sse2.Shuffle(row3, 0x4E);
        row2 = Sse2.Shuffle(row2, 0x93);
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
        var useAvx2Transpose = degree == 8 && Avx2.IsSupported;
        for (var block = 0; block < ChunkLength / BlockLength; block++)
        {
            var wordOffset = block * 16;
            if (useAvx2Transpose)
            {
                LoadAndTranspose8(inputWords, wordOffset, message);
            }
            else
            {
                for (var word = 0; word < 16; word++)
                {
                    for (var lane = 0; lane < degree; lane++)
                    {
                        lanes[lane] = inputWords[wordOffset + word + (lane * 256)];
                    }

                    message[word] = new Vector<uint>(lanes);
                }
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

        if (useAvx2Transpose)
        {
            StoreTransposed8(chainingValues, output);
        }
        else
        {
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
    private static int CompressParentsParallel(
        Span<uint> childChainingValues,
        int childCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        var parentCount = childCount / 2;
        if (parentCount < 9 || !TryCompress16Parents(childChainingValues, parentCount, keyWords, flags))
        {
            if (parentCount >= 2 &&
                Vector.IsHardwareAccelerated &&
                Vector<uint>.Count is 4 or 8 &&
                parentCount <= Vector<uint>.Count)
            {
                CompressParentsVector(childChainingValues, parentCount, keyWords, flags);
            }
            else
            {
                CompressParentsScalar(childChainingValues, parentCount, keyWords, flags);
            }
        }

        if ((childCount & 1) != 0)
        {
            childChainingValues.Slice((childCount - 1) * 8, 8)
                .CopyTo(childChainingValues.Slice(parentCount * 8, 8));
            parentCount++;
        }

        return parentCount;
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
