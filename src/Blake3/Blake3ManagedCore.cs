// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

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

    [SkipLocalsInit]
    internal static void Compress(
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
}
