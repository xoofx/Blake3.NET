# Blake3.NET [![Build Status](https://github.com/xoofx/Blake3.NET/workflows/managed/badge.svg?branch=master)](https://github.com/xoofx/Blake3.NET/actions) [![Build Status](https://github.com/xoofx/Blake3.NET/workflows/native/badge.svg?branch=master)](https://github.com/xoofx/Blake3.NET/actions) [![NuGet](https://img.shields.io/nuget/v/Blake3.svg)](https://www.nuget.org/packages/Blake3/)

<img align="right" width="160px" height="160px" src="img/logo.png">

Blake3.NET is a fast managed wrapper around the SIMD Rust implementations of the [BLAKE3](https://github.com/BLAKE3-team/BLAKE3) cryptographic hash function.

## Usage

Hash directly a buffer:

```c#
var hash = Blake3.Hasher.Hash(Encoding.UTF8.GetBytes("BLAKE3"));
Console.WriteLine(hash);
// Prints f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4
```

Or use the `Hasher` struct for incremental updates:

```c#
// Hasher is a disposable struct!
using var hasher = Blake3.Hasher.New();
hasher.Update(Encoding.UTF8.GetBytes("BLAKE3"));
var hash = hasher.Finalize();
```

Or hash a stream on the go with `Blake3Stream`:

```c#
using var blake3Stream = new Blake3Stream(new MemoryStream());
blake3Stream.Write(Encoding.UTF8.GetBytes("BLAKE3"));
var hash = blake3Stream.ComputeHash();
```
## Platforms

Blake3.NET is supporting **.NET 5.0+** on the following platforms:

- Windows x64
- Windows x86
- Windows ARM64
- Windows ARM
- Linux x64
- Linux ARM64
- Linux ARM
- OSX x64

## How to Build?

You need to install the [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0). Then from the root folder:

```console
$ dotnet build src -c Release
```

In order to rebuild the native binaries, you need to run the build scripts from [lib/blake3_dotnet](lib/blake3_dotnet/readme.md)

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).
