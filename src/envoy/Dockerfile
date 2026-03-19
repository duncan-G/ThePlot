# Generate self-signed cert for production (when Aspire dev cert is unavailable)
FROM debian:bookworm-slim AS certgen
RUN apt-get update && apt-get install -y --no-install-recommends openssl && rm -rf /var/lib/apt/lists/*
RUN openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /tls/key.pem -out /tls/cert.pem \
  -subj "/CN=envoy-proxy" -addext "subjectAltName=DNS:*,DNS:localhost,IP:127.0.0.1"

FROM envoyproxy/envoy:v1.34-latest
COPY --from=certgen /tls /etc/envoy/tls

# Copy config and entrypoint into image (used when Azure File volume is not populated)
COPY entrypoint.sh envoy.yaml.tmpl /etc/envoy/
RUN chmod +x /etc/envoy/entrypoint.sh
