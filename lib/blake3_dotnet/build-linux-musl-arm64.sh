#!/bin/sh
set -eu

TARGET="aarch64-unknown-linux-musl"
BUILD="linux-musl-arm64"

rustup target add "$TARGET"
cargo build --release --target "$TARGET"

mkdir -p "build/$BUILD/native"
cp "target/$TARGET/release/libblake3_dotnet.so" "build/$BUILD/native"

# Prefer a target-specific strip if available, otherwise fall back.
if command -v aarch64-linux-musl-strip >/dev/null 2>&1; then
  aarch64-linux-musl-strip "build/$BUILD/native"/*.so
elif command -v llvm-strip >/dev/null 2>&1; then
  llvm-strip "build/$BUILD/native"/*.so || true
elif command -v strip >/dev/null 2>&1; then
  strip "build/$BUILD/native"/*.so || true
fi
