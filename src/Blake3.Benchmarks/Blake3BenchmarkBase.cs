using BenchmarkDotNet.Attributes;
using System;

namespace Blake3.Benchmarks
{
    public abstract class Blake3BenchmarkBase
    {
        protected byte[] Data { get; private set; } = Array.Empty<byte>();

        public abstract int N { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            Data = new byte[N];
            new Random(42).NextBytes(Data);
        }
    }
}
