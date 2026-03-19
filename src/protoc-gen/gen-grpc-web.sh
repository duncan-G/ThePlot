#!/bin/bash

INITIAL_CWD="$(pwd)"
proto_file_path=""
output_directory_path=""
file_name=""

usage() {
    echo "Usage: $0 -i <file_path> [-o <output_directory_path>] [-f <file_name>]" 1>&2
    exit 1
}

# Parse command line options
while getopts ":i:o:f:" opt; do
    case ${opt} in
        i )
            proto_file_path=$OPTARG
            ;;
        o )
            output_directory_path=$OPTARG
            ;;
        f )
            file_name=$OPTARG
            ;;
        \? )
            echo "Invalid option: $OPTARG" 1>&2
            usage
            ;;
        : )
            echo "Invalid option: $OPTARG requires an argument" 1>&2
            usage
            ;;
    esac
done
shift $((OPTIND -1))

# Check if input file path is provided
if [ -z "$proto_file_path" ]; then
    echo "Error Input file path is required"
    usage
    exit 1
fi

# Check if input file_ path exists
if [ ! -f "$proto_file_path" ]; then
    echo "Error: $proto_file_path does not exist."
    exit 1
fi

# Check if proto_file_path is a protobuff file"
if [[ ! "$proto_file_path" == *.proto ]]; then
    echo "Error: Input file does not seem to be a protobuff file. It must have a .proto extension." >&2
    exit 1
fi

# Check if output directory is provided
if [ -z "$output_directory_path" ]; then
    echo "Error Output directory is required"
    usage
    exit 1
fi

# Check if output directory exists
if [ ! -d "$output_directory_path" ]; then
    echo "Error: $output_directory_path does not exist."
    exit 1
fi

# Get file name from path
if [ -z "$file_name" ]; then
    file_name=$(basename "$proto_file_path")
fi

# Since generation from a proto file will generate multiple files,
# we need to create a subdirectory in the output directory to store all the generated files.
# We will use the file name without the .proto extension as the subdirectory name.
output_sub_directory=${file_name%.proto}
mkdir -p "$output_directory_path/$output_sub_directory"

# Get proto file parent directoy
proto_directory_path=$(dirname "$proto_file_path")

# Resolve repo root from script location and ensure we run from there
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

# Convert paths to absolute for Docker volume mounts (Docker requires absolute paths)
# Paths may be relative to initial cwd (e.g. client/ when run via npm)
PROTO_DIR_ABS="$(cd "$INITIAL_CWD" && cd "$proto_directory_path" && pwd)"
OUTPUT_DIR_ABS="$(cd "$INITIAL_CWD" && cd "$output_directory_path" && pwd)"

# Build image if not present
if ! docker image inspect protoc-gen-grpc-web:latest >/dev/null 2>&1; then
    echo "Building protoc-gen-grpc-web image..."
    docker build -t protoc-gen-grpc-web:latest "$REPO_ROOT/src/protoc-gen"
fi

docker run --rm \
    -v "$PROTO_DIR_ABS":/home/protos \
    -v "$OUTPUT_DIR_ABS":/home/api \
    protoc-gen-grpc-web:latest -I="/home/protos" "$file_name" \
    --plugin=protoc-gen-js=/usr/local/bin/protoc-gen-js \
    --js_out=import_style=commonjs:"/home/api/$output_sub_directory" \
    --grpc-web_out=import_style=typescript,mode=grpcwebtext:"/home/api/$output_sub_directory"

echo "Finished generating grpc services"
