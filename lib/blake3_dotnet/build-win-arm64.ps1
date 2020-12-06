#!/bin/sh
$TARGET="aarch64-pc-windows-msvc"
$BUILD="win-arm64"
rustup target add $TARGET
cargo build --release --target $TARGET
New-Item -ItemType Directory -Force -Path build/$BUILD/native
cp target/$TARGET/release/blake3_dotnet.dll build/$BUILD/native