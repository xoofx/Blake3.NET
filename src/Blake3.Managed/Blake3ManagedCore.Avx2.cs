// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Blake3;

internal static partial class Blake3ManagedCore
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LoadAndTranspose8(
        ReadOnlySpan<uint> inputWords,
        int wordOffset,
        Span<Vector<uint>> message)
    {
        ref var inputWord = ref MemoryMarshal.GetReference(inputWords);
        ref var messageWord = ref MemoryMarshal.GetReference(message);
        for (nuint lane = 0; lane < 8; lane++)
        {
            var inputOffset = (nuint)wordOffset + (lane * 256);
            Unsafe.Add(ref messageWord, lane) = Vector256.AsVector(
                Vector256.LoadUnsafe(ref inputWord, inputOffset));
            Unsafe.Add(ref messageWord, lane + 8) = Vector256.AsVector(
                Vector256.LoadUnsafe(ref inputWord, inputOffset + 8));
        }

        Transpose8(ref messageWord);
        Transpose8(ref Unsafe.Add(ref messageWord, 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTransposed8(Span<Vector<uint>> chainingValues, Span<uint> output)
    {
        ref var chainingValue = ref MemoryMarshal.GetReference(chainingValues);
        ref var outputWord = ref MemoryMarshal.GetReference(output);
        Transpose8(ref chainingValue);
        for (nuint lane = 0; lane < 8; lane++)
        {
            Vector256.AsVector256(Unsafe.Add(ref chainingValue, lane)).StoreUnsafe(ref outputWord, lane * 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Transpose8(ref Vector<uint> first)
    {
        var v0 = Vector256.AsVector256(first);
        var v1 = Vector256.AsVector256(Unsafe.Add(ref first, 1));
        var v2 = Vector256.AsVector256(Unsafe.Add(ref first, 2));
        var v3 = Vector256.AsVector256(Unsafe.Add(ref first, 3));
        var v4 = Vector256.AsVector256(Unsafe.Add(ref first, 4));
        var v5 = Vector256.AsVector256(Unsafe.Add(ref first, 5));
        var v6 = Vector256.AsVector256(Unsafe.Add(ref first, 6));
        var v7 = Vector256.AsVector256(Unsafe.Add(ref first, 7));

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

        first = Vector256.AsVector(Avx2.Permute2x128(abcd0, efgh0, 0x20));
        Unsafe.Add(ref first, 1) = Vector256.AsVector(Avx2.Permute2x128(abcd1, efgh1, 0x20));
        Unsafe.Add(ref first, 2) = Vector256.AsVector(Avx2.Permute2x128(abcd2, efgh2, 0x20));
        Unsafe.Add(ref first, 3) = Vector256.AsVector(Avx2.Permute2x128(abcd3, efgh3, 0x20));
        Unsafe.Add(ref first, 4) = Vector256.AsVector(Avx2.Permute2x128(abcd0, efgh0, 0x31));
        Unsafe.Add(ref first, 5) = Vector256.AsVector(Avx2.Permute2x128(abcd1, efgh1, 0x31));
        Unsafe.Add(ref first, 6) = Vector256.AsVector(Avx2.Permute2x128(abcd2, efgh2, 0x31));
        Unsafe.Add(ref first, 7) = Vector256.AsVector(Avx2.Permute2x128(abcd3, efgh3, 0x31));
    }
}
