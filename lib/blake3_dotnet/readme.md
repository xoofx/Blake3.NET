# blake3_dotnet

BLAKE3 Rust libraries compiled to native and exposed as plain shared libraries.

## How to build?

You need to have [`rustup`](https://rustup.rs/) installed

Run one of the `build*` scripts (e.g `build-win-x64.ps1` or `build-linux-x64.sh`).

Musl targets are supported via:
- `build-linux-musl-x64.sh` (`x86_64-unknown-linux-musl`)
- `build-linux-musl-arm64.sh` (`aarch64-unknown-linux-musl`)