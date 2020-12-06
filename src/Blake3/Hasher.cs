using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blake3
{
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

        [SkipLocalsInit]
        public static Hash HashData(ReadOnlySpan<byte> input)
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

        public static void HashData(ReadOnlySpan<byte> input, Span<byte> output)
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
                    blake3_hash_preemptive(ptr, (void*)size, ptrOut);
                }
            }
        }

        public void Dispose()
        {
            if (_hasher != null) blake3_delete(_hasher);
            _hasher = null;
        }

        public void Reset()
        {
            if (_hasher == null) ThrowNullReferenceException();
            blake3_reset(_hasher);
        }

        public void Update(byte[] data)
        {
            if (data == null) ThrowArgumentNullException();
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                FastUpdate(_hasher, ptr, data.Length);
            }
        }

        public void Update<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                FastUpdate(_hasher, ptr, data.Length * sizeof(T));
            }
        }

        public void UpdateWithJoin(byte[] data)
        {
            if (data == null) ThrowArgumentNullException();
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                blake3_update_with_join(_hasher, ptr, (void*)data.Length);
            }
        }

        public void UpdateWithJoin<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            if (_hasher == null) ThrowNullReferenceException();
            fixed (void* ptr = data)
            {
                void* size = (void*) (IntPtr) (data.Length * sizeof(T));
                blake3_update_with_join(_hasher, ptr, size);
            }
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

        [SkipLocalsInit]
#pragma warning disable 465
        public Hash Finalize()
#pragma warning restore 465
        {
            var hash = new Hash();
            blake3_finalize(_hasher, &hash);
            return hash;
        }

        public void Finalize(Span<byte> hash)
        {
            if (_hasher == null) ThrowNullReferenceException();
            ref var pData = ref MemoryMarshal.GetReference(hash);
            fixed (void* ptr = &pData)
            {

                blake3_finalize_xof(_hasher, ptr, (void*) (IntPtr) hash.Length);
            }
        }

        public static Hasher New()
        {
            return new Hasher(blake3_new());
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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "blake3_finalize_xof")]
        private static extern void blake3_finalize_xof_preemptive(void* hasher, void* ptr, void* size);
    }
}
