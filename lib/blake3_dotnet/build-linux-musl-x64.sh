#!/bin/sh
set -eu

TARGET="x86_64-unknown-linux-musl"
BUILD="linux-musl-x64"

rustup target add "$TARGET"
cargo build --release --target "$TARGET"

mkdir -p "build/$BUILD/native"
cp "target/$TARGET/release/libblake3_dotnet.so" "build/$BUILD/native"

if command -v strip >/dev/null 2>&1; then
  strip "build/$BUILD/native"/*.so || true
fi
