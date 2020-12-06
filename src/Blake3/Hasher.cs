// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blake3
{
    /// <summary>
    /// An incremental hash state that can accept any number of writes.
    /// </summary>
    /// <remarks>
    /// Performance note: The <see cref="Update{T}"/> and <see cref="UpdateWithJoin{T}"/> methods perform poorly when the caller's input buffer is small.
    /// See their method docs below. A 16 KiB buffer is large enough to leverage all currently supported SIMD instruction sets.
    /// </remarks>
    public unsafe struct Hasher : IDisposable
    {
        private const string DllName = "blake3_dotnet";
        private void* _hasher;
        /// <summary>
        ///  Arbitrary limit to switch to a preemptive version.
        /// </summary>
        private const int LimitPreemptive = 64 * 1024;

        private Hasher(void* hasher)
        {
            _hasher = hasher;
        }

        /// <summary>
        /// The default hash function.
        /// </summary>
        /// <param name="input">The input data to hash.</param>
        /// <returns>The calculated 512-bit/32-byte hash.</returns>
        /// <remarks>
        /// For an incremental version that accepts multiple writes <see cref="Update{T}"/>
        /// This function is always single-threaded. For multi-threading support <see cref="UpdateWithJoin"/> 
        /// </remarks>
        [SkipLocalsInit]
        public static Hash Hash(ReadOnlySpan<byte> input)
        {
            var hash = new Hash();
            fixed (void* ptr = input)
            {
                var size = input.Length;
                if (size <= LimitPreemptive)
                {
                    blake3_hash(ptr, (void*) size, &hash);
                }
                else
                {
                    blake3_hash_preemptive(ptr, (void*)size, &hash);
                }
            }
            return hash;
        }

        /// <summary>
        /// The default hash function.
        /// </summary>
        /// <param name="input">The input data to hash.</param>
        /// <param name="output">The output hash.</param>
        /// <remarks>
        /// For an incremental version that accepts multiple writes <see cref="Update{T}"/>
        /// This function is always single-threaded. For multi-threading support <see cref="UpdateWithJoin"/> 
        /// </remarks>
        public static void Hash(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (output.Length == 32)
            {
                fixed (void* ptrOut = output)
                fixed (void* ptr = input)
                {
                    var size = input.Length;
                    if (size <= LimitPreemptive)
                    {
                        blake3_hash(ptr, (void*) size, ptrOut);
                    }
                    else
                    {
                        blake3_hash_preemptive(ptr, (void*) size, ptrOut);
                    }
                }
            }
            else
            {
                using var hasher = New();
                hasher.Update(input);
                hasher.Finalize(output);
            }
        }

        /// <summary>
        /// Dispose this instance.
        /// </summary>
        public void Dispose()
        {
            if (_hasher != null) blake3_delete(_hasher);
            _hasher = null;
        }

        /// <summary>
        /// Reset the Hasher to its initial state.
        /// </summary>
        /// <remarks>
        /// This is functionally the same as overwriting the Hasher with a new one, using the same key or context string if any.
        /// However, depending on how much inlining the optimizer does, moving a Hasher might copy its entire CV stack, most of which is useless uninitialized bytes.
        /// This methods avoids that copy.
        /// </remarks>
        public void Reset()
        {
            if (_hasher == null) ThrowNullReferenceException();
            blake3_reset(_hasher);
        }

        /// <summary>
        /// Add input bytes to the hash state. You can call this any number of times.
        /// </summary>
        /// <param name="data">The input data byte buffer to hash.</param>
        /// <remarks>
        /// This method is always single-threaded. For multi-threading support, see <see cref="UpdateWithJoin"/> below.
        ///
        /// Note that the degree of SIMD parallelism that update can use is limited by the size of this input buffer.
        /// The 8 KiB buffer currently used by std::io::copy is enough to leverage AVX2, for example, but not enough to leverage AVX-512.
        /// A 16 KiB buffer is large enough to leverage all currently supported SIMD instruction sets.
        /// </remarks>
        public void Update(ReadOnlySpan<byte> data)
        {
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                FastUpdate(_hasher, ptr, data.Length);
            }
        }

        /// <summary>
        /// Add input data to the hash state. You can call this any number of times.
        /// </summary>
        /// <typeparam name="T">Type of the data</typeparam>
        /// <param name="data">The data span to hash.</param>
        /// <remarks>
        /// This method is always single-threaded. For multi-threading support, see <see cref="UpdateWithJoin"/> below.
        ///
        /// Note that the degree of SIMD parallelism that update can use is limited by the size of this input buffer.
        /// The 8 KiB buffer currently used by std::io::copy is enough to leverage AVX2, for example, but not enough to leverage AVX-512.
        /// A 16 KiB buffer is large enough to leverage all currently supported SIMD instruction sets.
        /// </remarks>
        public void Update<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                FastUpdate(_hasher, ptr, data.Length * sizeof(T));
            }
        }

        /// <summary>
        /// Add input bytes to the hash state, as with update, but potentially using multi-threading.
        /// </summary>
        /// <param name="data">The input byte buffer.</param>
        /// <remarks>
        /// To get any performance benefit from multi-threading, the input buffer size needs to be very large.
        /// As a rule of thumb on x86_64, there is no benefit to multi-threading inputs less than 128 KiB.
        /// Other platforms have different thresholds, and in general you need to benchmark your specific use case.
        /// Where possible, memory mapping an entire input file is recommended, to take maximum advantage of multi-threading without needing to tune a specific buffer size.
        /// Where memory mapping is not possible, good multi-threading performance requires doing IO on a background thread, to avoid sleeping all your worker threads while the input buffer is (serially) refilled.
        /// This is quite complicated compared to memory mapping.
        /// </remarks>
        public void UpdateWithJoin(ReadOnlySpan<byte> data)
        {
            if (data == null) ThrowArgumentNullException();
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                blake3_update_with_join(_hasher, ptr, (void*)data.Length);
            }
        }

        /// <summary>
        /// Add input data span to the hash state, as with update, but potentially using multi-threading.
        /// </summary>
        /// <param name="data">The input data buffer.</param>
        /// <remarks>
        /// To get any performance benefit from multi-threading, the input buffer size needs to be very large.
        /// As a rule of thumb on x86_64, there is no benefit to multi-threading inputs less than 128 KiB.
        /// Other platforms have different thresholds, and in general you need to benchmark your specific use case.
        /// Where possible, memory mapping an entire input file is recommended, to take maximum advantage of multi-threading without needing to tune a specific buffer size.
        /// Where memory mapping is not possible, good multi-threading performance requires doing IO on a background thread, to avoid sleeping all your worker threads while the input buffer is (serially) refilled.
        /// This is quite complicated compared to memory mapping.
        /// </remarks>
        public void UpdateWithJoin<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                void* size = (void*) (IntPtr) (data.Length * sizeof(T));
                blake3_update_with_join(_hasher, ptr, size);
            }
        }

        /// <summary>
        /// Finalize the hash state and return the Hash of the input.
        /// </summary>
        /// <returns>The calculated 512-bit/32-byte hash.</returns>
        /// <remarks>
        /// This method is idempotent. Calling it twice will give the same result. You can also add more input and finalize again.
        /// </remarks>
        [SkipLocalsInit]
