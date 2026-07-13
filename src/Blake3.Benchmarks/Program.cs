using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
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

        [Benchmark(Baseline = true, Description = "Blake3 native")]
        public Hash RunBlake3Native()
        {
            return Hasher.Hash(_data);
        }

        [Benchmark(Description = "Blake3 managed")]
        public Hash RunBlake3Managed()
        {
            return Hasher2.Hash(_data);
        }

        //[Benchmark(Description = "Blake2Fast")]
        //public void RunBlake2Fast()
        //{
        //    Blake2b.ComputeHash(_data);
        //}

        //[Benchmark(Description = "SHA256")]
        //public unsafe void RunSHA256()
        //{
        //    Span<byte> data = stackalloc byte[32];
        //    System.Security.Cryptography.SHA256.HashData(_data.AsSpan(), data);
        //}

        static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.ShortRun) // 3 warmup + 3 iterations, fast feedback loop for AI iteration
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                    maxDepth: 3,
                    printSource: true,
                    exportGithubMarkdown: true,
                    exportCombinedDisassemblyReport: true)))
                .AddExporter(JsonExporter.Full);

            BenchmarkRunner.Run<Program>(config, args);
        }
    }
}
