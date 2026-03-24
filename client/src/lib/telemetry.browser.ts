import {
  BatchSpanProcessor,
  StackContextManager,
  WebTracerProvider,
} from '@opentelemetry/sdk-trace-web';
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from '@opentelemetry/sdk-logs';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-http';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { logs } from '@opentelemetry/api-logs';
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';
import { XMLHttpRequestInstrumentation } from '@opentelemetry/instrumentation-xml-http-request';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { W3CTraceContextPropagator } from '@opentelemetry/core';

/**
 * Initialize OpenTelemetry for the browser.
 * Exports traces and logs to Envoy's OTLP endpoint (/otlp/v1).
 * Includes page load and fetch instrumentation.
 */
export function initBrowserTelemetry(otelEndpoint: string): void {
  if (!otelEndpoint) {
    return;
  }

  const resource = resourceFromAttributes({
    'service.name': 'theplot-client',
    'service.version': typeof navigator !== 'undefined' ? 'browser' : 'unknown',
  });

  const traceExporter = new OTLPTraceExporter({
    url: `${otelEndpoint}/traces`,
  });

  const isDev =
    typeof import.meta !== 'undefined' && (import.meta as { env?: { DEV?: boolean } }).env?.DEV === true;
  const batchConfig = isDev ? { scheduledDelayMillis: 1000 } : undefined;

  const traceProvider = new WebTracerProvider({
    resource,
    spanProcessors: [new BatchSpanProcessor(traceExporter, batchConfig)],
  });

  traceProvider.register({
    contextManager: new StackContextManager(),
    propagator: new W3CTraceContextPropagator(),
  });

  const logExporter = new OTLPLogExporter({
    url: `${otelEndpoint}/logs`,
  });

  const loggerProvider = new LoggerProvider({
    resource,
    processors: [new BatchLogRecordProcessor(logExporter, batchConfig)],
  });

  logs.setGlobalLoggerProvider(loggerProvider);

  const ignoreUrls = [/\/otlp\/v1\//];
  const propagateToAll = [/^https?:\/\/.*/];

  registerInstrumentations({
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        propagateTraceHeaderCorsUrls: propagateToAll,
        ignoreUrls,
        clearTimingResources: true,
      }),
      new XMLHttpRequestInstrumentation({
        propagateTraceHeaderCorsUrls: propagateToAll,
        ignoreUrls,
        clearTimingResources: true,
      }),
    ],
  });
}
