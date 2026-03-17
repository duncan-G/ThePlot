# Envoy gRPC-Web Proxy for ThePlot

This directory contains a minimal Envoy configuration that routes HTTP/gRPC-Web requests from the browser client to the gRPC ThePlot Parser service.

## Why gRPC-Web?

Browsers cannot speak native gRPC (which uses HTTP/2). gRPC-Web is an alternative that uses HTTP/1.1 and is supported by browsers. Envoy acts as a transcoder:

- **Client** (browser) → HTTP POST with gRPC-Web encoding → **Envoy** (port 11000)
- **Envoy** → transcodes to gRPC (HTTP/2) → **Parser** (port 5192)

## Architecture

```
┌─────────────┐     gRPC-Web      ┌─────────────┐     gRPC       ┌─────────────┐
│   Client    │ ───────────────►  │    Envoy    │ ─────────────►  │   Parser   │
│  (browser)  │   :11000/parser/*  │  (container)│   host:5192     │  (host)    │
└─────────────┘                   └─────────────┘                 └─────────────┘
```

## Routes

| Path | Backend | Description |
|------|---------|-------------|
| `/parser/*` | Parser gRPC | Greeter and other gRPC services |

The client sends requests to `http://localhost:11000/parser/greet.Greeter/SayHello`. Envoy rewrites to `/greet.Greeter/SayHello` and forwards to the parser.

## Client Integration

The Aspire AppHost wires the client with `WithReference(envoyProxy)`. The client receives the Envoy base URL via service discovery (e.g. `envoy-grpc-web__http`). Use that URL + `/parser` as the base for gRPC-Web calls.

**Example** (Angular with `@grpc/grpc-web`):

```typescript
// Base URL from Aspire: http://localhost:11000
// gRPC-Web service path: /parser
const client = new GreeterClient({ host: 'http://localhost:11000', transportOptions: { pathPrefix: '/parser' } });
```

## Configuration

- **envoy.yaml** – Single-file config (no TLS, no auth) for development
- Parser cluster targets `host.aspire.internal:5192` (Aspire's container-to-host hostname)
- CORS enabled for all origins (dev only)
