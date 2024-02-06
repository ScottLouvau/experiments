# Build Rust toolchain image
FROM rust:latest AS build-base
RUN apt update -y && apt upgrade -y

# Code Coverage Tools
RUN rustup component add llvm-tools-preview
RUN cargo install cargo-llvm-cov

# Cross-Compile for Windows x64
RUN apt install -y g++-mingw-w64-x86-64
RUN rustup target add x86_64-pc-windows-gnu
RUN rustup toolchain install stable-x86_64-pc-windows-gnu --force-non-host

# Cross-Compile for MacOS (Apple Silicon)
# Not working. 
# How is ripgrep built for MacOS? https://github.com/BurntSushi/ripgrep
#RUN rustup target add aarch64-apple-darwin
#RUN rustup toolchain install stable-aarch64-apple-darwin --force-non-host

FROM build-base AS builder
WORKDIR /usr/local/app
COPY Cargo.toml Cargo.lock ./
COPY ./src ./src
COPY ./data ./data
RUN cargo build --release
RUN cargo test > unit-tests.log
RUN cargo llvm-cov --lcov --output-path lcov.info

RUN cargo build --release --target=x86_64-pc-windows-gnu
#RUN cargo build --release --target=aarch64-apple-darwin

# docker build -t scottlouvau/qwertle .
