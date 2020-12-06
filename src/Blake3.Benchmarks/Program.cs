using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Blake2Fast;

namespace Blake3.Benchmarks
{
    [RPlotExporter]
    public class Program
    {
        private byte[] _data;

        [Params(4, 100, 1000, 10000, 100000, 1000000)]
        public int N;

        [GlobalSetup]
        public void Setup()
        {
            _data = new byte[N];
            new Random(42).NextBytes(_data);
        }

        [Benchmark(Description = "Blake3")]
        public void RunBlake3()
        {
            // Benchmark the WithJoin version
            if (_data.Length >= 1000000)
            {
                using var hasher = Hasher.New();
                hasher.UpdateWithJoin<byte>(_data);
                hasher.Finalize();
            }
            else
            {
                Hasher.Hash(_data.AsSpan());
            }
        }

        [Benchmark(Description = "Blake2Fast")]
        public void RunBlake2Fast()
        {
            Blake2b.ComputeHash(_data);
        }

        [Benchmark(Description = "SHA256")]
        public unsafe void RunSHA256()
        {
            Span<byte> data = stackalloc byte[32];
            System.Security.Cryptography.SHA256.HashData(_data.AsSpan(), data);
        }

        static void Main(string[] args)
        {
            BenchmarkRunner.Run<Program>();
        }
    }
}
