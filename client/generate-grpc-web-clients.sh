#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROTOC_GEN="$REPO_ROOT/src/protoc-gen"
cd "$REPO_ROOT"

mkdir -p client/src/lib/services

# gRPC-Web client generation
bash "$PROTOC_GEN/gen-grpc-web.sh" -i src/ThePlot.Api.Grpc/Protos/greet.proto -o client/src/lib/services
bash "$PROTOC_GEN/gen-grpc-web.sh" -i src/ThePlot.Api.Grpc/Protos/upload.proto -o client/src/lib/services
bash "$PROTOC_GEN/gen-grpc-web.sh" -i src/ThePlot.Api.Grpc/Protos/screenplay.proto -o client/src/lib/services

# Error codes from C# to TypeScript
node "$PROTOC_GEN/generate_error_codes.js"
