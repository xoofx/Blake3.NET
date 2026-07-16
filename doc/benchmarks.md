# Benchmarks

[Back to the main README](../readme.md)

These benchmarks use [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet/) on .NET 10
with fixed, randomly generated input data. They cover three focused comparisons:

- Serial one-shot hashing with this repository's fully managed `Blake3` package and the
  `Blake3.Native` wrapper around the SIMD-optimized official Rust implementation, measured on x64
  and ARM64.
- One-shot hashing with `Blake3` and other .NET hash implementations, measured on x64:
  [`Blake3.Managed`](https://www.nuget.org/packages/Blake3.Managed/) 1.2.1 (shown as
  `Blake3 managed (ext)`), [Blake2Fast](https://github.com/saucecontrol/Blake2Fast), and
  `System.Security.Cryptography.SHA256`.
- Explicit parallel hashing with `Hasher.UpdateWithJoin` from `Blake3` and `Blake3.Native`, measured
  on x64 for inputs from 512 KiB to 10 MB.

`Blake3.Managed` is an independently maintained package, not a previous or alternate package name
for this repository's managed implementation. Its one-shot `Hasher.Hash` API may use internal
parallelism for large inputs, so those large-input results are not a serial-to-serial comparison.

> **Highlights**
>
> - The fully managed `Blake3` package delivers near-native serial performance without shipping or
>   loading a native library. Across the x64 and ARM64 measurements, it ranges from about **11%
>   faster to 22% slower** than `Blake3.Native`.
> - On x64, `Blake3` is about **12% to 22% slower** than native from 1 KB through 10 MB, while it is
>   **11% faster** for the 100-byte input. On ARM64, it is about **13% to 20% slower** from 1 KB
>   through 10 MB and **5% faster** for the 4-byte input.
> - The x64 serial results show the benefits of BLAKE3 on larger inputs. At 64 KiB, `Blake3` is
>   about **2.4x faster** than the external `Blake3.Managed` package, **3.9x faster** than SHA256,
>   and **9.9x faster** than Blake2Fast. For large inputs, the external `Blake3.Managed` one-shot API
>   uses its own parallelism and can outperform a serial call.
> - The serial `Blake3` and `Blake3.Native` measurements complete without managed allocations.
>   Explicit parallel hashing adds scheduling and allocation overhead, but substantially improves
>   throughput for multi-megabyte inputs: at 10 MB on x64, `UpdateWithJoin` is about **4.4x faster**
>   than serial `Blake3` and **6.5x faster** than serial `Blake3.Native` in these runs.
>
> Benchmark results depend on the CPU, available cores, runtime, and input shape. In particular,
> parallel crossover points can vary and should be measured with representative application data.

The tables are BenchmarkDotNet `ShortRun` results (three warmup and three measurement iterations),
so small differences should be treated as indicative rather than absolute.

## Serial: `Blake3` vs `Blake3.Native` (`Hasher.Hash`)

### AMD Ryzen 9 9950X (x64)

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1 WarmupCount=3
```

| Method           | N        | Mean          | Error         | StdDev       | Ratio | Code Size | Allocated | Alloc Ratio |
|----------------- |--------- |--------------:|--------------:|-------------:|------:|----------:|----------:|------------:|
| 'Blake3 default' | 4        |      82.12 ns |      2.671 ns |     0.146 ns |  1.00 |   3,218 B |         - |          NA |
| 'Blake3 native'  | 4        |      75.27 ns |      0.196 ns |     0.011 ns |  0.92 |     355 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 100      |     123.12 ns |      0.984 ns |     0.054 ns |  1.00 |   3,211 B |         - |          NA |
| 'Blake3 native'  | 100      |     138.03 ns |      2.187 ns |     0.120 ns |  1.12 |     355 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 1000     |   1,114.55 ns |      3.604 ns |     0.198 ns |  1.00 |   3,211 B |         - |          NA |
| 'Blake3 native'  | 1000     |     992.50 ns |      9.308 ns |     0.510 ns |  0.89 |     355 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 10000    |   3,612.77 ns |    115.985 ns |     6.358 ns |  1.00 |   7,350 B |         - |          NA |
| 'Blake3 native'  | 10000    |   3,098.95 ns |     20.440 ns |     1.120 ns |  0.86 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 65536    |   6,030.67 ns |     17.941 ns |     0.983 ns |  1.00 |   8,169 B |         - |          NA |
| 'Blake3 native'  | 65536    |   4,987.99 ns |     86.206 ns |     4.725 ns |  0.83 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 100000   |  10,905.14 ns |    320.218 ns |    17.552 ns |  1.00 |   8,494 B |         - |          NA |
| 'Blake3 native'  | 100000   |   9,225.64 ns |    171.406 ns |     9.395 ns |  0.85 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 131072   |  11,754.00 ns |     25.263 ns |     1.385 ns |  1.00 |   8,155 B |         - |          NA |
| 'Blake3 native'  | 131072   |   9,745.71 ns |    164.301 ns |     9.006 ns |  0.83 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 262144   |  23,587.73 ns |    852.524 ns |    46.730 ns |  1.00 |   8,155 B |         - |          NA |
| 'Blake3 native'  | 262144   |  19,280.80 ns |    237.858 ns |    13.038 ns |  0.82 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 524288   |  46,399.06 ns |  2,031.634 ns |   111.361 ns |  1.00 |   8,155 B |         - |          NA |
| 'Blake3 native'  | 524288   |  38,418.34 ns |    183.719 ns |    10.070 ns |  0.83 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 1000000  |  88,234.66 ns |  2,110.065 ns |   115.660 ns |  1.00 |   8,485 B |         - |          NA |
| 'Blake3 native'  | 1000000  |  75,019.60 ns |    429.557 ns |    23.545 ns |  0.85 |     360 B |         - |          NA |
|                  |          |               |               |              |       |           |           |             |
| 'Blake3 default' | 10000000 | 907,587.73 ns | 45,363.456 ns | 2,486.524 ns |  1.00 |   8,441 B |         - |          NA |
| 'Blake3 native'  | 10000000 | 756,075.81 ns | 41,412.564 ns | 2,269.962 ns |  0.83 |     360 B |         - |          NA |

### Apple M4 Pro (ARM64)

```
BenchmarkDotNet v0.15.8, macOS Tahoe 26.5.1 (25F80) [Darwin 25.5.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1 WarmupCount=3
```

| Method           | N        | Mean            | Error          | StdDev        | Ratio | Allocated | Alloc Ratio |
|----------------- |--------- |----------------:|---------------:|--------------:|------:|----------:|------------:|
| 'Blake3 default' | 4        |        49.87 ns |       1.231 ns |      0.067 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 4        |        52.63 ns |       1.330 ns |      0.073 ns |  1.06 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 100      |       102.62 ns |       3.250 ns |      0.178 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 100      |        91.25 ns |       2.549 ns |      0.140 ns |  0.89 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 1000     |       836.55 ns |      20.618 ns |      1.130 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 1000     |       700.29 ns |      22.154 ns |      1.214 ns |  0.84 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 10000    |     5,218.23 ns |     110.609 ns |      6.063 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 10000    |     4,514.60 ns |      38.689 ns |      2.121 ns |  0.87 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 65536    |    28,487.52 ns |     821.890 ns |     45.051 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 65536    |    25,077.70 ns |     136.511 ns |      7.483 ns |  0.88 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 100000   |    44,171.43 ns |   1,142.003 ns |     62.597 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 100000   |    38,953.93 ns |     189.971 ns |     10.413 ns |  0.88 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 131072   |    57,069.08 ns |   2,428.221 ns |    133.099 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 131072   |    50,187.54 ns |     441.632 ns |     24.207 ns |  0.88 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 262144   |   114,543.10 ns |   7,969.881 ns |    436.856 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 262144   |   100,528.36 ns |   1,200.951 ns |     65.828 ns |  0.88 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 524288   |   228,890.88 ns |   6,392.852 ns |    350.414 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 524288   |   201,074.93 ns |   1,575.382 ns |     86.352 ns |  0.88 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 1000000  |   440,723.65 ns |   9,942.807 ns |    544.999 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 1000000  |   384,006.50 ns |  12,111.836 ns |    663.891 ns |  0.87 |         - |          NA |
|                  |          |                 |                |               |       |           |             |
| 'Blake3 default' | 10000000 | 4,428,962.46 ns | 300,739.435 ns | 16,484.543 ns |  1.00 |         - |          NA |
| 'Blake3 native'  | 10000000 | 3,846,564.83 ns |  25,030.885 ns |  1,372.027 ns |  0.87 |         - |          NA |


## One-shot: other hash implementations (AMD Ryzen 9 9950X, x64)


```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1 WarmupCount=3
```

| Method                 | N        | Mean            | Error         | StdDev       | Ratio | RatioSD | Gen0   | Code Size | Allocated | Alloc Ratio |
|----------------------- |--------- |----------------:|--------------:|-------------:|------:|--------:|-------:|----------:|----------:|------------:|
| 'Blake3 default'       | 4        |        82.42 ns |     11.776 ns |     0.645 ns |  1.00 |    0.01 |      - |   3,218 B |         - |          NA |
| 'Blake3 managed (ext)' | 4        |        86.94 ns |      0.212 ns |     0.012 ns |  1.05 |    0.01 |      - |   3,745 B |         - |          NA |
| Blake2Fast             | 4        |       133.42 ns |      0.486 ns |     0.027 ns |  1.62 |    0.01 | 0.0052 |   5,453 B |      88 B |          NA |
| SHA256                 | 4        |       103.44 ns |      4.587 ns |     0.251 ns |  1.25 |    0.01 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 100      |       123.47 ns |      1.800 ns |     0.099 ns |  1.00 |    0.00 |      - |   3,211 B |         - |          NA |
| 'Blake3 managed (ext)' | 100      |       150.45 ns |      1.767 ns |     0.097 ns |  1.22 |    0.00 |      - |   3,992 B |         - |          NA |
| Blake2Fast             | 100      |       134.52 ns |     10.890 ns |     0.597 ns |  1.09 |    0.00 | 0.0052 |   5,450 B |      88 B |          NA |
| SHA256                 | 100      |       126.18 ns |      5.356 ns |     0.294 ns |  1.02 |    0.00 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 1000     |     1,115.35 ns |      3.225 ns |     0.177 ns |  1.00 |    0.00 |      - |   3,211 B |         - |          NA |
| 'Blake3 managed (ext)' | 1000     |     1,053.13 ns |     15.556 ns |     0.853 ns |  0.94 |    0.00 |      - |   3,992 B |         - |          NA |
| Blake2Fast             | 1000     |       949.12 ns |      4.116 ns |     0.226 ns |  0.85 |    0.00 | 0.0048 |   5,546 B |      88 B |          NA |
| SHA256                 | 1000     |       444.94 ns |      8.808 ns |     0.483 ns |  0.40 |    0.00 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 10000    |     3,606.93 ns |     20.725 ns |     1.136 ns |  1.00 |    0.00 |      - |   7,354 B |         - |          NA |
| 'Blake3 managed (ext)' | 10000    |     3,834.32 ns |     66.911 ns |     3.668 ns |  1.06 |    0.00 |      - |  18,213 B |      56 B |          NA |
| Blake2Fast             | 10000    |     9,242.27 ns |    247.965 ns |    13.592 ns |  2.56 |    0.00 |      - |   5,549 B |      88 B |          NA |
| SHA256                 | 10000    |     3,673.16 ns |    141.781 ns |     7.771 ns |  1.02 |    0.00 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 65536    |     6,025.40 ns |    267.785 ns |    14.678 ns |  1.00 |    0.00 |      - |   8,169 B |         - |          NA |
| 'Blake3 managed (ext)' | 65536    |    14,552.86 ns |    262.955 ns |    14.413 ns |  2.42 |    0.01 |      - |  18,114 B |      56 B |          NA |
| Blake2Fast             | 65536    |    59,733.70 ns |     80.460 ns |     4.410 ns |  9.91 |    0.02 |      - |   5,827 B |      88 B |          NA |
| SHA256                 | 65536    |    23,581.89 ns |    748.634 ns |    41.035 ns |  3.91 |    0.01 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 100000   |    10,907.97 ns |    582.644 ns |    31.937 ns |  1.00 |    0.00 |      - |   8,456 B |         - |          NA |
| 'Blake3 managed (ext)' | 100000   |    14,958.62 ns |    235.925 ns |    12.932 ns |  1.37 |    0.00 | 0.1221 |  30,276 B |    2231 B |          NA |
| Blake2Fast             | 100000   |    91,314.10 ns |  3,061.176 ns |   167.793 ns |  8.37 |    0.03 |      - |   5,549 B |      88 B |          NA |
| SHA256                 | 100000   |    35,867.08 ns |  1,181.185 ns |    64.745 ns |  3.29 |    0.01 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 131072   |    11,538.65 ns |     34.346 ns |     1.883 ns |  1.00 |    0.00 |      - |   8,155 B |         - |          NA |
| 'Blake3 managed (ext)' | 131072   |    17,473.52 ns |    625.486 ns |    34.285 ns |  1.51 |    0.00 | 0.1221 |  30,044 B |    2508 B |          NA |
| Blake2Fast             | 131072   |   119,413.87 ns |    413.862 ns |    22.685 ns | 10.35 |    0.00 |      - |   5,827 B |      88 B |          NA |
| SHA256                 | 131072   |    46,945.24 ns |    559.570 ns |    30.672 ns |  4.07 |    0.00 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 262144   |    23,386.06 ns |    544.747 ns |    29.859 ns |  1.00 |    0.00 |      - |   8,155 B |         - |          NA |
| 'Blake3 managed (ext)' | 262144   |    18,825.93 ns |    745.672 ns |    40.873 ns |  0.81 |    0.00 | 0.1526 |  30,020 B |    2858 B |          NA |
| Blake2Fast             | 262144   |   239,922.34 ns |  2,700.268 ns |   148.011 ns | 10.26 |    0.01 |      - |   5,826 B |      88 B |          NA |
| SHA256                 | 262144   |    93,802.67 ns |    257.119 ns |    14.094 ns |  4.01 |    0.00 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 524288   |    46,882.88 ns |  3,506.974 ns |   192.229 ns |  1.00 |    0.01 |      - |   8,155 B |         - |          NA |
| 'Blake3 managed (ext)' | 524288   |    27,097.21 ns |    941.849 ns |    51.626 ns |  0.58 |    0.00 | 0.2136 |  30,033 B |    3699 B |          NA |
| Blake2Fast             | 524288   |   480,848.50 ns | 48,825.934 ns | 2,676.314 ns | 10.26 |    0.06 |      - |   5,823 B |      88 B |          NA |
| SHA256                 | 524288   |   190,418.43 ns | 44,029.824 ns | 2,413.423 ns |  4.06 |    0.05 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 1000000  |    91,670.84 ns | 31,592.943 ns | 1,731.716 ns |  1.00 |    0.02 |      - |   8,485 B |         - |          NA |
| 'Blake3 managed (ext)' | 1000000  |    35,480.01 ns | 64,008.083 ns | 3,508.499 ns |  0.39 |    0.03 | 0.2441 |  29,502 B |    4253 B |          NA |
| Blake2Fast             | 1000000  |   913,685.16 ns | 30,384.972 ns | 1,665.503 ns |  9.97 |    0.16 |      - |   5,539 B |      88 B |          NA |
| SHA256                 | 1000000  |   358,664.79 ns | 10,127.695 ns |   555.133 ns |  3.91 |    0.06 |      - |     342 B |         - |          NA |
|                        |          |                 |               |              |       |         |        |           |           |             |
| 'Blake3 default'       | 10000000 |   925,091.31 ns | 71,865.863 ns | 3,939.210 ns |  1.00 |    0.01 |      - |   8,441 B |         - |          NA |
| 'Blake3 managed (ext)' | 10000000 |   221,220.17 ns |  6,563.852 ns |   359.787 ns |  0.24 |    0.00 | 0.2441 |  37,399 B |    7923 B |          NA |
| Blake2Fast             | 10000000 | 9,159,290.62 ns | 32,710.881 ns | 1,792.994 ns |  9.90 |    0.04 |      - |   5,823 B |      88 B |          NA |
| SHA256                 | 10000000 | 3,576,552.21 ns | 10,767.420 ns |   590.199 ns |  3.87 |    0.01 |      - |     342 B |         - |          NA |


## Explicit parallel hashing (AMD Ryzen 9 9950X, x64)

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1 WarmupCount=3
```

| Method                   | N        | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------- |--------- |----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| 'Blake3 parallel'        | 524288   |  50.56 us | 14.678 us | 0.805 us |  1.00 |    0.02 | 0.2441 |    4197 B |        1.00 |
| 'Blake3 native parallel' | 524288   |  17.23 us |  0.411 us | 0.023 us |  0.34 |    0.00 |      - |         - |        0.00 |
|                          |          |           |           |          |       |         |        |           |             |
| 'Blake3 parallel'        | 1000000  |  26.95 us |  3.921 us | 0.215 us |  1.00 |    0.01 | 0.4272 |    7072 B |        1.00 |
| 'Blake3 native parallel' | 1000000  | 184.98 us | 46.628 us | 2.556 us |  6.86 |    0.09 |      - |         - |        0.00 |
|                          |          |           |           |          |       |         |        |           |             |
| 'Blake3 parallel'        | 10000000 | 207.25 us | 30.229 us | 1.657 us |  1.00 |    0.01 | 1.4648 |   25776 B |        1.00 |
| 'Blake3 native parallel' | 10000000 | 116.22 us | 14.228 us | 0.780 us |  0.56 |    0.01 |      - |         - |        0.00 |

