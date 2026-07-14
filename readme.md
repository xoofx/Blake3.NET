# Blake3<span color="A80016">.NET</span> [![managed](https://github.com/xoofx/Blake3.NET/actions/workflows/managed.yml/badge.svg)](https://github.com/xoofx/Blake3.NET/actions/workflows/managed.yml) [![native](https://github.com/xoofx/Blake3.NET/actions/workflows/native.yml/badge.svg)](https://github.com/xoofx/Blake3.NET/actions/workflows/native.yml) [![NuGet](https://img.shields.io/nuget/v/Blake3.svg)](https://www.nuget.org/packages/Blake3/) [![NuGet Managed](https://img.shields.io/nuget/v/Blake3.Managed.svg)](https://www.nuget.org/packages/Blake3.Managed/)

<img align="right" width="160px" height="160px" src="img/logo.png">

Blake3.NET provides both a fast managed wrapper around the SIMD Rust implementations and a fully managed .NET implementation of the [BLAKE3](https://github.com/BLAKE3-team/BLAKE3) cryptographic hash function.

> The current _native_ version of BLAKE3 used by Blake3.NET is `1.8.2`

## Features

- Compatible with .NET 10.
- `Span`-friendly API with either a native or fully managed implementation.
- API similar to the [Blake3 Rust API](https://docs.rs/blake3/1.4.1/blake3/).
- CPU SIMD Hardware accelerated with dynamic CPU feature detection.
  - Multiple [platforms](#platforms) supported.
- Incremental update API via `Hasher`.
- Fully managed regular, keyed, derive-key, incremental, and XOF hashing in the `Blake3.Managed` package.
- Support for multi-threaded hashing via `Hasher.UpdateWithJoin` in both packages.

## Packages

Choose one of the two packages. Both expose the same hashing API and type names in the `Blake3`
namespace, including `Blake3.Hasher`, `Blake3.Hash`, `Blake3HashAlgorithm`, and `Blake3Stream`:

```console
dotnet add package Blake3
# or, for a fully managed implementation with no native library:
dotnet add package Blake3.Managed
```

The `Blake3` package uses the optimized Rust implementation through native interop. The
`Blake3.Managed` package uses runtime-selected scalar and 128/256/512-bit SIMD code implemented
entirely in .NET. Switching packages does not require source changes. A project should normally
reference only one package because both intentionally define the same fully qualified public types.

## Usage

Hash a buffer directly:

```c#
var hash = Blake3.Hasher.Hash(Encoding.UTF8.GetBytes("BLAKE3"));
Console.WriteLine(hash);
// Prints f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4
```

Or use the disposable `Hasher` type for incremental updates:

```c#
using var hasher = Blake3.Hasher.New();
hasher.Update(Encoding.UTF8.GetBytes("BLAKE3"));
var hash = hasher.Finalize();
```

With `Blake3.Managed`, `Hasher.UpdateWithJoin` hashes large aligned subtrees in parallel and preserves
the exact incremental semantics of `Update`; smaller inputs stay on the serial path to avoid scheduling
overhead.

Or seek in the output "stream" to any position:

```c#
using var hasher = Blake3.Hasher.New();
hasher.Update(Encoding.UTF8.GetBytes("BLAKE3"));
var hashAtPosition = new byte[1024];
hasher.Finalize(4242, hashAtPosition);
```

Or hash a stream on the go with `Blake3Stream`:

```c#
using var blake3Stream = new Blake3Stream(new MemoryStream());
blake3Stream.Write(Encoding.UTF8.GetBytes("BLAKE3"));
var hash = blake3Stream.ComputeHash();
```

Or produce a message authentication code using a 256-bit key:

```c#
using var blake3 = Hasher.NewKeyed(macKey);
blake3.UpdateWithJoin(message);
var tag = blake3.Finalize();
byte[] authenticationTag = tag.AsSpan().ToArray();
````

Or derive a subkey from a master key:

```c#
const string context = "[application] [commit timestamp] [purpose]";
using var blake3 = Hasher.NewDeriveKey(Encoding.UTF8.GetBytes(context));
blake3.Update(inputKeyingMaterial);
var derivedKey = blake3.Finalize();
byte[] subkey = derivedKey.AsSpan().ToArray();
```

## Platforms

The native `Blake3` package is supported on the following platforms:

- `win-x64`, `win-x86`, `win-arm64`, `win-arm`
- `linux-x64`, `linux-arm64`, `linux-arm`, `linux-musl-x64`, `linux-musl-arm64`
- `osx-x64`, `osx-arm64`

The `Blake3.Managed` package has no native runtime dependency and can run on any platform supported by
.NET 10. Hardware intrinsics are selected at runtime, with a scalar fallback when SIMD is unavailable.

## Benchmarks

These benchmarks use [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet/) on .NET 10
to compare several input sizes and implementations:

- `Blake3`, which calls the SIMD-optimized Rust implementation through native interop.
- `Blake3.Managed`, a portable .NET implementation with runtime-selected SIMD acceleration and no
  native code dependency.
- [Blake2Fast](https://github.com/saucecontrol/Blake2Fast).
- `System.Security.Cryptography.SHA256`.

Both BLAKE3 packages are measured in serial mode and in parallel mode using `Hasher.UpdateWithJoin`.

> **Highlights**
>
> - `Blake3.Managed` is competitive with the native Rust implementation: across the serial results
>   below, it ranges from about **7% faster to 30% slower**. It is a compelling option when portability
>   and avoiding native binaries matter more than always getting the highest possible throughput.
> - Parallel hashing has scheduling and coordination overhead, so serial hashing is preferable for
>   small inputs. In this run, parallel hashing starts to become competitive at around **512 KiB** and
>   provides its clearest gains for **multi-megabyte inputs**. As the crossover depends on the CPU,
>   available cores, runtime, and input shape, benchmark representative data before choosing it for a
>   hot path.
> - For large inputs, both BLAKE3 implementations substantially outperform the built-in SHA256 on this
>   machine. Relative performance will vary, especially with the CPU's SHA and SIMD capabilities.

### Results

The following benchmark was run on an AMD Ryzen 9 9950X:

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

## How to Build?

You need to install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or latest Visual Studio 2026. Then from the root folder:

```console
$ dotnet build src -c Release
```

In order to rebuild the native binaries, you need to run the build scripts from [lib/blake3_dotnet](lib/blake3_dotnet/readme.md)

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
