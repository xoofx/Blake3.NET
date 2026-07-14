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

| Method                    | N        | Mean            | Error          | StdDev        | Ratio | RatioSD | Code Size | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |--------- |----------------:|---------------:|--------------:|------:|--------:|----------:|-------:|----------:|------------:|
| 'Blake3 native'           | 4        |        75.80 ns |       2.742 ns |      0.150 ns |  1.00 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 4        |        96.53 ns |       2.637 ns |      0.145 ns |  1.27 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 4        |        82.21 ns |      19.361 ns |      1.061 ns |  1.08 |    0.01 |   2,279 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 4        |       163.56 ns |     261.523 ns |     14.335 ns |  2.16 |    0.16 |        NA | 0.1214 |    2032 B |          NA |
| Blake2Fast                | 4        |       137.54 ns |       8.722 ns |      0.478 ns |  1.81 |    0.01 |   5,453 B | 0.0052 |      88 B |          NA |
| SHA256                    | 4        |       115.78 ns |     196.735 ns |     10.784 ns |  1.53 |    0.12 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 100      |       142.40 ns |      12.513 ns |      0.686 ns |  1.00 |    0.01 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 100      |       153.55 ns |      10.883 ns |      0.597 ns |  1.08 |    0.01 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 100      |       132.32 ns |      19.674 ns |      1.078 ns |  0.93 |    0.01 |   2,272 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 100      |       234.82 ns |      67.876 ns |      3.721 ns |  1.65 |    0.02 |        NA | 0.1214 |    2032 B |          NA |
| Blake2Fast                | 100      |       134.94 ns |      18.923 ns |      1.037 ns |  0.95 |    0.01 |   5,450 B | 0.0052 |      88 B |          NA |
| SHA256                    | 100      |       138.56 ns |     146.566 ns |      8.034 ns |  0.97 |    0.05 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 1000     |       995.27 ns |      44.240 ns |      2.425 ns |  1.00 |    0.00 |     355 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 1000     |     1,001.95 ns |      18.530 ns |      1.016 ns |  1.01 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 1000     |     1,132.57 ns |     399.877 ns |     21.919 ns |  1.14 |    0.02 |   2,272 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 1000     |     1,375.29 ns |      84.049 ns |      4.607 ns |  1.38 |    0.00 |        NA | 0.1202 |    2032 B |          NA |
| Blake2Fast                | 1000     |       956.44 ns |     121.181 ns |      6.642 ns |  0.96 |    0.01 |   5,550 B | 0.0038 |      88 B |          NA |
| SHA256                    | 1000     |       449.12 ns |       3.816 ns |      0.209 ns |  0.45 |    0.00 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 10000    |     3,115.34 ns |      82.487 ns |      4.521 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 10000    |     3,157.38 ns |      45.903 ns |      2.516 ns |  1.01 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 10000    |     3,741.27 ns |     158.332 ns |      8.679 ns |  1.20 |    0.00 |   6,078 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 10000    |     4,233.85 ns |     332.888 ns |     18.247 ns |  1.36 |    0.01 |        NA | 0.1144 |    2032 B |          NA |
| Blake2Fast                | 10000    |     9,235.58 ns |     126.637 ns |      6.941 ns |  2.96 |    0.00 |   5,549 B |      - |      88 B |          NA |
| SHA256                    | 10000    |     3,697.00 ns |     131.098 ns |      7.186 ns |  1.19 |    0.00 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 65536    |     5,121.15 ns |     256.663 ns |     14.069 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 65536    |    37,223.51 ns |   1,851.871 ns |    101.507 ns |  7.27 |    0.02 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 65536    |     6,434.91 ns |   1,125.852 ns |     61.712 ns |  1.26 |    0.01 |   6,149 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 65536    |    19,186.93 ns |   6,681.266 ns |    366.223 ns |  3.75 |    0.06 |        NA | 0.0916 |    2032 B |          NA |
| Blake2Fast                | 65536    |    60,424.43 ns |   1,940.282 ns |    106.353 ns | 11.80 |    0.03 |   5,827 B |      - |      88 B |          NA |
| SHA256                    | 65536    |    23,838.49 ns |   1,304.229 ns |     71.489 ns |  4.65 |    0.02 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 100000   |     9,457.37 ns |   1,126.614 ns |     61.754 ns |  1.00 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 100000   |    57,637.99 ns |   4,277.860 ns |    234.484 ns |  6.09 |    0.04 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 100000   |    11,196.23 ns |   1,309.860 ns |     71.798 ns |  1.18 |    0.01 |   6,590 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 100000   |    14,199.48 ns |   7,723.410 ns |    423.346 ns |  1.50 |    0.04 |        NA | 0.1068 |    2032 B |          NA |
| Blake2Fast                | 100000   |    92,327.04 ns |   6,859.453 ns |    375.990 ns |  9.76 |    0.07 |   5,549 B |      - |      90 B |          NA |
| SHA256                    | 100000   |    36,375.42 ns |   1,056.754 ns |     57.924 ns |  3.85 |    0.02 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 131072   |    10,028.99 ns |   1,204.046 ns |     65.998 ns |  1.00 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 131072   |    68,613.48 ns |  11,945.912 ns |    654.796 ns |  6.84 |    0.07 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 131072   |    12,303.24 ns |   1,473.063 ns |     80.744 ns |  1.23 |    0.01 |   6,156 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 131072   |    26,593.60 ns |   7,409.869 ns |    406.160 ns |  2.65 |    0.04 |        NA | 0.0916 |    2032 B |          NA |
| Blake2Fast                | 131072   |   120,753.03 ns |   5,376.569 ns |    294.708 ns | 12.04 |    0.07 |   5,827 B |      - |      88 B |          NA |
| SHA256                    | 131072   |    47,474.33 ns |   1,930.577 ns |    105.821 ns |  4.73 |    0.03 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 262144   |    19,990.86 ns |     914.761 ns |     50.141 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 262144   |    70,163.42 ns |  17,521.705 ns |    960.424 ns |  3.51 |    0.04 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 262144   |    25,770.78 ns |   6,496.970 ns |    356.121 ns |  1.29 |    0.02 |   6,156 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 262144   |    36,703.18 ns |   4,171.513 ns |    228.655 ns |  1.84 |    0.01 |        NA | 0.2441 |    4962 B |          NA |
| Blake2Fast                | 262144   |   242,187.71 ns |   5,911.815 ns |    324.047 ns | 12.11 |    0.03 |   5,823 B |      - |      88 B |          NA |
| SHA256                    | 262144   |    95,353.51 ns |  10,032.142 ns |    549.896 ns |  4.77 |    0.03 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 524288   |    39,488.71 ns |   3,036.480 ns |    166.440 ns |  1.00 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 524288   |    17,998.78 ns |   3,137.964 ns |    172.002 ns |  0.46 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 524288   |    49,525.68 ns |  11,790.143 ns |    646.257 ns |  1.25 |    0.01 |   6,156 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 524288   |    49,223.40 ns |  11,643.140 ns |    638.200 ns |  1.25 |    0.01 |        NA | 0.3662 |    6158 B |          NA |
| Blake2Fast                | 524288   |   484,473.19 ns |  17,713.546 ns |    970.939 ns | 12.27 |    0.05 |   5,823 B |      - |      88 B |          NA |
| SHA256                    | 524288   |   189,652.06 ns |   1,642.776 ns |     90.046 ns |  4.80 |    0.02 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 1000000  |    76,814.91 ns |   2,324.927 ns |    127.437 ns |  1.00 |    0.00 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 1000000  |   225,470.52 ns |  30,270.007 ns |  1,659.201 ns |  2.94 |    0.02 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 1000000  |    93,914.75 ns |  13,165.180 ns |    721.628 ns |  1.22 |    0.01 |   6,612 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 1000000  |    28,859.87 ns |   2,069.081 ns |    113.413 ns |  0.38 |    0.00 |        NA | 0.5493 |    9133 B |          NA |
| Blake2Fast                | 1000000  |   923,363.25 ns |  36,955.748 ns |  2,025.669 ns | 12.02 |    0.03 |   5,539 B |      - |      88 B |          NA |
| SHA256                    | 1000000  |   362,081.12 ns |  13,172.901 ns |    722.051 ns |  4.71 |    0.01 |     342 B |      - |         - |          NA |
|                           |          |                 |                |               |       |         |           |        |           |             |
| 'Blake3 native'           | 10000000 |   773,463.20 ns |  80,683.008 ns |  4,422.508 ns |  1.00 |    0.01 |     360 B |      - |         - |          NA |
| 'Blake3 native parallel'  | 10000000 |   120,835.40 ns |   9,464.983 ns |    518.808 ns |  0.16 |    0.00 |        NA |      - |         - |          NA |
| 'Blake3 managed'          | 10000000 | 1,002,409.38 ns |  54,831.990 ns |  3,005.526 ns |  1.30 |    0.01 |   6,590 B |      - |         - |          NA |
| 'Blake3 managed parallel' | 10000000 |   229,875.78 ns |   1,821.866 ns |     99.863 ns |  0.30 |    0.00 |        NA | 1.7090 |   27908 B |          NA |
| Blake2Fast                | 10000000 | 9,296,369.53 ns | 292,683.481 ns | 16,042.969 ns | 12.02 |    0.06 |   5,829 B |      - |      88 B |          NA |
| SHA256                    | 10000000 | 3,640,666.41 ns | 484,814.553 ns | 26,574.322 ns |  4.71 |    0.04 |     342 B |      - |         - |          NA |


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
