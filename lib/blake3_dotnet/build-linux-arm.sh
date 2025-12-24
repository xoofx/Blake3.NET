#!/bin/sh
TARGET="arm-unknown-linux-gnueabihf"
BUILD="linux-arm"
rustup target add $TARGET
cargo build --release --target $TARGET
mkdir -p build/$BUILD/native
cp target/$TARGET/release/libblake3_dotnet.so build/$BUILD/native
arm-linux-gnueabihf-strip build/$BUILD/native/*.so