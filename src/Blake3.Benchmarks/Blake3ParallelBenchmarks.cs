extern alias NativeBlake3;

using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using NativeHasher = NativeBlake3::Blake3.Hasher;

namespace Blake3.Benchmarks
{
    [RPlotExporter]
    public class Blake3ParallelBenchmarks : Blake3BenchmarkBase
    {
        [Params(524288, 1000000, 10000000)]
        public override int N { get; set; }

        [Benchmark(Baseline = true, Description = "Blake3 parallel")]
        public uint RunBlake3Parallel()
        {
            using var hasher = Hasher.New();
            hasher.UpdateWithJoin(Data);
            return MemoryMarshal.Cast<byte, uint>(hasher.Finalize().AsSpan())[0];
        }

        [Benchmark(Description = "Blake3 native parallel")]
        public uint RunBlake3NativeParallel()
        {
            using var hasher = NativeHasher.New();
            hasher.UpdateWithJoin(Data);
            return MemoryMarshal.Cast<byte, uint>(hasher.Finalize().AsSpan())[0];
        }
    }
}
