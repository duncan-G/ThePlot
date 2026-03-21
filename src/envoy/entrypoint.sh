#!/bin/sh
set -e

require_env() {
  VAR_NAME="$1"
  eval "VAR_VALUE=\$${VAR_NAME}"
  if [ -z "$VAR_VALUE" ]; then
    echo "Error: Environment variable '$VAR_NAME' is not set." >&2
    exit 1
  fi
}


require_env ALLOWED_HOSTS
require_env CORS_ORIGIN_EXACT
require_env OTEL_INSTANCE_ID
require_env OTEL_HTTP_HOST
require_env OTEL_HTTP_PORT
require_env OTEL_GRPC_HOST
require_env OTEL_GRPC_PORT
require_env GRPC_API_HOST
require_env GRPC_API_PORT

if [ -n "$CORS_ORIGIN_SUBDOMAIN_REGEX" ]; then
  CORS_FRAGMENT_TMPL=/etc/envoy/cors-allow-origins-with-subdomain.tmpl
  # If the value contains '*', treat it as wildcard host syntax (e.g. *.example.com), not a raw
  # regex. sed: escape '.' so dots match literally; replace each '*' with '[a-zA-Z0-9.-]+' so one
  # '*' matches one or more subdomain label characters. Wrap in '^' and '$' so the whole Origin
  # header must match (Envoy safe_regex). Values without '*' pass through unchanged.
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

ALLOWED_HOSTS_ARRAY="[\"$(echo "$ALLOWED_HOSTS" | sed 's/,/","/g')\"]"

sed \
  -e "s|__ALLOWED_HOSTS__|${ALLOWED_HOSTS_ARRAY}|g" \
  -e "s|__OTEL_INSTANCE_ID__|${OTEL_INSTANCE_ID}|g" \
  -e "s|__OTEL_HTTP_HOST__|${OTEL_HTTP_HOST}|g" \
  -e "s|__OTEL_HTTP_PORT__|${OTEL_HTTP_PORT}|g" \
  -e "s|__OTEL_GRPC_HOST__|${OTEL_GRPC_HOST}|g" \
  -e "s|__OTEL_GRPC_PORT__|${OTEL_GRPC_PORT}|g" \
  -e "s|__GRPC_API_HOST__|${GRPC_API_HOST}|g" \
  -e "s|__GRPC_API_PORT__|${GRPC_API_PORT}|g" \
  /tmp/envoy.yaml.tmpl > /tmp/envoy.yaml

exec envoy -c /tmp/envoy.yaml "$@"