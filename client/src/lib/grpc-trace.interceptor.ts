import type * as grpcWeb from 'grpc-web';
import { context, trace, SpanKind, SpanStatusCode } from '@opentelemetry/api';

const tracer = trace.getTracer('grpc-web');

/**
 * Creates a named client span for each gRPC-Web unary call, using the full
 * RPC method path from the MethodDescriptor (e.g. "upload.Upload/RequestUploadToken").
 *
 * Trace context propagation to the server is handled automatically by the
 * XMLHttpRequestInstrumentation + W3CTraceContextPropagator registered in
 * telemetry.browser.ts — no manual traceparent header injection needed here.
 */
class TraceUnaryInterceptor<TReq, TRes> implements grpcWeb.UnaryInterceptor<TReq, TRes> {
    intercept(
        request: grpcWeb.Request<TReq, TRes>,
        invoker: (
            request: grpcWeb.Request<TReq, TRes>,
            metadata: grpcWeb.Metadata
        ) => Promise<grpcWeb.UnaryResponse<TReq, TRes>>
    ): Promise<grpcWeb.UnaryResponse<TReq, TRes>> {
        const fullMethod = request.getMethodDescriptor().getName();
        const method = fullMethod.replace(/^\//, '');
        const [service = '', rpc = ''] = method.split('/');

        const span = tracer.startSpan(`gRPC ${method}`, {
            kind: SpanKind.CLIENT,
            attributes: {
                'rpc.system': 'grpc-web',
                'rpc.service': service,
                'rpc.method': rpc,
            },
        });

        return context.with(trace.setSpan(context.active(), span), () => {
            const original = request.getMetadata() || {};
            const metadata: grpcWeb.Metadata = {};
            for (const [key, value] of Object.entries(original)) {
                metadata[key] = String(value);
            }

            return invoker(request, metadata)
                .then(response => {
                    span.setStatus({ code: SpanStatusCode.OK });
                    span.end();
                    return response;
                })
                .catch(err => {
                    span.setStatus({ code: SpanStatusCode.ERROR, message: String(err) });
                    span.end();
                    throw err;
                });
        });
    }
}

export function createTraceUnaryInterceptor(): grpcWeb.UnaryInterceptor<unknown, unknown> {
  return new TraceUnaryInterceptor();
}
