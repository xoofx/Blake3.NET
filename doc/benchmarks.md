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

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | N        | Mean            | Error          | StdDev        | Ratio | RatioSD | Code Size | Gen0   | Allocated | Alloc Ratio |
|------------------------- |--------- |----------------:|---------------:|--------------:|------:|--------:|----------:|-------:|----------:|------------:|
| 'Blake3 native'          | 4        |        78.03 ns |       4.238 ns |      0.232 ns |  1.00 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel' | 4        |        96.51 ns |       3.809 ns |      0.209 ns |  1.24 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 4        |        84.85 ns |       1.861 ns |      0.102 ns |  1.09 |    0.00 |   2,138 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 4        |       144.79 ns |      21.233 ns |      1.164 ns |  1.86 |    0.01 |        NA | 0.1214 |    2032 B |          NA |
| 'Blake3 managed'         | 4        |        89.82 ns |       1.332 ns |      0.073 ns |  1.15 |    0.00 |   3,745 B |      - |         - |          NA |
| Blake2Fast               | 4        |       138.26 ns |       3.351 ns |      0.184 ns |  1.77 |    0.00 |   5,453 B | 0.0052 |      88 B |          NA |
| SHA256                   | 4        |       105.88 ns |       2.749 ns |      0.151 ns |  1.36 |    0.00 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 100      |       142.47 ns |       7.072 ns |      0.388 ns |  1.00 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel' | 100      |       150.16 ns |       1.429 ns |      0.078 ns |  1.05 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 100      |       128.18 ns |       7.727 ns |      0.424 ns |  0.90 |    0.00 |   2,131 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 100      |       229.31 ns |      35.790 ns |      1.962 ns |  1.61 |    0.01 |        NA | 0.1214 |    2032 B |          NA |
| 'Blake3 managed'         | 100      |       155.90 ns |       7.305 ns |      0.400 ns |  1.09 |    0.00 |   3,992 B |      - |         - |          NA |
| Blake2Fast               | 100      |       138.53 ns |       8.153 ns |      0.447 ns |  0.97 |    0.00 |   5,450 B | 0.0052 |      88 B |          NA |
| SHA256                   | 100      |       127.39 ns |      22.543 ns |      1.236 ns |  0.89 |    0.01 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 1000     |     1,027.29 ns |      52.876 ns |      2.898 ns |  1.00 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel' | 1000     |     1,036.40 ns |      73.308 ns |      4.018 ns |  1.01 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 1000     |     1,149.91 ns |      65.474 ns |      3.589 ns |  1.12 |    0.00 |   2,131 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 1000     |     1,398.89 ns |      51.127 ns |      2.802 ns |  1.36 |    0.00 |        NA | 0.1202 |    2032 B |          NA |
| 'Blake3 managed'         | 1000     |     1,085.29 ns |      47.888 ns |      2.625 ns |  1.06 |    0.00 |   3,992 B |      - |         - |          NA |
| Blake2Fast               | 1000     |       982.42 ns |      21.560 ns |      1.182 ns |  0.96 |    0.00 |   5,550 B | 0.0038 |      88 B |          NA |
| SHA256                   | 1000     |       458.65 ns |       2.029 ns |      0.111 ns |  0.45 |    0.00 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 10000    |     3,185.51 ns |      44.358 ns |      2.431 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 10000    |     3,223.63 ns |      41.111 ns |      2.253 ns |  1.01 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 10000    |     4,007.42 ns |      43.837 ns |      2.403 ns |  1.26 |    0.00 |   5,984 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 10000    |     4,301.43 ns |      80.051 ns |      4.388 ns |  1.35 |    0.00 |        NA | 0.1144 |    2032 B |          NA |
| 'Blake3 managed'         | 10000    |     3,917.23 ns |     228.655 ns |     12.533 ns |  1.23 |    0.00 |  18,271 B |      - |      56 B |          NA |
| Blake2Fast               | 10000    |     9,544.51 ns |     427.954 ns |     23.458 ns |  3.00 |    0.01 |   5,549 B |      - |      88 B |          NA |
| SHA256                   | 10000    |     3,779.22 ns |     140.295 ns |      7.690 ns |  1.19 |    0.00 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 65536    |     5,122.13 ns |     342.425 ns |     18.769 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 65536    |    36,894.41 ns |   1,408.865 ns |     77.225 ns |  7.20 |    0.03 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 65536    |     6,174.87 ns |     221.740 ns |     12.154 ns |  1.21 |    0.00 |   6,130 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 65536    |    18,692.68 ns |      17.009 ns |      0.932 ns |  3.65 |    0.01 |        NA | 0.0916 |    2032 B |          NA |
| 'Blake3 managed'         | 65536    |    16,272.94 ns |   1,092.418 ns |     59.879 ns |  3.18 |    0.01 |  18,170 B |      - |      56 B |          NA |
| Blake2Fast               | 65536    |    61,792.86 ns |   2,388.151 ns |    130.903 ns | 12.06 |    0.04 |   5,827 B |      - |      88 B |          NA |
| SHA256                   | 65536    |    24,282.38 ns |     219.083 ns |     12.009 ns |  4.74 |    0.02 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 100000   |     9,433.98 ns |     114.677 ns |      6.286 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 100000   |    58,304.09 ns |   2,233.122 ns |    122.405 ns |  6.18 |    0.01 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 100000   |    11,291.17 ns |     186.972 ns |     10.249 ns |  1.20 |    0.00 |   6,525 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 100000   |    13,854.43 ns |     176.447 ns |      9.672 ns |  1.47 |    0.00 |        NA | 0.1068 |    2032 B |          NA |
| 'Blake3 managed'         | 100000   |    16,029.59 ns |   5,140.495 ns |    281.768 ns |  1.70 |    0.03 |  30,681 B | 0.1221 |    2175 B |          NA |
| Blake2Fast               | 100000   |    94,057.97 ns |   2,361.547 ns |    129.444 ns |  9.97 |    0.01 |   5,549 B |      - |      88 B |          NA |
| SHA256                   | 100000   |    36,998.63 ns |     775.331 ns |     42.498 ns |  3.92 |    0.00 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 131072   |    10,004.22 ns |     351.422 ns |     19.263 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 131072   |    68,344.33 ns |   4,685.956 ns |    256.853 ns |  6.83 |    0.02 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 131072   |    13,040.14 ns |     278.996 ns |     15.293 ns |  1.30 |    0.00 |   6,130 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 131072   |    26,507.63 ns |     648.928 ns |     35.570 ns |  2.65 |    0.01 |        NA | 0.0916 |    2032 B |          NA |
| 'Blake3 managed'         | 131072   |    18,475.52 ns |  13,998.442 ns |    767.302 ns |  1.85 |    0.07 |  29,897 B | 0.1221 |    2438 B |          NA |
| Blake2Fast               | 131072   |   124,302.80 ns |   8,533.007 ns |    467.723 ns | 12.43 |    0.05 |   5,827 B |      - |      88 B |          NA |
| SHA256                   | 131072   |    48,515.98 ns |     942.871 ns |     51.682 ns |  4.85 |    0.01 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 262144   |    19,860.82 ns |   1,214.385 ns |     66.565 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 262144   |    59,860.90 ns |  18,020.210 ns |    987.749 ns |  3.01 |    0.04 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 262144   |    25,584.75 ns |     833.530 ns |     45.689 ns |  1.29 |    0.00 |   6,130 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 262144   |    33,118.78 ns |     720.625 ns |     39.500 ns |  1.67 |    0.01 |        NA | 0.2441 |    4886 B |          NA |
| 'Blake3 managed'         | 262144   |    19,686.28 ns |   1,842.691 ns |    101.004 ns |  0.99 |    0.01 |  29,949 B | 0.1526 |    2877 B |          NA |
| Blake2Fast               | 262144   |   250,294.92 ns |  12,197.389 ns |    668.580 ns | 12.60 |    0.05 |   5,823 B |      - |      88 B |          NA |
| SHA256                   | 262144   |    97,539.01 ns |      98.688 ns |      5.409 ns |  4.91 |    0.01 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 524288   |    39,770.43 ns |     968.684 ns |     53.097 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 524288   |    20,037.35 ns |   1,606.505 ns |     88.058 ns |  0.50 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 524288   |    47,559.11 ns |   1,799.340 ns |     98.628 ns |  1.20 |    0.00 |   6,130 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 524288   |    42,653.38 ns |   2,077.727 ns |    113.887 ns |  1.07 |    0.00 |        NA | 0.3662 |    6150 B |          NA |
| 'Blake3 managed'         | 524288   |    33,026.14 ns |  13,535.738 ns |    741.939 ns |  0.83 |    0.02 |  30,017 B | 0.1831 |    3293 B |          NA |
| Blake2Fast               | 524288   |   499,768.59 ns |  28,593.408 ns |  1,567.301 ns | 12.57 |    0.04 |   5,826 B |      - |      88 B |          NA |
| SHA256                   | 524288   |   193,635.29 ns |   2,381.735 ns |    130.551 ns |  4.87 |    0.01 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 1000000  |    76,590.27 ns |   3,725.673 ns |    204.217 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 1000000  |   263,517.04 ns |  48,093.261 ns |  2,636.154 ns |  3.44 |    0.03 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 1000000  |    92,275.73 ns |   1,599.019 ns |     87.648 ns |  1.20 |    0.00 |   6,518 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 1000000  |    27,143.73 ns |     865.607 ns |     47.447 ns |  0.35 |    0.00 |        NA | 0.5493 |    9017 B |          NA |
| 'Blake3 managed'         | 1000000  |    34,336.42 ns |  84,504.830 ns |  4,631.995 ns |  0.45 |    0.05 |  29,525 B | 0.1831 |    3999 B |          NA |
| Blake2Fast               | 1000000  |   946,132.58 ns |   3,595.098 ns |    197.059 ns | 12.35 |    0.03 |   5,542 B |      - |      88 B |          NA |
| SHA256                   | 1000000  |   369,130.84 ns |   9,392.071 ns |    514.811 ns |  4.82 |    0.01 |     342 B |      - |         - |          NA |
|                          |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'          | 10000000 |   787,261.80 ns |  35,564.851 ns |  1,949.430 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel' | 10000000 |   146,727.86 ns |  36,654.685 ns |  2,009.167 ns |  0.19 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 sharp'           | 10000000 |   916,980.35 ns |   9,272.492 ns |    508.257 ns |  1.16 |    0.00 |   6,496 B |      - |         - |          NA |
| 'Blake3 sharp parallel'  | 10000000 |   218,620.40 ns |  16,976.678 ns |    930.549 ns |  0.28 |    0.00 |        NA | 1.7090 |   27777 B |          NA |
| 'Blake3 managed'         | 10000000 |   211,337.98 ns |  11,121.311 ns |    609.597 ns |  0.27 |    0.00 |  37,786 B | 0.2441 |    7381 B |          NA |
| Blake2Fast               | 10000000 | 9,451,634.38 ns | 722,348.749 ns | 39,594.373 ns | 12.01 |    0.05 |   5,823 B |      - |      88 B |          NA |
| SHA256                   | 10000000 | 3,692,137.37 ns |  47,671.419 ns |  2,613.031 ns |  4.69 |    0.01 |     342 B |      - |         - |          NA |


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
