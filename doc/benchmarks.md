# Benchmarks

[Back to the main README](../readme.md)

These benchmarks use [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet/) on .NET 10
to compare several input sizes and implementations:

- `Blake3` (shown as `Blake3 default`), this repository's fully managed implementation with
  runtime-selected SIMD acceleration and no native code dependency.
- `Blake3.Native` (shown as `Blake3 native`), this repository's wrapper around the SIMD-optimized
  official Rust implementation.
- [`Blake3.Managed`](https://www.nuget.org/packages/Blake3.Managed/) 1.2.1 (shown as
  `Blake3 managed (ext)`), an independently maintained, fully managed SIMD implementation from NuGet.
- [Blake2Fast](https://github.com/saucecontrol/Blake2Fast).
- `System.Security.Cryptography.SHA256`.

`Blake3` and `Blake3.Native` are each measured with their one-shot serial `Hasher.Hash` API and in
explicit parallel mode using `Hasher.UpdateWithJoin`. The `Blake3.Managed` row calls that package's
one-shot `Hasher.Hash` API, which may use its own internal parallelism for large inputs; it is not an
`UpdateWithJoin` measurement.

> **Highlights**
>
> - The fully managed `Blake3` package is competitive with `Blake3.Native`: across these x64 and
>   ARM64 serial results, it ranges from about **10% faster to 35% slower**. It avoids shipping or
>   loading a native library while preserving this repository's established API.
> - `Blake3.Managed` is a separate external package, not a previous or alternate package name for
>   this repository's managed implementation. Its large-input one-shot results may include internal
>   parallelism and should not be treated as serial measurements.
> - Parallel hashing has scheduling and coordination overhead, so serial hashing is preferable for
>   small inputs. Across these runs, explicit parallel gains begin at roughly **128 KiB to 1 MiB**,
>   depending on the implementation and CPU, and are clearest for **multi-megabyte inputs**. As the
>   crossover depends on the CPU,
>   available cores, runtime, and input shape, benchmark representative data before choosing it for a
>   hot path.
> - Comparisons with SHA256 vary by architecture: BLAKE3 leads for large x64 inputs in this run,
>   while hardware-accelerated SHA256 leads the ARM64 serial measurements. Relative performance will
>   vary with the CPU's SHA and SIMD capabilities.

## AMD Ryzen 9 9950X (x64)

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1 WarmupCount=3
```

| Method                   | N        | Mean            | Error          | StdDev        | Ratio | RatioSD | Code Size | Gen0   | Allocated | Alloc Ratio |
|------------------------- |--------- |----------------:|---------------:|--------------:|------:|--------:|----------:|-------:|----------:|------------:|
| 'Blake3 default'         | 4        |        82.81 ns |       1.772 ns |      0.097 ns |  1.00 |    0.00 |   2,138 B |      - |         - |          NA |
| 'Blake3 parallel'        | 4        |       143.07 ns |      32.149 ns |      1.762 ns |  1.73 |    0.02 |        NA | 0.1214 |    2032 B |          NA |
| 'Blake3 native'          | 4        |        77.29 ns |       7.152 ns |      0.392 ns |  0.93 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel' | 4        |        97.92 ns |      43.095 ns |      2.362 ns |  1.18 |    0.02 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 4        |        88.68 ns |      12.827 ns |      0.703 ns |  1.07 |    0.01 |   3,745 B |      - |         - |          NA |
| Blake2Fast               | 4        |       135.27 ns |       3.381 ns |      0.185 ns |  1.63 |    0.00 |   5,453 B | 0.0052 |      88 B |          NA |
| SHA256                   | 4        |       111.82 ns |      43.988 ns |      2.411 ns |  1.35 |    0.03 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 100      |       127.50 ns |      27.596 ns |      1.513 ns |  1.00 |    0.01 |   2,131 B |      - |         - |          NA |
| 'Blake3 parallel'        | 100      |       231.22 ns |     101.578 ns |      5.568 ns |  1.81 |    0.04 |        NA | 0.1214 |    2032 B |          NA |
| 'Blake3 native'          | 100      |       139.98 ns |      14.929 ns |      0.818 ns |  1.10 |    0.01 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel' | 100      |       153.15 ns |      22.458 ns |      1.231 ns |  1.20 |    0.01 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 100      |       153.57 ns |      16.666 ns |      0.914 ns |  1.20 |    0.01 |   3,992 B |      - |         - |          NA |
| Blake2Fast               | 100      |       135.12 ns |       9.086 ns |      0.498 ns |  1.06 |    0.01 |   5,450 B | 0.0052 |      88 B |          NA |
| SHA256                   | 100      |       139.73 ns |     128.061 ns |      7.019 ns |  1.10 |    0.05 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 1000     |     1,123.47 ns |      44.257 ns |      2.426 ns |  1.00 |    0.00 |   2,131 B |      - |         - |          NA |
| 'Blake3 parallel'        | 1000     |     1,369.44 ns |     118.928 ns |      6.519 ns |  1.22 |    0.01 |        NA | 0.1202 |    2032 B |          NA |
| 'Blake3 native'          | 1000     |     1,005.85 ns |      62.789 ns |      3.442 ns |  0.90 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel' | 1000     |     1,013.50 ns |     169.049 ns |      9.266 ns |  0.90 |    0.01 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 1000     |     1,074.42 ns |     103.268 ns |      5.660 ns |  0.96 |    0.00 |   3,992 B |      - |         - |          NA |
| Blake2Fast               | 1000     |       963.86 ns |      25.608 ns |      1.404 ns |  0.86 |    0.00 |   5,546 B | 0.0038 |      88 B |          NA |
| SHA256                   | 1000     |       474.73 ns |      10.194 ns |      0.559 ns |  0.42 |    0.00 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 10000    |     3,872.31 ns |   1,220.379 ns |     66.893 ns |  1.00 |    0.02 |   5,984 B |      - |         - |          NA |
| 'Blake3 parallel'        | 10000    |     4,313.10 ns |     538.603 ns |     29.523 ns |  1.11 |    0.02 |        NA | 0.1144 |    2032 B |          NA |
| 'Blake3 native'          | 10000    |     3,152.27 ns |      80.296 ns |      4.401 ns |  0.81 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 10000    |     3,202.15 ns |     108.253 ns |      5.934 ns |  0.83 |    0.01 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 10000    |     3,966.12 ns |   2,215.113 ns |    121.418 ns |  1.02 |    0.03 |  18,269 B |      - |      56 B |          NA |
| Blake2Fast               | 10000    |     9,979.99 ns |     259.416 ns |     14.219 ns |  2.58 |    0.04 |   5,549 B |      - |      88 B |          NA |
| SHA256                   | 10000    |     3,970.57 ns |     540.873 ns |     29.647 ns |  1.03 |    0.02 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 65536    |     6,978.36 ns |   2,131.763 ns |    116.849 ns |  1.00 |    0.02 |   6,122 B |      - |         - |          NA |
| 'Blake3 parallel'        | 65536    |    20,062.30 ns |     243.303 ns |     13.336 ns |  2.88 |    0.04 |        NA | 0.0916 |    2032 B |          NA |
| 'Blake3 native'          | 65536    |     5,412.55 ns |   2,655.248 ns |    145.543 ns |  0.78 |    0.02 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 65536    |    37,957.75 ns |   3,041.156 ns |    166.696 ns |  5.44 |    0.08 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 65536    |    15,980.45 ns |   2,235.844 ns |    122.554 ns |  2.29 |    0.04 |  18,170 B |      - |      56 B |          NA |
| Blake2Fast               | 65536    |    63,379.37 ns |  29,297.340 ns |  1,605.886 ns |  9.08 |    0.24 |   5,827 B |      - |      88 B |          NA |
| SHA256                   | 65536    |    25,268.39 ns |   3,560.417 ns |    195.158 ns |  3.62 |    0.06 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 100000   |    12,121.16 ns |   1,300.012 ns |     71.258 ns |  1.00 |    0.01 |   6,496 B |      - |         - |          NA |
| 'Blake3 parallel'        | 100000   |    15,613.15 ns |   4,313.461 ns |    236.435 ns |  1.29 |    0.02 |        NA | 0.1068 |    2032 B |          NA |
| 'Blake3 native'          | 100000   |    10,027.29 ns |   2,421.679 ns |    132.740 ns |  0.83 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 100000   |    59,710.59 ns |  21,364.132 ns |  1,171.040 ns |  4.93 |    0.09 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 100000   |    16,755.23 ns |  14,208.856 ns |    778.835 ns |  1.38 |    0.06 |  30,307 B | 0.1221 |    2276 B |          NA |
| Blake2Fast               | 100000   |    97,853.31 ns |  22,363.122 ns |  1,225.798 ns |  8.07 |    0.10 |   5,549 B |      - |      88 B |          NA |
| SHA256                   | 100000   |    38,497.06 ns |   7,001.939 ns |    383.800 ns |  3.18 |    0.03 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 131072   |    13,141.60 ns |   5,883.034 ns |    322.469 ns |  1.00 |    0.03 |   6,130 B |      - |         - |          NA |
| 'Blake3 parallel'        | 131072   |    28,121.86 ns |   9,876.587 ns |    541.369 ns |  2.14 |    0.06 |        NA | 0.0916 |    2032 B |          NA |
| 'Blake3 native'          | 131072   |    10,711.18 ns |   1,553.486 ns |     85.152 ns |  0.82 |    0.02 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 131072   |    66,115.73 ns |  16,945.810 ns |    928.857 ns |  5.03 |    0.12 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 131072   |    21,203.25 ns |   1,162.668 ns |     63.730 ns |  1.61 |    0.04 |  29,883 B | 0.1526 |    2559 B |          NA |
| Blake2Fast               | 131072   |   128,273.51 ns |   9,031.522 ns |    495.048 ns |  9.76 |    0.21 |   5,827 B |      - |      88 B |          NA |
| SHA256                   | 131072   |    49,869.19 ns |  23,461.614 ns |  1,286.010 ns |  3.80 |    0.12 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 262144   |    28,130.90 ns |     343.698 ns |     18.839 ns |  1.00 |    0.00 |   6,130 B |      - |         - |          NA |
| 'Blake3 parallel'        | 262144   |    35,827.23 ns |   3,357.110 ns |    184.015 ns |  1.27 |    0.01 |        NA | 0.2441 |    4970 B |          NA |
| 'Blake3 native'          | 262144   |    20,886.10 ns |  12,418.635 ns |    680.707 ns |  0.74 |    0.02 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 262144   |    75,466.70 ns |  53,593.011 ns |  2,937.614 ns |  2.68 |    0.09 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 262144   |    26,092.65 ns |  15,115.100 ns |    828.510 ns |  0.93 |    0.03 |  30,086 B | 0.1526 |    2827 B |          NA |
| Blake2Fast               | 262144   |   256,723.01 ns |  56,060.617 ns |  3,072.872 ns |  9.13 |    0.09 |   5,826 B |      - |      88 B |          NA |
| SHA256                   | 262144   |    95,268.43 ns |   1,442.488 ns |     79.068 ns |  3.39 |    0.00 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 524288   |    48,652.63 ns |  16,543.946 ns |    906.830 ns |  1.00 |    0.02 |   6,130 B |      - |         - |          NA |
| 'Blake3 parallel'        | 524288   |    51,398.13 ns |  26,408.406 ns |  1,447.534 ns |  1.06 |    0.03 |        NA | 0.3662 |    6167 B |          NA |
| 'Blake3 native'          | 524288   |    41,860.36 ns |   6,351.475 ns |    348.146 ns |  0.86 |    0.02 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 524288   |    19,276.32 ns |   2,474.317 ns |    135.626 ns |  0.40 |    0.01 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 524288   |    29,502.59 ns |   3,021.997 ns |    165.646 ns |  0.61 |    0.01 |  30,365 B | 0.2136 |    3649 B |          NA |
| Blake2Fast               | 524288   |   508,291.86 ns | 246,848.462 ns | 13,530.597 ns | 10.45 |    0.29 |   5,826 B |      - |      88 B |          NA |
| SHA256                   | 524288   |   201,823.68 ns |  12,902.960 ns |    707.255 ns |  4.15 |    0.07 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 1000000  |    99,196.37 ns |  13,126.615 ns |    719.514 ns |  1.00 |    0.01 |   6,518 B |      - |         - |          NA |
| 'Blake3 parallel'        | 1000000  |    29,928.95 ns |   4,123.479 ns |    226.022 ns |  0.30 |    0.00 |        NA | 0.5493 |    9041 B |          NA |
| 'Blake3 native'          | 1000000  |    81,460.16 ns |  16,691.505 ns |    914.918 ns |  0.82 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 1000000  |   256,387.10 ns |  42,673.622 ns |  2,339.085 ns |  2.58 |    0.03 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 1000000  |    33,255.65 ns |   3,644.426 ns |    199.763 ns |  0.34 |    0.00 |  29,534 B | 0.2441 |    4376 B |          NA |
| Blake2Fast               | 1000000  |   979,773.57 ns | 252,654.951 ns | 13,848.870 ns |  9.88 |    0.14 |   5,539 B |      - |      88 B |          NA |
| SHA256                   | 1000000  |   385,679.26 ns |  24,361.508 ns |  1,335.336 ns |  3.89 |    0.03 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 default'         | 10000000 | 1,173,385.55 ns |  70,252.408 ns |  3,850.772 ns |  1.00 |    0.00 |   6,496 B |      - |         - |          NA |
| 'Blake3 parallel'        | 10000000 |   239,012.28 ns |  25,234.574 ns |  1,383.192 ns |  0.20 |    0.00 |        NA | 1.7090 |   28197 B |          NA |
| 'Blake3 native'          | 10000000 |   879,186.04 ns | 154,131.147 ns |  8,448.448 ns |  0.75 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 10000000 |   136,429.26 ns |  26,957.874 ns |  1,477.652 ns |  0.12 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 10000000 |   242,924.03 ns |  12,817.227 ns |    702.555 ns |  0.21 |    0.00 |  37,386 B | 0.2441 |    7861 B |          NA |
| Blake2Fast               | 10000000 | 9,899,805.21 ns | 963,123.292 ns | 52,792.038 ns |  8.44 |    0.05 |   5,829 B |      - |      88 B |          NA |
| SHA256                   | 10000000 | 3,854,017.71 ns | 163,131.648 ns |  8,941.796 ns |  3.28 |    0.01 |     342 B |      - |         - |          NA |

## Apple M4 Pro (ARM64)

```
BenchmarkDotNet v0.15.8, macOS Tahoe 26.5.1 (25F80) [Darwin 25.5.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1 WarmupCount=3
```

| Method                   | N        | Mean            | Error          | StdDev        | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------- |--------- |----------------:|---------------:|--------------:|------:|--------:|-------:|-------:|----------:|------------:|
| 'Blake3 default'         | 4        |        53.10 ns |       2.524 ns |      0.138 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 4        |       153.03 ns |       1.336 ns |      0.073 ns |  2.88 |    0.01 | 0.2427 |      - |    2032 B |          NA |
| 'Blake3 native'          | 4        |        55.15 ns |       1.768 ns |      0.097 ns |  1.04 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 4        |        73.22 ns |       1.659 ns |      0.091 ns |  1.38 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 4        |        69.92 ns |     129.301 ns |      7.087 ns |  1.32 |    0.12 |      - |      - |         - |          NA |
| Blake2Fast               | 4        |        88.71 ns |       4.649 ns |      0.255 ns |  1.67 |    0.01 | 0.0105 |      - |      88 B |          NA |
| SHA256                   | 4        |       152.89 ns |      17.445 ns |      0.956 ns |  2.88 |    0.02 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 100      |       107.81 ns |       4.701 ns |      0.258 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 100      |       211.70 ns |      12.904 ns |      0.707 ns |  1.96 |    0.01 | 0.2427 |      - |    2032 B |          NA |
| 'Blake3 native'          | 100      |        96.87 ns |      26.507 ns |      1.453 ns |  0.90 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 100      |       117.49 ns |       8.460 ns |      0.464 ns |  1.09 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 100      |       122.71 ns |       2.711 ns |      0.149 ns |  1.14 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 100      |        89.25 ns |       4.856 ns |      0.266 ns |  0.83 |    0.00 | 0.0105 |      - |      88 B |          NA |
| SHA256                   | 100      |       117.53 ns |      15.381 ns |      0.843 ns |  1.09 |    0.01 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 1000     |       885.57 ns |      38.060 ns |      2.086 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 1000     |     1,062.01 ns |      29.565 ns |      1.621 ns |  1.20 |    0.00 | 0.2422 |      - |    2032 B |          NA |
| 'Blake3 native'          | 1000     |       736.97 ns |      31.116 ns |      1.706 ns |  0.83 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 1000     |       755.62 ns |      27.510 ns |      1.508 ns |  0.85 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 1000     |       956.65 ns |      34.869 ns |      1.911 ns |  1.08 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 1000     |       628.55 ns |      13.982 ns |      0.766 ns |  0.71 |    0.00 | 0.0105 |      - |      88 B |          NA |
| SHA256                   | 1000     |       351.38 ns |      26.030 ns |      1.427 ns |  0.40 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 10000    |     5,457.38 ns |     439.474 ns |     24.089 ns |  1.00 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 10000    |     5,885.74 ns |     184.373 ns |     10.106 ns |  1.08 |    0.00 | 0.2365 |      - |    2032 B |          NA |
| 'Blake3 native'          | 10000    |     4,532.66 ns |      89.803 ns |      4.922 ns |  0.83 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 10000    |    33,319.97 ns |   1,875.999 ns |    102.830 ns |  6.11 |    0.03 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 10000    |     5,975.50 ns |     371.457 ns |     20.361 ns |  1.09 |    0.01 |      - |      - |         - |          NA |
| Blake2Fast               | 10000    |     6,198.98 ns |     247.319 ns |     13.556 ns |  1.14 |    0.00 | 0.0076 |      - |      88 B |          NA |
| SHA256                   | 10000    |     2,993.95 ns |     132.973 ns |      7.289 ns |  0.55 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 65536    |    30,685.03 ns |   2,363.882 ns |    129.572 ns |  1.00 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 65536    |    34,321.23 ns |   1,064.746 ns |     58.362 ns |  1.12 |    0.00 | 0.1831 |      - |    2032 B |          NA |
| 'Blake3 native'          | 65536    |    25,869.22 ns |   1,330.126 ns |     72.909 ns |  0.84 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 65536    |    43,039.77 ns |   4,448.247 ns |    243.823 ns |  1.40 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 65536    |    34,477.29 ns |   1,230.016 ns |     67.421 ns |  1.12 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 65536    |    40,397.99 ns |   1,916.379 ns |    105.043 ns |  1.32 |    0.01 |      - |      - |      88 B |          NA |
| SHA256                   | 65536    |    19,087.78 ns |     126.158 ns |      6.915 ns |  0.62 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 100000   |    47,812.98 ns |   1,802.618 ns |     98.808 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 100000   |    50,209.67 ns |   1,053.439 ns |     57.743 ns |  1.05 |    0.00 | 0.1831 |      - |    2032 B |          NA |
| 'Blake3 native'          | 100000   |    40,013.07 ns |     625.819 ns |     34.303 ns |  0.84 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 100000   |    83,671.69 ns |  10,632.262 ns |    582.790 ns |  1.75 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 100000   |    51,542.59 ns |   1,287.507 ns |     70.573 ns |  1.08 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 100000   |    61,502.14 ns |   2,239.054 ns |    122.730 ns |  1.29 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                   | 100000   |    29,022.57 ns |   1,061.489 ns |     58.184 ns |  0.61 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 131072   |    61,606.37 ns |   1,642.964 ns |     90.056 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 131072   |    66,707.72 ns |   3,430.837 ns |    188.056 ns |  1.08 |    0.00 | 0.1221 |      - |    2032 B |          NA |
| 'Blake3 native'          | 131072   |    51,640.71 ns |     528.814 ns |     28.986 ns |  0.84 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 131072   |    50,256.74 ns |  10,497.428 ns |    575.399 ns |  0.82 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 131072   |    68,064.68 ns |   3,063.187 ns |    167.904 ns |  1.10 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 131072   |    80,661.27 ns |   1,007.382 ns |     55.218 ns |  1.31 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                   | 131072   |    38,039.20 ns |     659.897 ns |     36.171 ns |  0.62 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 262144   |   123,673.17 ns |   3,876.224 ns |    212.469 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 262144   |    41,078.74 ns |  15,943.365 ns |    873.910 ns |  0.33 |    0.01 | 0.7935 |      - |    7082 B |          NA |
| 'Blake3 native'          | 262144   |   106,442.78 ns |   2,391.901 ns |    131.108 ns |  0.86 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 262144   |    60,527.47 ns |   4,012.588 ns |    219.943 ns |  0.49 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 262144   |   137,303.27 ns |   3,441.009 ns |    188.613 ns |  1.11 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 262144   |   163,916.90 ns |   3,264.773 ns |    178.953 ns |  1.33 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                   | 262144   |    75,985.82 ns |   1,237.007 ns |     67.805 ns |  0.61 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 524288   |   250,600.25 ns |   3,603.979 ns |    197.546 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 524288   |    71,531.41 ns |  34,045.706 ns |  1,866.160 ns |  0.29 |    0.01 | 1.3428 |      - |   10634 B |          NA |
| 'Blake3 native'          | 524288   |   214,958.73 ns |   8,622.126 ns |    472.608 ns |  0.86 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 524288   |    78,654.41 ns |   7,529.114 ns |    412.696 ns |  0.31 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 524288   |   274,768.49 ns |   5,889.613 ns |    322.830 ns |  1.10 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 524288   |   329,154.90 ns |   3,601.041 ns |    197.385 ns |  1.31 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                   | 524288   |   152,176.03 ns |   9,186.020 ns |    503.517 ns |  0.61 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 1000000  |   479,980.61 ns |   6,249.434 ns |    342.553 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 1000000  |   104,796.52 ns |  19,093.046 ns |  1,046.554 ns |  0.22 |    0.00 | 1.8311 |      - |   14381 B |          NA |
| 'Blake3 native'          | 1000000  |   414,676.00 ns |  25,482.426 ns |  1,396.778 ns |  0.86 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 1000000  |   269,320.93 ns |  10,978.882 ns |    601.790 ns |  0.56 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 1000000  |   524,761.94 ns |   9,204.666 ns |    504.539 ns |  1.09 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 1000000  |   629,033.68 ns |   6,145.852 ns |    336.875 ns |  1.31 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                   | 1000000  |   289,552.37 ns |   1,260.629 ns |     69.099 ns |  0.60 |    0.00 |      - |      - |         - |          NA |
|                          |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 default'         | 10000000 | 4,822,540.69 ns |  69,467.598 ns |  3,807.753 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 parallel'        | 10000000 |   867,966.45 ns | 100,194.501 ns |  5,491.999 ns |  0.18 |    0.00 | 9.7656 | 1.9531 |   84834 B |          NA |
| 'Blake3 native'          | 10000000 | 4,185,335.29 ns |  89,018.910 ns |  4,879.427 ns |  0.87 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel' | 10000000 |   653,996.50 ns |  60,925.609 ns |  3,339.538 ns |  0.14 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed (ext)'   | 10000000 | 5,328,655.05 ns |  58,161.314 ns |  3,188.018 ns |  1.10 |    0.00 |      - |      - |         - |          NA |
| Blake2Fast               | 10000000 | 6,306,882.05 ns |  65,270.984 ns |  3,577.723 ns |  1.31 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                   | 10000000 | 2,911,

## Blake3VsNativeBenchmarks

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


## ExternalHashBenchmarks


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


## Blake3ParallelBenchmarks

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

