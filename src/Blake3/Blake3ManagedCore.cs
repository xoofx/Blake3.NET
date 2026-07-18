// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Blake3;

/// <summary>
/// Coordinates managed BLAKE3 hashing and dispatches compression work to the best available
/// implementation for the current CPU.
/// </summary>
/// <remarks>
/// Implementations are grouped by partial file: scalar and portable SIMD fallbacks, x86,
/// Arm64, AVX2, and AVX-512. This file contains the shared algorithm and dispatch policy.
/// </remarks>
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

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Compress(
        ReadOnlySpan<uint> chainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> output)
    {
        // Prefer architecture-specific single-block compression, then portable SIMD, then scalar.
        if (AdvSimd.Arm64.IsSupported)
        {
            CompressScalarArm64(chainingValue, blockWords, counter, blockLength, flags, output);
        }
        else if (Sse41.IsSupported)
        {
            ref var chainingValueWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(chainingValue);
            ref var blockWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(blockWords);
            ref var outputWord = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(output);
            CompressSse41(
                ref chainingValueWord,
                ref blockWord,
                counter,
                blockLength,
                flags,
                ref outputWord,
                output.Length);
        }
        else if (Vector128.IsHardwareAccelerated)
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
        var blockFlags = ChunkStart;

        while (input.Length > BlockLength)
        {
            if (BitConverter.IsLittleEndian)
            {
                // Complete little-endian blocks can be loaded directly without staging words.
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

            blockFlags = 0;
            input = input[BlockLength..];
        }

        if (BitConverter.IsLittleEndian)
        {
            blockWords.Clear();
            input.CopyTo(MemoryMarshal.AsBytes(blockWords));
        }
        else
        {
            Span<byte> finalBlock = stackalloc byte[BlockLength];
            finalBlock.Clear();
            input.CopyTo(finalBlock);
            BytesToWords(finalBlock, blockWords);
        }
        var flags = ChunkEnd | Root | blockFlags;

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

        // The 16-way AVX-512 kernel is tried first. The platform-width dispatcher then selects
        // Arm64, AVX2, SSE2, or the portable Vector<T> implementation.
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

            if (BitConverter.IsLittleEndian)
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

        if (BitConverter.IsLittleEndian)
        {
            blockWords.Clear();
            input.CopyTo(MemoryMarshal.AsBytes(blockWords));
        }
        else
        {
            Span<byte> finalBlock = stackalloc byte[BlockLength];
            finalBlock.Clear();
            input.CopyTo(finalBlock);
            BytesToWords(finalBlock, blockWords);
        }
        var finalFlags = flags | ChunkEnd;
        if (blocksCompressed == 0)
        {
            finalFlags |= ChunkStart;
        }

        Compress(chainingValue, blockWords, chunkCounter, (uint)input.Length, finalFlags, output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BytesToWords(ReadOnlySpan<byte> bytes, Span<uint> words)
    {
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.Cast<byte, uint>(bytes)[..words.Length].CopyTo(words);
            return;
        }

        for (var index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(index * sizeof(uint), sizeof(uint)));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WordsToBytes(ReadOnlySpan<uint> words, Span<byte> bytes)
    {
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.AsBytes(words).CopyTo(bytes);
            return;
        }

        for (var index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(index * sizeof(uint), sizeof(uint)), words[index]);
        }
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
            if (parentCount > 4 ||
                !TryCompress4ParentsArm64(chainingValues, parentCount, keyWords, flags))
            {
                if (parentCount <= Vector<uint>.Count)
                {
                    CompressParentsVector(chainingValues, parentCount, keyWords, flags);
                }
                else
                {
                    CompressParentsScalar(chainingValues, parentCount, keyWords, flags);
                }
            }

            chunkCount = parentCount;
        }

        Span<uint> compressionOutput = stackalloc uint[16];
        Compress(keyWords, chainingValues[..16], 0, BlockLength, flags | Parent, compressionOutput);
        compressionOutput[..8].CopyTo(chainingValues);
    }

    [SkipLocalsInit]
    private static int CompressParentsParallel(
        Span<uint> childChainingValues,
        int childCount,
        ReadOnlySpan<uint> keyWords,
        uint flags)
    {
        var parentCount = childCount / 2;

        // Descend from the widest implementation to architecture-specific and portable fallbacks.
        if (parentCount < 9 || !TryCompress16Parents(childChainingValues, parentCount, keyWords, flags))
        {
            var parentsCompressed = TryCompress4ParentsArm64(
                childChainingValues,
                parentCount,
                keyWords,
                flags);
            if (!parentsCompressed)
            {
                parentsCompressed = parentCount >= 2 &&
                                    TryCompress8Parents(childChainingValues, parentCount, keyWords, flags);
            }
            if (!parentsCompressed)
            {
                parentsCompressed = TryCompressParentsX86(childChainingValues, parentCount, keyWords, flags);
            }

            if (!parentsCompressed)
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
        }

        if ((childCount & 1) != 0)
        {
            childChainingValues.Slice((childCount - 1) * 8, 8)
                .CopyTo(childChainingValues.Slice(parentCount * 8, 8));
            parentCount++;
        }

        return parentCount;
    }

}
