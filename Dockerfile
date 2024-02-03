ARG PROD_IMAGE=gcr.io/distroless/cc-debian12
# gcr.io/distroless/cc-debian12         [34.6 MB; default Rust build works]
# gcr.io/distroless/cc-debian12:debug   [38.6 MB; default build; adds console access]
# ubuntu:latest                         [69.8 MB; default Rust build works; vulnerabilities]

# Build Rust toolchain image
FROM rust:latest AS build-base
RUN apt update -y && apt install -y llvm-dev libclang-dev
RUN rustup component add llvm-tools-preview --toolchain 1.75.0
RUN cargo install cargo-llvm-cov

WORKDIR /usr/local/app
COPY Cargo.toml Cargo.lock ./
COPY ./src ./src
COPY ./data ./data
RUN cargo build --release
RUN cargo test > unit-tests.log
RUN cargo llvm-cov --lcov --output-path lcov.info

# docker build -t scottlouvau/qwertle .
