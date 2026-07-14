// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Blake3;

/// <summary>
/// A fully managed incremental BLAKE3 hash state that can accept any number of writes.
/// </summary>
/// <remarks>
/// Instances are not thread-safe. Use <see cref="New"/>, <see cref="NewKeyed"/>, or
/// <see cref="NewDeriveKey(string)"/> to construct an instance.
/// </remarks>
public sealed class Hasher : IDisposable
{
    private const int ParallelThresholdBytes = 192 * 1024;
    private const int MinimumParallelBytesPerWorker = 32 * 1024;

    private readonly uint[] _keyWords = new uint[8];
    private readonly uint[] _chunkChainingValue = new uint[8];
    private readonly byte[] _block = new byte[Blake3ManagedCore.BlockLength];
    private readonly uint[] _chainingValueStack = new uint[Blake3ManagedCore.MaximumDepth * 8];
    private readonly uint _flags;

    private ulong _chunkCounter;
    private int _blockLength;
    private int _blocksCompressed;
    private int _stackLength;
    private bool _disposed;

    /// <summary>
    /// Invalid constructor.
    /// </summary>
    [Obsolete("Use New() to create a new instance of Hasher", true)]
    public Hasher()
    {
    }

    private Hasher(ReadOnlySpan<uint> keyWords, uint flags)
    {
        keyWords.CopyTo(_keyWords);
        keyWords.CopyTo(_chunkChainingValue);
        _flags = flags;
    }

    /// <summary>
    /// Calculates the default 256-bit BLAKE3 hash.
    /// </summary>
    /// <param name="input">The bytes to hash.</param>
    /// <returns>The calculated 32-byte hash.</returns>
    public static Hash Hash(ReadOnlySpan<byte> input)
    {
        if (input.Length <= Blake3ManagedCore.ChunkLength)
        {
            var hash = new Hash();
            Blake3ManagedCore.HashSingleChunk(input, hash.AsSpan());
            return hash;
        }

        var result = new Hash();
        Blake3ManagedCore.HashAllAtOnce(input, result.AsSpan());
        return result;
    }

    /// <summary>
    /// Calculates a BLAKE3 extendable output of any length.
    /// </summary>
    /// <param name="input">The bytes to hash.</param>
    /// <param name="output">The destination for the hash output.</param>
    public static void Hash(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.Length <= Blake3ManagedCore.ChunkLength)
        {
            Blake3ManagedCore.HashSingleChunk(input, output);
            return;
        }