#pragma warning disable 465
        public Hash Finalize()
#pragma warning restore 465
        {
            var hash = new Hash();
            blake3_finalize(_hasher, &hash);
            return hash;
        }

        /// <summary>
        /// Finalize the hash state to the output span, which can supply any number of output bytes.
        /// </summary>
        /// <param name="hash">The output hash, which can supply any number of output bytes.</param>
        /// <remarks>
        /// This method is idempotent. Calling it twice will give the same result. You can also add more input and finalize again.
        /// </remarks>
        public void Finalize(Span<byte> hash)
        {
            if (_hasher == null) ThrowNullReferenceException();
            ref var pData = ref MemoryMarshal.GetReference(hash);
            fixed (void* ptr = &pData)
            {
                var size = hash.Length;
                if (size == Blake3.Hash.Size)
                {
                    blake3_finalize(_hasher, ptr);
                }
                else
                {
                    blake3_finalize_xof(_hasher, ptr, (void*)(IntPtr)hash.Length);
                }
            }
        }

        /// <summary>
        /// Construct a new Hasher for the regular hash function.
        /// </summary>
        /// <returns>A new instance of the hasher</returns>
        /// <remarks>
        /// The struct returned needs to be disposed explicitly.
        /// </remarks>
        public static Hasher New()
        {
            return new Hasher(blake3_new());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FastUpdate(void* hasher, void* ptr, long size)
        {
            if (size <= LimitPreemptive)
            {
                blake3_update(hasher, ptr, (void*)size);
            }
            else
            {
                blake3_update_preemptive(hasher, ptr, (void*)size);
            }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNullReferenceException()
        {
            throw new NullReferenceException("The Hasher is not initialized or already destroyed.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentNullException()
        {
            // ReSharper disable once NotResolvedInText
            throw new ArgumentNullException("data");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentOutOfRange(int size)
        {
            // ReSharper disable once NotResolvedInText
            throw new ArgumentOutOfRangeException("output", $"Invalid size {size} of the output buffer. Expecting >= 32");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void* blake3_new();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blake3_hash(void* ptr, void* size, void* ptrOut);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "blake3_hash")]
        private static extern void blake3_hash_preemptive(void* ptr, void* size, void* ptrOut);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blake3_delete(void* hasher);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blake3_reset(void* hasher);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blake3_update(void* hasher, void* ptr, void* size);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "blake3_update")]
        private static extern void blake3_update_preemptive(void* hasher, void* ptr, void* size);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void blake3_update_with_join(void* hasher, void* ptr, void* size);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blake3_finalize(void* hasher, void* ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        private static extern void blake3_finalize_xof(void* hasher, void* ptr, void* size);
    }
}
