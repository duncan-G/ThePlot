#!/bin/sh
set -e

# Resolve placeholders from Aspire-injected env vars into envoy config.
# OTEL_COLLECTOR_GRPC_ENDPOINT     -> direct gRPC endpoint of the OTEL Collector sidecar
# PARSER_ENDPOINT             -> http://aspire.dev.internal:<port>
# CORS_ORIGIN_EXACT           -> exact origin for CORS (e.g. https://localhost:4200)
# CORS_ORIGIN_SUBDOMAIN_REGEX           -> regex or glob for CORS (e.g. https://*.dev.localhost:4200)
# TLS_CERT_PATH               -> PEM cert for TLS/QUIC listeners (Aspire dev cert in dev; baked-in self-signed in prod)
# TLS_KEY_PATH                -> PEM key for TLS/QUIC listeners (Aspire dev cert in dev; baked-in self-signed in prod)

# Production fallback: Aspire dev cert is unavailable in publish mode; use baked-in self-signed cert
if [ -z "$TLS_CERT_PATH" ] || [ -z "$TLS_KEY_PATH" ]; then
  TLS_CERT_PATH="${TLS_CERT_PATH:-/etc/envoy/tls/cert.pem}"
  TLS_KEY_PATH="${TLS_KEY_PATH:-/etc/envoy/tls/key.pem}"
  export TLS_CERT_PATH TLS_KEY_PATH
  echo "INFO: Using baked-in self-signed TLS cert (TLS_CERT_PATH/TLS_KEY_PATH were unset)" >&2
fi

# OTEL_INSTANCE_ID: required for service.instance.id; fallback to hostname in prod
OTEL_INSTANCE_ID="${OTEL_INSTANCE_ID:-$(hostname 2>/dev/null || echo 'envoy-proxy')}"
export OTEL_INSTANCE_ID

# Extract host/port; supports https://host:port, http://host:port, and host:port
OTEL_GRPC_HOST=$(echo "$OTEL_COLLECTOR_GRPC_ENDPOINT" | sed -E 's|^https?://||; s|:.*||')
OTEL_GRPC_PORT=$(echo "$OTEL_COLLECTOR_GRPC_ENDPOINT" | sed -E 's|.*:([0-9]+)(/.*)?$|\1|')

OTEL_HTTP_HOST=$(echo "$OTEL_COLLECTOR_HTTP_ENDPOINT" | sed -E 's|^https?://||; s|:.*||')
OTEL_HTTP_PORT=$(echo "$OTEL_COLLECTOR_HTTP_ENDPOINT" | sed -E 's|.*:([0-9]+)(/.*)?$|\1|')

GRPC_API_HOST=$(echo "$GRPC_ENDPOINT" | sed -E 's|^https?://||; s|:.*||')
GRPC_API_PORT=$(echo "$GRPC_ENDPOINT" | sed -E 's|.*:([0-9]+)(/.*)?$|\1|')
[ -z "$GRPC_API_HOST" ] && GRPC_API_HOST="api-grpc-service"
[ -z "$GRPC_API_PORT" ] && GRPC_API_PORT="7234"

# Emit warnings for unset/empty required vars (no defaults; errors will surface downstream)
[ -z "$OTEL_COLLECTOR_GRPC_ENDPOINT" ] && echo "WARNING: OTEL_COLLECTOR_GRPC_ENDPOINT is unset" >&2
[ -z "$OTEL_GRPC_HOST" ] && echo "WARNING: OTEL_GRPC_HOST is empty (from OTEL_COLLECTOR_GRPC_ENDPOINT)" >&2
[ -z "$OTEL_GRPC_PORT" ] && echo "WARNING: OTEL_GRPC_PORT is empty (from OTEL_COLLECTOR_GRPC_ENDPOINT)" >&2
[ -z "$GRPC_ENDPOINT" ] && echo "WARNING: GRPC_ENDPOINT is unset" >&2
[ -z "$GRPC_API_HOST" ] && echo "WARNING: GRPC_API_HOST is empty (from GRPC_ENDPOINT)" >&2
[ -z "$GRPC_API_PORT" ] && echo "WARNING: GRPC_API_PORT is empty (from GRPC_ENDPOINT)" >&2
[ -z "$CORS_ORIGIN_EXACT" ] && echo "WARNING: CORS_ORIGIN_EXACT is unset" >&2
[ -z "$CORS_ORIGIN_SUBDOMAIN_REGEX" ] && echo "WARNING: CORS_ORIGIN_SUBDOMAIN_REGEX is unset" >&2
[ -z "$ALLOWED_HOSTS" ] && echo "WARNING: ALLOWED_HOSTS is unset" >&2

