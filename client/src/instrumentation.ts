/**
 * OpenTelemetry Node instrumentation - must load before any other modules.
 * Instruments HTTP, Express, and exports traces and logs to the OTel collector's HTTP endpoint.
 */
import { NodeSDK } from '@opentelemetry/sdk-node';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-http';
import { BatchLogRecordProcessor } from '@opentelemetry/sdk-logs';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';

const otelEndpoint = (process.env['NODE_OTLP_ENDPOINT'] || '').replace(/\/$/, '');
const isDev = process.env['NODE_ENV'] === 'development';
const batchConfig = isDev ? { scheduledDelayMillis: 1000 } : undefined;

if (otelEndpoint) {
  const traceExporter = new OTLPTraceExporter({
    url: `${otelEndpoint}/v1/traces`,
  });
  const logExporter = new OTLPLogExporter({
    url: `${otelEndpoint}/v1/logs`,
  });

  const sdk = new NodeSDK({
    resource: resourceFromAttributes({
      'service.name': 'theplot-ssr',
      'service.version': 'node',
    }),
    spanProcessors: [new BatchSpanProcessor(traceExporter, batchConfig)],
    logRecordProcessors: [
      new BatchLogRecordProcessor(logExporter, batchConfig),
    ],
    instrumentations: [
      getNodeAutoInstrumentations({
        '@opentelemetry/instrumentation-fs': { enabled: false },
        '@opentelemetry/instrumentation-net': { enabled: true },
      }),
    ],
  });

  sdk.start();
}
