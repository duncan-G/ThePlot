FROM debian:bookworm-slim

ARG TARGETARCH

ENV DEBIAN_FRONTEND=noninteractive

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        ca-certificates curl unzip wget; \
    \
    case "${TARGETARCH}" in \
        amd64)  ARCH="x86_64"  GRPC_WEB_ARCH="x86_64" ;; \
        arm64)  ARCH="aarch_64" GRPC_WEB_ARCH="aarch64" ;; \
        *) echo "Unsupported architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    \
    ## protoc‑gen‑js (protobuf‑javascript)
    curl -sSL -o /tmp/protobuf-js.zip \
        "https://github.com/protocolbuffers/protobuf-javascript/releases/download/v3.21.4/protobuf-javascript-3.21.4-linux-${ARCH}.zip"; \
    unzip -q -j /tmp/protobuf-js.zip "bin/protoc-gen-js" -d /usr/local/bin; \
    chmod +x /usr/local/bin/protoc-gen-js; \
    rm /tmp/protobuf-js.zip; \
    \
    ## protoc
    curl -sSL -o /tmp/protoc.zip \
        "https://github.com/protocolbuffers/protobuf/releases/download/v31.0/protoc-31.0-linux-${ARCH}.zip"; \
    unzip -q -j /tmp/protoc.zip "bin/protoc"         -d /usr/local/bin; \
    unzip -q    /tmp/protoc.zip "include/*"          -d /usr/local; \
    rm /tmp/protoc.zip; \
    \
    ## grpc‑web plugin
    wget -q "https://github.com/grpc/grpc-web/releases/download/1.5.0/protoc-gen-grpc-web-1.5.0-linux-${GRPC_WEB_ARCH}" \
            -O /usr/local/bin/protoc-gen-grpc-web; \
    chmod +x /usr/local/bin/protoc-gen-grpc-web; \
    \
    ## Clean‑up
    apt-get purge -y --auto-remove curl unzip wget; \
    rm -rf /var/lib/apt/lists/*

# ---------------------------------------------------------------------------
# Runtime tweaks
RUN useradd -u 1000 -m -d /home/protocuser protocuser
USER protocuser
WORKDIR /home/protocuser

ENTRYPOINT ["protoc"]
