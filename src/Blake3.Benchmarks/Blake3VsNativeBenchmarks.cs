extern alias NativeBlake3;

using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using NativeHasher = NativeBlake3::Blake3.Hasher;

namespace Blake3.Benchmarks
{
    [RPlotExporter]
    public class Blake3VsNativeBenchmarks : Blake3BenchmarkBase
    {
        [Params(4, 100, 1000, 10000, 65536, 100000, 131072, 262144, 524288, 1000000, 10000000)]
        public override int N { get; set; }

        [Benchmark(Baseline = true, Description = "Blake3 default")]
        public uint RunBlake3()
        {
            return MemoryMarshal.Cast<byte, uint>(Hasher.Hash(Data).AsSpan())[0];
        }

        [Benchmark(Description = "Blake3 native")]
        public uint RunBlake3Native()
        {
            return MemoryMarshal.Cast<byte, uint>(NativeHasher.Hash(Data).AsSpan())[0];
        }
    }
}