        Blake3ManagedCore.HashAllAtOnce(input, output);
    }

    /// <summary>
    /// Constructs a managed hasher for the regular hash function.
    /// </summary>
    /// <returns>A new managed hasher.</returns>
    public static Hasher New() => new(Blake3ManagedCore.InitializationVector, 0);

    /// <summary>
    /// Constructs a managed hasher for the keyed hash function.
    /// </summary>
    /// <param name="key">The 32-byte secret key.</param>
    /// <returns>A new managed keyed hasher.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not exactly 32 bytes.</exception>
    public static Hasher NewKeyed(ReadOnlySpan<byte> key)
    {
        if (key.Length != Blake3ManagedCore.KeyLength)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "Expecting the key to be 32 bytes");
        }

        Span<uint> keyWords = stackalloc uint[8];
        Blake3ManagedCore.BytesToWords(key, keyWords);
        return new Hasher(keyWords, Blake3ManagedCore.KeyedHash);
    }

    /// <summary>
    /// Constructs a managed hasher for the key derivation function.
    /// </summary>
    /// <param name="text">A globally unique, application-specific context string.</param>
    /// <returns>A new managed key derivation hasher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static Hasher NewDeriveKey(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return NewDeriveKeyCore(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Constructs a managed hasher for the key derivation function from a UTF-8 context.
    /// </summary>
    /// <param name="str">A UTF-8 encoded, globally unique, application-specific context string.</param>
    /// <returns>A new managed key derivation hasher.</returns>
    /// <remarks>Invalid UTF-8 is replaced to match the native wrapper's lossy UTF-8 conversion.</remarks>
    public static Hasher NewDeriveKey(ReadOnlySpan<byte> str)
    {
        // The native wrapper converts its byte input with Rust's String::from_utf8_lossy.
        var normalizedContext = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(str));
        return NewDeriveKeyCore(normalizedContext);
    }

    /// <summary>
    /// Resets this instance to its initial state while preserving its mode and key.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Reset()
    {
        ThrowIfDisposed();
        _keyWords.CopyTo(_chunkChainingValue, 0);
        CryptographicOperations.ZeroMemory(_block);
        Array.Clear(_chainingValueStack);
        _chunkCounter = 0;
        _blockLength = 0;
        _blocksCompressed = 0;
        _stackLength = 0;
    }

    /// <summary>
    /// Adds bytes to the hash state.
    /// </summary>
    /// <param name="data">The bytes to hash.</param>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Update(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        Span<uint> chunkChainingValue = stackalloc uint[8];
        Span<uint> parallelChainingValues = data.Length > 4 * Blake3ManagedCore.ChunkLength
            ? stackalloc uint[16 * 8]
            : Span<uint>.Empty;

        while (!data.IsEmpty)
        {
            if (ChunkStateLength == Blake3ManagedCore.ChunkLength)
            {
                CompleteChunkState(chunkChainingValue);
            }

            if (ChunkStateLength == 0 &&
                (_chunkCounter & 15) == 0 &&
                data.Length > 16 * Blake3ManagedCore.ChunkLength &&
                Blake3ManagedCore.TryHash16Chunks(data, _keyWords, _chunkCounter, _flags, parallelChainingValues))
            {
                Blake3ManagedCore.ReduceChunkChainingValues(parallelChainingValues, 16, _keyWords, _flags);
                _chunkCounter += 16;
                AddSubtreeChainingValue(parallelChainingValues[..8], _chunkCounter, 4);
                ResetChunkState(_chunkCounter);
                data = data[(16 * Blake3ManagedCore.ChunkLength)..];
                continue;
            }

            var vectorDegree = System.Numerics.Vector<uint>.Count;
            if (ChunkStateLength == 0 &&
                (_chunkCounter & (ulong)(vectorDegree - 1)) == 0 &&
                data.Length > vectorDegree * Blake3ManagedCore.ChunkLength)
            {
                var degree = Blake3ManagedCore.TryHashVectorChunks(
                    data,
                    _keyWords,
                    _chunkCounter,
                    _flags,
                    parallelChainingValues);
                if (degree > 0)
                {
                    Blake3ManagedCore.ReduceChunkChainingValues(parallelChainingValues, degree, _keyWords, _flags);
                    _chunkCounter += (ulong)degree;
                    AddSubtreeChainingValue(
                        parallelChainingValues[..8],
                        _chunkCounter,
                        System.Numerics.BitOperations.Log2((uint)degree));
                    ResetChunkState(_chunkCounter);
                    data = data[(degree * Blake3ManagedCore.ChunkLength)..];
                    continue;
                }
            }

            var take = Math.Min(Blake3ManagedCore.ChunkLength - ChunkStateLength, data.Length);
            UpdateChunkState(data[..take]);
            data = data[take..];
        }
    }

    /// <summary>
    /// Adds the in-memory bytes of unmanaged values to the hash state.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="data">The values whose in-memory representation is hashed.</param>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Update<T>(ReadOnlySpan<T> data) where T : unmanaged => Update(MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Adds bytes to the hash state, with the same result as <see cref="Update(ReadOnlySpan{byte})"/>.
    /// </summary>
    /// <param name="data">The bytes to hash.</param>
    /// <remarks>Large aligned subtrees are hashed in parallel; smaller inputs use the serial update path.</remarks>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void UpdateWithJoin(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        if (data.Length < ParallelThresholdBytes || Environment.ProcessorCount <= 1)
        {
            Update(data);
            return;
        }

        Span<uint> chunkChainingValue = stackalloc uint[8];
        if (ChunkStateLength != 0)
        {
            var take = Math.Min(Blake3ManagedCore.ChunkLength - ChunkStateLength, data.Length);
            if (take != 0)
            {
                Update(data[..take]);
                data = data[take..];
            }

            if (data.IsEmpty)
            {
                return;
            }

            CompleteChunkState(chunkChainingValue);
        }

        // Keep at least one byte (and therefore the final chunk output) in the ordinary chunk
        // state. Finalization needs that output to apply the root flag exactly as serial Update does.
        var completeChunkCount = (data.Length - 1) / Blake3ManagedCore.ChunkLength;
        var chunkDegree = Blake3ManagedCore.SimdChunkDegree;
        var misalignedChunks = (int)(_chunkCounter & (ulong)(chunkDegree - 1));
        var alignmentChunks = misalignedChunks == 0 ? 0 : chunkDegree - misalignedChunks;
        if (completeChunkCount < alignmentChunks)
        {
            Update(data);
            return;
        }

        var parallelGroupCount = (completeChunkCount - alignmentChunks) / chunkDegree;
        var parallelByteLength = parallelGroupCount * chunkDegree * Blake3ManagedCore.ChunkLength;
        if (parallelGroupCount < 2 || parallelByteLength < ParallelThresholdBytes)
        {
            Update(data);
            return;
        }

        if (alignmentChunks != 0)
        {
            var alignmentByteLength = alignmentChunks * Blake3ManagedCore.ChunkLength;
            ProcessCompleteChunks(data[..alignmentByteLength], alignmentChunks, chunkChainingValue);
            data = data[alignmentByteLength..];
        }

        var subtreeChainingValues = GC.AllocateUninitializedArray<uint>(parallelGroupCount * 8);
        try
        {
            HashChunkGroupsInParallel(
                data[..parallelByteLength],
                chunkDegree,
                parallelGroupCount,
                subtreeChainingValues);

            var subtreeLevel = System.Numerics.BitOperations.Log2((uint)chunkDegree);
            for (var group = 0; group < parallelGroupCount; group++)
            {
                _chunkCounter += (ulong)chunkDegree;
                AddSubtreeChainingValue(
                    subtreeChainingValues.AsSpan(group * 8, 8),
                    _chunkCounter,
                    subtreeLevel);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subtreeChainingValues.AsSpan()));
        }

        ResetChunkState(_chunkCounter);
        Update(data[parallelByteLength..]);
    }

    /// <summary>
    /// Adds the in-memory bytes of unmanaged values to the hash state, with the same result as <see cref="Update{T}"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="data">The values whose in-memory representation is hashed.</param>
    /// <remarks>Large aligned subtrees are hashed in parallel; smaller inputs use the serial update path.</remarks>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void UpdateWithJoin<T>(ReadOnlySpan<T> data) where T : unmanaged =>
        UpdateWithJoin(MemoryMarshal.AsBytes(data));

    /// <summary>
    /// Finalizes the state and returns the default 256-bit output.
    /// </summary>
    /// <returns>The calculated 32-byte hash.</returns>
    /// <remarks>This operation is idempotent and does not prevent subsequent updates.</remarks>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
