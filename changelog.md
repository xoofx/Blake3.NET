# Changelog

## 0.5.0
- Update to BLAKE3 1.2.0
- Add `Hasher.Finalize(long offset, Span<byte> hash)`

## 0.4.0
- Breaking change: The method `Hash.AsSpan` is renamed to `Hash.AsSpanUnsafe`
- Update to BLAKE3 1.1.0
- Add osx-arm64 binaries

## 0.3.0

- Add support for `netstandard2.0`
- Add support for `Hasher.NewKeyed` and `Hasher.NewDeriveKey`

## 0.2.1

- Set preemptive limit to 1024 bytes instead of 64Kb

## 0.2.0

- Add Blake3HashAlgorithm class.

## 0.1.0

Initial version.