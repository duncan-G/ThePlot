# Envoy Proxy for ThePlot

This directory contains an Envoy configuration that routes HTTP requests from the browser client to the ThePlot Parser service.

## Architecture

```
┌─────────────┐     HTTP/HTTPS    ┌─────────────┐     HTTP/2     ┌─────────────┐
│   Client    │ ─────────────────►  │    Envoy    │ ─────────────►  │   Parser   │
│  (browser)  │   :11000/parser/*  │  (container)│   host:port    │  (host)    │
└─────────────┘                   └─────────────┘                 └─────────────┘
```

## Routes

| Path | Backend | Description |
|------|---------|-------------|
| `/parser/*` | Parser | REST API (e.g. POST /parser/api/upload for PDF upload) |

Envoy rewrites `/parser/` to `/` and forwards to the parser. The client sends requests to `http://localhost:11000/parser/api/upload` for PDF uploads.

## Client Integration

The Aspire AppHost wires the client with `WithReference(envoyProxy)`. The client receives the Envoy base URL via service discovery. Use that URL + `/parser` as the base for API calls.

**Example** (uploading a PDF):

```typescript
const formData = new FormData();
formData.set('file', pdfFile);
const res = await fetch(`${parserBaseUrl}/api/upload`, { method: 'POST', body: formData });
```

## Configuration

- **envoy.yaml** – Single-file config (no TLS, no auth) for development
- Parser cluster targets `host.aspire.internal:5192` (Aspire's container-to-host hostname)
- OTLP access logs, traces, and stats sent to Aspire dashboard at `host.aspire.internal:4317`
- **Use the `http` launch profile** when running Aspire so the OTLP endpoint binds to `0.0.0.0` and is reachable from the Envoy container (HTTPS uses a dev cert that containers cannot trust)
- CORS enabled for all origins (dev only)
