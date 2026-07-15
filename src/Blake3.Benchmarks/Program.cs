using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;

namespace Blake3.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.ShortRun) // 3 warmup + 3 iterations, fast feedback loop for AI iteration
                .AddDiagnoser(MemoryDiagnoser.Default);

            if (OperatingSystem.IsWindows())
            {
                config = config
                    .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                        maxDepth: 3,
                        printSource: true,
                        exportGithubMarkdown: true,
                        exportCombinedDisassemblyReport: true)))
                    .AddExporter(JsonExporter.Full);
            }

            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args, config);
        }
    }
}
