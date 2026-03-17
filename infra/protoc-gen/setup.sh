#!/bin/bash

working_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$working_dir"

# Build protoc-gen image for grpc-web code generation
echo "Building protoc-gen-grpc-web image..."
docker build -t protoc-gen-grpc-web:latest ./infra/protoc-gen

# Install npm dependencies
echo "Installing client dependencies..."
cd client
npm ci
cd "$working_dir"

echo "Setup complete."
