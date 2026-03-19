FROM envoyproxy/envoy:v1.34-latest

# Copy config and entrypoint into image (used when Azure File volume is not populated)
COPY entrypoint.sh envoy.yaml.tmpl /etc/envoy/
RUN chmod +x /etc/envoy/entrypoint.sh
