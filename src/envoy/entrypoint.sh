#!/bin/sh
set -e

OTEL_INSTANCE_ID="${OTEL_INSTANCE_ID:-$(hostname 2>/dev/null || echo 'envoy-proxy')}"
export OTEL_INSTANCE_ID

# Extract host/port from URL; defaults port to 443 (https) or 80 (http)
parse_host() { echo "$1" | sed -E 's|^https?://||; s|[:/].*||'; }
parse_port() {
  _p=$(echo "$1" | sed -E 's|.*:([0-9]+)(/.*)?$|\1|')
  case "$_p" in ''|*[!0-9]*) echo "$1" | grep -qE '^https://' && echo 443 || echo 80 ;; *) echo "$_p" ;; esac
}

OTEL_GRPC_HOST=$(parse_host "$OTEL_COLLECTOR_GRPC_ENDPOINT")
OTEL_GRPC_PORT=$(parse_port "$OTEL_COLLECTOR_GRPC_ENDPOINT")
OTEL_HTTP_HOST=$(parse_host "$OTEL_COLLECTOR_HTTP_ENDPOINT")
OTEL_HTTP_PORT=$(parse_port "$OTEL_COLLECTOR_HTTP_ENDPOINT")
GRPC_API_HOST=$(parse_host "$GRPC_ENDPOINT")
GRPC_API_PORT=$(parse_port "$GRPC_ENDPOINT")

for _v in OTEL_COLLECTOR_GRPC_ENDPOINT GRPC_ENDPOINT CORS_ORIGIN_EXACT ALLOWED_HOSTS; do
  eval "[ -z \"\$$_v\" ]" && echo "WARNING: $_v is unset" >&2 || true
done

# CORS: subdomain regex fragment only in Development (when CORS_ORIGIN_SUBDOMAIN_REGEX is set)
if [ -n "$CORS_ORIGIN_SUBDOMAIN_REGEX" ]; then
  CORS_FRAGMENT_TMPL=/etc/envoy/cors-allow-origins-with-subdomain.tmpl
  case "$CORS_ORIGIN_SUBDOMAIN_REGEX" in
    *"*"*) CORS_ORIGIN_SUBDOMAIN_REGEX="^$(echo "$CORS_ORIGIN_SUBDOMAIN_REGEX" | sed 's/\./\\./g;s/\*/[a-zA-Z0-9.-]+/g')\$" ;;
  esac
else
  CORS_FRAGMENT_TMPL=/etc/envoy/cors-allow-origins-exact.tmpl
fi

sed \
  -e "s|__CORS_ORIGIN_EXACT__|${CORS_ORIGIN_EXACT}|g" \
  -e "s|__CORS_ORIGIN_SUBDOMAIN_REGEX__|${CORS_ORIGIN_SUBDOMAIN_REGEX}|g" \
  "$CORS_FRAGMENT_TMPL" > /tmp/cors-allow-origins.yaml

sed -e "/^__CORS_ALLOW_ORIGIN_MATCHES__$/r /tmp/cors-allow-origins.yaml" \
    -e "/^__CORS_ALLOW_ORIGIN_MATCHES__$/d" \
    /etc/envoy/envoy.yaml.tmpl > /tmp/envoy.yaml.tmpl

ALLOWED_HOSTS_DOMAINS=$(echo "$ALLOWED_HOSTS" | sed 's/^/["/' | sed 's/,/", "/g' | sed 's/$/"]/')

sed \
  -e "s|__OTEL_GRPC_HOST__|${OTEL_GRPC_HOST}|g" \
  -e "s|__OTEL_GRPC_PORT__|${OTEL_GRPC_PORT}|g" \
  -e "s|__OTEL_HTTP_HOST__|${OTEL_HTTP_HOST}|g" \
  -e "s|__OTEL_HTTP_PORT__|${OTEL_HTTP_PORT}|g" \
  -e "s|__OTEL_INSTANCE_ID__|${OTEL_INSTANCE_ID}|g" \
  -e "s|__GRPC_API_HOST__|${GRPC_API_HOST}|g" \
  -e "s|__GRPC_API_PORT__|${GRPC_API_PORT}|g" \
  -e "s|__ALLOWED_HOSTS__|$(echo "$ALLOWED_HOSTS_DOMAINS" | sed 's|"|\\"|g')|g" \
  /tmp/envoy.yaml.tmpl > /tmp/envoy.yaml

echo "entrypoint: otel=$OTEL_GRPC_HOST:$OTEL_GRPC_PORT grpc=$GRPC_API_HOST:$GRPC_API_PORT cors=$CORS_ORIGIN_EXACT hosts=$ALLOWED_HOSTS"
exec envoy -c /tmp/envoy.yaml "$@"
