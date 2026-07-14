# Benchmarks

[Back to the main README](../readme.md)

These benchmarks use [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet/) on .NET 10
to compare several input sizes and implementations:

- `Blake3`, which calls the SIMD-optimized Rust implementation through native interop.
- `Blake3.Sharp`, a portable .NET implementation with runtime-selected SIMD acceleration and no
  native code dependency.
- [Blake2Fast](https://github.com/saucecontrol/Blake2Fast).
- `System.Security.Cryptography.SHA256`.

Both BLAKE3 packages are measured in serial mode and in parallel mode using `Hasher.UpdateWithJoin`.

> **Highlights**
>
> - `Blake3.Sharp` is competitive with the native Rust implementation: across these x64 and ARM64 serial results,
>   it ranges from about **7% faster to 30% slower**. It is a compelling option when portability
>   and avoiding native binaries matter more than always getting the highest possible throughput.
> - Parallel hashing has scheduling and coordination overhead, so serial hashing is preferable for
>   small inputs. Across these runs, the crossover occurs between roughly **128 KiB and 512 KiB**, and
>   parallel hashing provides its clearest gains for **multi-megabyte inputs**. As the crossover depends on the CPU,
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

| Method                    | N        | Mean            | Error          | StdDev        | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------- |--------- |----------------:|---------------:|--------------:|------:|--------:|-------:|-------:|----------:|------------:|
| 'Blake3 native'           | 4        |        54.63 ns |       4.892 ns |      0.268 ns |  1.00 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 4        |        73.61 ns |       1.130 ns |      0.062 ns |  1.35 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 4        |        53.40 ns |       0.958 ns |      0.052 ns |  0.98 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 4        |       155.98 ns |       7.096 ns |      0.389 ns |  2.86 |    0.01 | 0.2427 |      - |    2032 B |          NA |
| Blake2Fast                | 4        |        89.95 ns |       5.470 ns |      0.300 ns |  1.65 |    0.01 | 0.0105 |      - |      88 B |          NA |
| SHA256                    | 4        |       155.78 ns |       5.971 ns |      0.327 ns |  2.85 |    0.01 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 100      |        98.00 ns |       5.644 ns |      0.309 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 100      |       121.07 ns |       8.355 ns |      0.458 ns |  1.24 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 100      |       109.72 ns |       6.966 ns |      0.382 ns |  1.12 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 100      |       214.49 ns |       3.280 ns |      0.180 ns |  2.19 |    0.01 | 0.2427 |      - |    2032 B |          NA |
| Blake2Fast                | 100      |        91.00 ns |       2.603 ns |      0.143 ns |  0.93 |    0.00 | 0.0105 |      - |      88 B |          NA |
| SHA256                    | 100      |       119.61 ns |      16.474 ns |      0.903 ns |  1.22 |    0.01 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 1000     |       746.78 ns |      12.175 ns |      0.667 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 1000     |       764.26 ns |      10.076 ns |      0.552 ns |  1.02 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 1000     |       891.85 ns |      14.138 ns |      0.775 ns |  1.19 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 1000     |     1,069.82 ns |      20.768 ns |      1.138 ns |  1.43 |    0.00 | 0.2422 |      - |    2032 B |          NA |
| Blake2Fast                | 1000     |       634.74 ns |       2.637 ns |      0.145 ns |  0.85 |    0.00 | 0.0105 |      - |      88 B |          NA |
| SHA256                    | 1000     |       350.97 ns |       6.532 ns |      0.358 ns |  0.47 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 10000    |     4,602.40 ns |     251.165 ns |     13.767 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 10000    |    33,551.96 ns |   3,596.025 ns |    197.110 ns |  7.29 |    0.04 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 10000    |     5,586.79 ns |     142.646 ns |      7.819 ns |  1.21 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 10000    |     5,993.31 ns |      51.767 ns |      2.837 ns |  1.30 |    0.00 | 0.2365 |      - |    2032 B |          NA |
| Blake2Fast                | 10000    |     6,246.34 ns |     197.050 ns |     10.801 ns |  1.36 |    0.00 | 0.0076 |      - |      88 B |          NA |
| SHA256                    | 10000    |     2,994.28 ns |      65.116 ns |      3.569 ns |  0.65 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 65536    |    26,085.55 ns |     473.545 ns |     25.957 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 65536    |    42,992.70 ns |  12,602.885 ns |    690.807 ns |  1.65 |    0.02 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 65536    |    31,172.85 ns |   1,403.355 ns |     76.923 ns |  1.20 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 65536    |    34,678.61 ns |   1,102.136 ns |     60.412 ns |  1.33 |    0.00 | 0.1831 |      - |    2032 B |          NA |
| Blake2Fast                | 65536    |    40,578.36 ns |   1,088.729 ns |     59.677 ns |  1.56 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                    | 65536    |    19,119.21 ns |     917.103 ns |     50.270 ns |  0.73 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 100000   |    40,564.94 ns |   1,032.091 ns |     56.572 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 100000   |    83,064.02 ns |  11,919.892 ns |    653.369 ns |  2.05 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 100000   |    48,730.08 ns |   1,605.163 ns |     87.984 ns |  1.20 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 100000   |    51,074.99 ns |   1,218.756 ns |     66.804 ns |  1.26 |    0.00 | 0.1831 |      - |    2032 B |          NA |
| Blake2Fast                | 100000   |    62,500.95 ns |     572.123 ns |     31.360 ns |  1.54 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                    | 100000   |    29,042.81 ns |     649.545 ns |     35.604 ns |  0.72 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 131072   |    52,814.87 ns |   1,446.209 ns |     79.272 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 131072   |    50,867.40 ns |  10,235.078 ns |    561.019 ns |  0.96 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 131072   |    63,088.51 ns |     874.194 ns |     47.918 ns |  1.19 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 131072   |    68,356.97 ns |   4,505.919 ns |    246.985 ns |  1.29 |    0.00 | 0.1221 |      - |    2032 B |          NA |
| Blake2Fast                | 131072   |    82,224.21 ns |   7,020.556 ns |    384.820 ns |  1.56 |    0.01 |      - |      - |      88 B |          NA |
| SHA256                    | 131072   |    38,135.92 ns |   2,443.146 ns |    133.917 ns |  0.72 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 262144   |   106,478.71 ns |   3,408.555 ns |    186.834 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 262144   |    61,529.79 ns |  13,136.312 ns |    720.046 ns |  0.58 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 262144   |   127,121.48 ns |   3,467.199 ns |    190.049 ns |  1.19 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 262144   |    39,862.71 ns |   1,349.791 ns |     73.987 ns |  0.37 |    0.00 | 0.7935 |      - |    7082 B |          NA |
| Blake2Fast                | 262144   |   165,279.44 ns |     925.884 ns |     50.751 ns |  1.55 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                    | 262144   |    76,187.40 ns |   4,852.938 ns |    266.006 ns |  0.72 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 524288   |   216,947.68 ns |  11,974.904 ns |    656.385 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 524288   |    78,209.23 ns |   6,374.411 ns |    349.403 ns |  0.36 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 524288   |   255,604.02 ns |   3,465.561 ns |    189.959 ns |  1.18 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 524288   |    72,289.16 ns |  14,233.826 ns |    780.204 ns |  0.33 |    0.00 | 1.3428 |      - |   10632 B |          NA |
| Blake2Fast                | 524288   |   330,627.79 ns |   8,922.170 ns |    489.054 ns |  1.52 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                    | 524288   |   152,092.51 ns |   2,935.931 ns |    160.928 ns |  0.70 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 1000000  |   417,014.26 ns |   2,345.747 ns |    128.578 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 1000000  |   271,067.89 ns |  72,822.569 ns |  3,991.651 ns |  0.65 |    0.01 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 1000000  |   489,391.44 ns |   1,964.795 ns |    107.697 ns |  1.17 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 1000000  |   106,021.74 ns |  21,381.775 ns |  1,172.007 ns |  0.25 |    0.00 | 1.8311 |      - |   14379 B |          NA |
| Blake2Fast                | 1000000  |   631,112.36 ns |  14,197.852 ns |    778.232 ns |  1.51 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                    | 1000000  |   294,554.46 ns |  13,246.935 ns |    726.109 ns |  0.71 |    0.00 |      - |      - |         - |          NA |
|                           |          |                 |                |               |       |         |        |        |           |             |
| 'Blake3 native'           | 10000000 | 4,203,279.94 ns | 209,883.309 ns | 11,504.412 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 native parallel'  | 10000000 |   664,717.87 ns | 149,454.074 ns |  8,192.082 ns |  0.16 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed'          | 10000000 | 4,933,668.62 ns |  30,826.288 ns |  1,689.693 ns |  1.17 |    0.00 |      - |      - |         - |          NA |
| 'Blake3 managed parallel' | 10000000 |   867,942.38 ns | 292,194.575 ns | 16,016.171 ns |  0.21 |    0.00 | 9.7656 | 1.9531 |   84831 B |          NA |
| Blake2Fast                | 10000000 | 6,311,448.89 ns |  94,187.113 ns |  5,162.713 ns |  1.50 |    0.00 |      - |      - |      88 B |          NA |
| SHA256                    | 10000000 | 2,991,769.21 ns | 111,094.488 ns |  6,089.464 ns |  0.71 |    0.00 |      - |      - |         - |          NA |
