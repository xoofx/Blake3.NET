#!/bin/sh
TARGET="aarch64-apple-darwin"
BUILD="osx-arm64"
rustup target add $TARGET
cargo build --release --target $TARGET
mkdir -p build/$BUILD
cp target/$TARGET/release/libblake3_dotnet.dylib build/$BUILD