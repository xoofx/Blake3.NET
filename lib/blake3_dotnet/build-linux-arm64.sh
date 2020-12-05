#!/bin/sh
TARGET="aarch64-unknown-linux-gnu"
BUILD="linux-arm64"
rustup target add $TARGET
cargo build --release --target $TARGET
mkdir -p build/$BUILD
cp target/$TARGET/release/libblake3_dotnet.so build/$BUILD
strip build/$BUILD/*.so