// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blake3;

internal static partial class Blake3ManagedCore
{
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

}
