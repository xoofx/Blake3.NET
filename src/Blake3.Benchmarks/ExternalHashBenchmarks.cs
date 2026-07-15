using BenchmarkDotNet.Attributes;
using Blake2Fast;
using System;
using System.Runtime.InteropServices;
using ManagedHasher = Blake3.Managed.Hasher;

namespace Blake3.Benchmarks
{
    [RPlotExporter]
    public class ExternalHashBenchmarks : Blake3BenchmarkBase
    {
        [Params(4, 100, 1000, 10000, 65536, 100000, 131072, 262144, 524288, 1000000, 10000000)]
        public override int N { get; set; }

        [Benchmark(Baseline = true, Description = "Blake3 default")]
        public uint RunBlake3()
        {
            return MemoryMarshal.Cast<byte, uint>(Hasher.Hash(Data).AsSpan())[0];
        }

        [Benchmark(Description = "Blake3 managed (ext)")]
        public uint RunBlake3Managed()
        {
            return MemoryMarshal.Cast<byte, uint>(ManagedHasher.Hash(Data).AsSpan())[0];
        }

        [Benchmark(Description = "Blake2Fast")]
        public uint RunBlake2Fast()
        {
            return MemoryMarshal.Cast<byte, uint>(Blake2b.ComputeHash(Data))[0];
        }

        [Benchmark(Description = "SHA256")]
        public uint RunSHA256()
        {
            Span<byte> hash = stackalloc byte[32];
            System.Security.Cryptography.SHA256.HashData(Data, hash);
            return MemoryMarshal.Cast<byte, uint>(hash)[0];
        }
    }
}