# CORS: regex (glob like https://*.localhost:4200 -> RE2 regex)
if [ -n "$CORS_ORIGIN_SUBDOMAIN_REGEX" ]; then
  case "$CORS_ORIGIN_SUBDOMAIN_REGEX" in
    *"*"*)
      # Replace literal dots with \. and * with valid multi-level subdomain characters
      CORS_ORIGIN_SUBDOMAIN_REGEX="^$(echo "$CORS_ORIGIN_SUBDOMAIN_REGEX" | sed 's/\./\\./g;s/\*/[a-zA-Z0-9.-]+/g')\$"
      ;;
    *)
      # Already a regex; use as-is
      ;;
  esac
fi

# ALLOWED_HOSTS: comma-separated domains for virtual_host
ALLOWED_HOSTS_DOMAINS=$(echo "$ALLOWED_HOSTS" | sed 's/^/["/' | sed 's/,/", "/g' | sed 's/$/"]/')

# Alt-Svc port for HTTP/3: extract from ALLOWED_HOSTS (format: host:port or host1:port,host2:port)
ALT_SVC_PORT=${ALT_SVC_PORT:-$(echo "$ALLOWED_HOSTS" | grep -oE ':[0-9]+' | head -1 | tr -d ':')}
ALT_SVC_PORT=${ALT_SVC_PORT:-11000}

# Use | as delimiter throughout to avoid sed errors when values contain /
sed \
  -e "s|__OTEL_GRPC_HOST__|${OTEL_GRPC_HOST}|g" \
  -e "s|__OTEL_GRPC_PORT__|${OTEL_GRPC_PORT}|g" \
  -e "s|__OTEL_HTTP_HOST__|${OTEL_HTTP_HOST}|g" \
  -e "s|__OTEL_HTTP_PORT__|${OTEL_HTTP_PORT}|g" \
  -e "s|__OTEL_INSTANCE_ID__|${OTEL_INSTANCE_ID}|g" \
  -e "s|__GRPC_API_HOST__|${GRPC_API_HOST}|g" \
  -e "s|__GRPC_API_PORT__|${GRPC_API_PORT}|g" \
  -e "s|__CORS_ORIGIN_EXACT__|${CORS_ORIGIN_EXACT}|g" \
  -e "s|__CORS_ORIGIN_SUBDOMAIN_REGEX__|${CORS_ORIGIN_SUBDOMAIN_REGEX}|g" \
  -e "s|__ALLOWED_HOSTS__|$(echo "$ALLOWED_HOSTS_DOMAINS" | sed 's|"|\\"|g')|g" \
  -e "s|__ALT_SVC_PORT__|${ALT_SVC_PORT}|g" \
  -e "s|__TLS_CERT_PATH__|${TLS_CERT_PATH}|g" \
  -e "s|__TLS_KEY_PATH__|${TLS_KEY_PATH}|g" \
  /etc/envoy/envoy.yaml.tmpl > /tmp/envoy.yaml

echo "--- entrypoint.sh: OTEL=$OTEL_GRPC_HOST:$OTEL_GRPC_PORT instance=$OTEL_INSTANCE_ID grpc=$GRPC_API_HOST:$GRPC_API_PORT cors_exact=$CORS_ORIGIN_EXACT hosts=$ALLOWED_HOSTS tls_cert=$TLS_CERT_PATH ---"
exec envoy -c /tmp/envoy.yaml "$@"
