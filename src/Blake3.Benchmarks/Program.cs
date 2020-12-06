using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Blake2Fast;

namespace Blake3.Benchmarks
{
    public class Program
    {
        [Benchmark(Description = "Blake3")]
        [ArgumentsSource(nameof(Data))]
        public void RunBlake3(byte[] input)
        {
            // Benchmark the WithJoin version
            if (input.Length >= 1000000)
            {
                using var hasher = Hasher.New();
                hasher.UpdateWithJoin<byte>(input);
                hasher.Finalize();
            }
            else
            {
                Hasher.Hash(input.AsSpan());
            }
        }

        [Benchmark(Description = "Blake2Fast")]
        [ArgumentsSource(nameof(Data))]
        public void RunBlake2Fast(byte[] input)
        {
            Blake2b.ComputeHash(input);
        }

        [Benchmark(Description = "SHA256")]
        [ArgumentsSource(nameof(Data))]
        public unsafe void RunSHA256(byte[] input)
        {
            Span<byte> data = stackalloc byte[32];
            System.Security.Cryptography.SHA256.HashData(input.AsSpan(), data);
        }

        public IEnumerable<byte[]> Data()
        {
            yield return new byte[] { 1, 2, 3 };
            yield return Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
            yield return Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();
            yield return Enumerable.Range(0, 10000).Select(i => (byte)i).ToArray();
            yield return Enumerable.Range(0, 100000).Select(i => (byte)i).ToArray();
            yield return Enumerable.Range(0, 1000000).Select(i => (byte)i).ToArray();
        }

        static void Main(string[] args)
        {
            BenchmarkRunner.Run<Program>();
        }
    }
}
