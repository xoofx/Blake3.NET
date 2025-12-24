#!/bin/sh
TARGET="x86_64-unknown-linux-gnu"
BUILD="linux-x64"
rustup target add $TARGET
cargo build --release --target $TARGET
mkdir -p build/$BUILD/native
cp target/$TARGET/release/libblake3_dotnet.so build/$BUILD/native
strip build/$BUILD/native/*.so