#pragma warning disable CS0465
    public Hash Finalize()
#pragma warning restore CS0465
    {
        var hash = new Hash();
        Finalize(hash.AsSpan());
        return hash;
    }

    /// <summary>
    /// Finalizes the state into an extendable output of any length.
    /// </summary>
    /// <param name="hash">The output destination.</param>
    /// <remarks>This operation is idempotent and does not prevent subsequent updates.</remarks>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Finalize(Span<byte> hash) => Finalize(0UL, hash);

    /// <summary>
    /// Finalizes the state into an extendable output starting at an arbitrary byte offset.
    /// </summary>
    /// <param name="offset">The byte offset in the BLAKE3 output stream.</param>
    /// <param name="hash">The output destination.</param>
    /// <remarks>This operation is idempotent and does not prevent subsequent updates.</remarks>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Finalize(ulong offset, Span<byte> hash)
    {
        ThrowIfDisposed();

        Span<uint> outputChainingValue = stackalloc uint[8];
        Span<uint> outputBlockWords = stackalloc uint[16];
        GetChunkOutput(outputChainingValue, outputBlockWords, out var outputCounter, out var outputBlockLength, out var outputFlags);

        Span<uint> rightChainingValue = stackalloc uint[8];
        for (var stackIndex = _stackLength - 1; stackIndex >= 0; stackIndex--)
        {
            GetOutputChainingValue(outputChainingValue, outputBlockWords, outputCounter, outputBlockLength, outputFlags, rightChainingValue);
            _keyWords.AsSpan().CopyTo(outputChainingValue);
            _chainingValueStack.AsSpan(stackIndex * 8, 8).CopyTo(outputBlockWords);
            rightChainingValue.CopyTo(outputBlockWords[8..]);
            outputCounter = 0;
            outputBlockLength = Blake3ManagedCore.BlockLength;
            outputFlags = _flags | Blake3ManagedCore.Parent;
        }

        WriteRootOutput(outputChainingValue, outputBlockWords, outputBlockLength, outputFlags, offset, hash);
    }

    /// <summary>
    /// Finalizes the state into an extendable output starting at an arbitrary byte offset.
    /// </summary>
    /// <param name="offset">The non-negative byte offset in the BLAKE3 output stream.</param>
    /// <param name="hash">The output destination.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Finalize(long offset, Span<byte> hash)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Finalize((ulong)offset, hash);
    }

    /// <summary>
    /// Clears this instance, including key material. A disposed instance cannot be reused.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_keyWords.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_chunkChainingValue.AsSpan()));
        CryptographicOperations.ZeroMemory(_block);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_chainingValueStack.AsSpan()));
        _chunkCounter = 0;
        _blockLength = 0;
        _blocksCompressed = 0;
        _stackLength = 0;
        _disposed = true;
    }

    private int ChunkStateLength => (_blocksCompressed * Blake3ManagedCore.BlockLength) + _blockLength;

    private uint ChunkStartFlag => _blocksCompressed == 0 ? Blake3ManagedCore.ChunkStart : 0;

    private static Hasher NewDeriveKeyCore(ReadOnlySpan<byte> context)
    {
        Span<byte> contextKey = stackalloc byte[Blake3ManagedCore.KeyLength];
        using (var contextHasher = new Hasher(Blake3ManagedCore.InitializationVector, Blake3ManagedCore.DeriveKeyContext))
        {
            contextHasher.Update(context);
            contextHasher.Finalize(contextKey);
        }

        Span<uint> contextKeyWords = stackalloc uint[8];
        Blake3ManagedCore.BytesToWords(contextKey, contextKeyWords);
        CryptographicOperations.ZeroMemory(contextKey);
        var hasher = new Hasher(contextKeyWords, Blake3ManagedCore.DeriveKeyMaterial);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(contextKeyWords));
        return hasher;
    }

    private void CompleteChunkState(Span<uint> chainingValue)
    {
        GetChunkChainingValue(chainingValue);
        var totalChunks = _chunkCounter + 1;
        AddChunkChainingValue(chainingValue, totalChunks);
        ResetChunkState(totalChunks);
    }

    private void ProcessCompleteChunks(ReadOnlySpan<byte> data, int chunkCount, Span<uint> chainingValue)
    {
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            Blake3ManagedCore.HashChunkGroup(
                data.Slice(chunk * Blake3ManagedCore.ChunkLength, Blake3ManagedCore.ChunkLength),
                _keyWords,
                _chunkCounter,
                _flags,
                1,
                chainingValue);
            _chunkCounter++;
            AddChunkChainingValue(chainingValue, _chunkCounter);
        }

        ResetChunkState(_chunkCounter);
    }

    private unsafe void HashChunkGroupsInParallel(
        ReadOnlySpan<byte> data,
        int chunkDegree,
        int groupCount,
        uint[] subtreeChainingValues)
    {
        var groupByteLength = chunkDegree * Blake3ManagedCore.ChunkLength;
        var workerCount = Math.Min(
            groupCount,
            Math.Min(
                Environment.ProcessorCount,
                Math.Max(1, data.Length / MinimumParallelBytesPerWorker)));
        var startingChunkCounter = _chunkCounter;
        var keyWords = _keyWords;
        var flags = _flags;
        var options = new ParallelOptions { MaxDegreeOfParallelism = workerCount };

        fixed (byte* inputPointer = data)
        {
            var inputAddress = (nint)inputPointer;
            Parallel.For(0, workerCount, options, worker =>
            {
                var firstGroup = (int)((long)worker * groupCount / workerCount);
                var endGroup = (int)((long)(worker + 1) * groupCount / workerCount);
                for (var group = firstGroup; group < endGroup; group++)
                {
                    var groupInput = new ReadOnlySpan<byte>(
                        (void*)(inputAddress + (group * groupByteLength)),
                        groupByteLength);
                    Blake3ManagedCore.HashChunkGroup(
                        groupInput,
                        keyWords,
                        startingChunkCounter + ((ulong)group * (ulong)chunkDegree),
                        flags,
                        chunkDegree,
                        subtreeChainingValues.AsSpan(group * 8, 8));
                }
            });
        }
    }

    private void UpdateChunkState(ReadOnlySpan<byte> data)
    {
        Span<uint> blockWords = stackalloc uint[16];
        Span<uint> compressionOutput = stackalloc uint[16];

        while (!data.IsEmpty)
        {
            if (_blockLength == Blake3ManagedCore.BlockLength)
            {
                Blake3ManagedCore.BytesToWords(_block, blockWords);
                Blake3ManagedCore.Compress(
                    _chunkChainingValue,
                    blockWords,
                    _chunkCounter,
                    Blake3ManagedCore.BlockLength,
                    _flags | ChunkStartFlag,
                    compressionOutput);
                compressionOutput[..8].CopyTo(_chunkChainingValue);
                _blocksCompressed++;
                _blockLength = 0;
                CryptographicOperations.ZeroMemory(_block);
            }

            var take = Math.Min(Blake3ManagedCore.BlockLength - _blockLength, data.Length);
            data[..take].CopyTo(_block.AsSpan(_blockLength));
            _blockLength += take;
            data = data[take..];
        }
    }

    private void GetChunkOutput(
        Span<uint> chainingValue,
        Span<uint> blockWords,
        out ulong counter,
        out uint blockLength,
        out uint flags)
    {
        _chunkChainingValue.AsSpan().CopyTo(chainingValue);
        Blake3ManagedCore.BytesToWords(_block, blockWords);
        counter = _chunkCounter;
        blockLength = (uint)_blockLength;
        flags = _flags | ChunkStartFlag | Blake3ManagedCore.ChunkEnd;
    }

    private void GetChunkChainingValue(Span<uint> chainingValue)
    {
        Span<uint> inputChainingValue = stackalloc uint[8];
        Span<uint> blockWords = stackalloc uint[16];
        GetChunkOutput(inputChainingValue, blockWords, out var counter, out var blockLength, out var flags);
        GetOutputChainingValue(inputChainingValue, blockWords, counter, blockLength, flags, chainingValue);
    }

    private static void GetOutputChainingValue(
        ReadOnlySpan<uint> inputChainingValue,
        ReadOnlySpan<uint> blockWords,
        ulong counter,
        uint blockLength,
        uint flags,
        Span<uint> chainingValue)
    {
        Span<uint> compressionOutput = stackalloc uint[16];
        Blake3ManagedCore.Compress(inputChainingValue, blockWords, counter, blockLength, flags, compressionOutput);
        compressionOutput[..8].CopyTo(chainingValue);
    }

    private void AddChunkChainingValue(ReadOnlySpan<uint> chunkChainingValue, ulong totalChunks)
    {
        Span<uint> newChainingValue = stackalloc uint[8];
        Span<uint> parentBlock = stackalloc uint[16];
        Span<uint> compressionOutput = stackalloc uint[16];
        chunkChainingValue.CopyTo(newChainingValue);

        while ((totalChunks & 1) == 0)
        {
            _stackLength--;
            _chainingValueStack.AsSpan(_stackLength * 8, 8).CopyTo(parentBlock);
            newChainingValue.CopyTo(parentBlock[8..]);
            Blake3ManagedCore.Compress(
                _keyWords,
                parentBlock,
                0,
                Blake3ManagedCore.BlockLength,
                _flags | Blake3ManagedCore.Parent,
                compressionOutput);
            compressionOutput[..8].CopyTo(newChainingValue);
            totalChunks >>= 1;
        }

        newChainingValue.CopyTo(_chainingValueStack.AsSpan(_stackLength * 8, 8));
        _stackLength++;
    }

    private void AddSubtreeChainingValue(ReadOnlySpan<uint> subtreeChainingValue, ulong totalChunks, int level)
    {
        Span<uint> newChainingValue = stackalloc uint[8];
        Span<uint> parentBlock = stackalloc uint[16];
        Span<uint> compressionOutput = stackalloc uint[16];
        subtreeChainingValue.CopyTo(newChainingValue);

        totalChunks >>= level;
        while ((totalChunks & 1) == 0)
        {
            _stackLength--;
            _chainingValueStack.AsSpan(_stackLength * 8, 8).CopyTo(parentBlock);
            newChainingValue.CopyTo(parentBlock[8..]);
            Blake3ManagedCore.Compress(
                _keyWords,
                parentBlock,
                0,
                Blake3ManagedCore.BlockLength,
                _flags | Blake3ManagedCore.Parent,
                compressionOutput);
            compressionOutput[..8].CopyTo(newChainingValue);
            totalChunks >>= 1;
        }

        newChainingValue.CopyTo(_chainingValueStack.AsSpan(_stackLength * 8, 8));
        _stackLength++;
    }

    private void ResetChunkState(ulong chunkCounter)
    {
        _keyWords.CopyTo(_chunkChainingValue, 0);
        CryptographicOperations.ZeroMemory(_block);
        _chunkCounter = chunkCounter;
        _blockLength = 0;
        _blocksCompressed = 0;
    }

    private static void WriteRootOutput(
        ReadOnlySpan<uint> inputChainingValue,
        ReadOnlySpan<uint> blockWords,
        uint blockLength,
        uint flags,
        ulong offset,
        Span<byte> output)
    {
        Span<uint> outputWords = stackalloc uint[16];
        Span<byte> outputBlock = stackalloc byte[Blake3ManagedCore.BlockLength];
        var outputBlockCounter = offset / Blake3ManagedCore.BlockLength;
        var offsetWithinBlock = (int)(offset % Blake3ManagedCore.BlockLength);

        while (!output.IsEmpty)
        {
            Blake3ManagedCore.Compress(
                inputChainingValue,
                blockWords,
                outputBlockCounter,
                blockLength,
                flags | Blake3ManagedCore.Root,
                outputWords);
            Blake3ManagedCore.WordsToBytes(outputWords, outputBlock);

            var take = Math.Min(Blake3ManagedCore.BlockLength - offsetWithinBlock, output.Length);
            outputBlock.Slice(offsetWithinBlock, take).CopyTo(output);
            output = output[take..];
            outputBlockCounter = unchecked(outputBlockCounter + 1);
            offsetWithinBlock = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            ThrowObjectDisposedException();
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException() => throw new ObjectDisposedException(nameof(Hasher));
}
