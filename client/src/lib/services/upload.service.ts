import { inject, Injectable } from '@angular/core';
import { context, trace, SpanStatusCode } from '@opentelemetry/api';
import { UploadClient } from './upload/UploadServiceClientPb';
import { UploadTokenRequest } from './upload/upload_pb';
import { SERVER_URL } from '../server-url.token';
import { createTraceUnaryInterceptor } from '../grpc-trace.interceptor';

export interface UploadResult {
  blobName: string;
}

const tracer = trace.getTracer('storybook-upload');

@Injectable({ providedIn: 'root' })
export class UploadService {
  private readonly client = new UploadClient(inject(SERVER_URL) + '/parser', null, {
    unaryInterceptors: [createTraceUnaryInterceptor()],
  });

  private static readonly PDF_MIME = 'application/pdf';
  private static readonly PDF_EXT = '.pdf';

  /**
   * Requests a short-lived SAS token from the API, then uploads the file
   * directly to Azure Blob Storage using the SAS URL.
   *
   * The entire flow runs under a single "pdf-upload" span. Each async
   * operation is wrapped in context.with() so the StackContextManager
   * (no Zone.js) correctly parents child spans created by auto-instrumentation.
   * Blob metadata carries the traceparent so the server-side validation
   * function can continue the same trace.
   */
  async uploadPdf(file: File): Promise<UploadResult> {
    const span = tracer.startSpan('pdf-upload', {
      attributes: { 'upload.filename': file.name },
    });
    const ctx = trace.setSpan(context.active(), span);

    try {
      const ext = file.name.toLowerCase().slice(-4);
      const type = file.type?.toLowerCase();

      if (ext !== UploadService.PDF_EXT) {
        throw new Error(`File must be a PDF. Received: ${file.name}`);
      }
      if (type && type !== UploadService.PDF_MIME) {
        throw new Error(`File must be a PDF (application/pdf). Received type: ${file.type}`);
      }

      const request = new UploadTokenRequest();
      request.setFilename(file.name);

      const response = await context.with(ctx, () =>
        this.client.requestUploadToken(request),
      );
      const uploadUrl = response.getUploadUrl();
      const blobName = response.getBlobName();

      // Build traceparent from the span reference directly (context.active()
      // is unreliable after await with StackContextManager).
      const sc = span.spanContext();
      const traceparent =
        `00-${sc.traceId}-${sc.spanId}-${sc.traceFlags.toString(16).padStart(2, '0')}`;

      const putRes = await context.with(ctx, () =>
        fetch(uploadUrl, {
          method: 'PUT',
          body: file,
          headers: {
            'Content-Type': 'application/pdf',
            'x-ms-blob-type': 'BlockBlob',
            'x-ms-meta-traceparent': traceparent,
          },
        }),
      );

      if (!putRes.ok) {
        const text = await putRes.text();
        throw new Error(text || `Upload failed: ${putRes.status}`);
      }

      span.setAttribute('upload.blob_name', blobName);
      span.setStatus({ code: SpanStatusCode.OK });
      return { blobName };
    } catch (err) {
      span.setStatus({
        code: SpanStatusCode.ERROR,
        message: err instanceof Error ? err.message : String(err),
      });
      throw err;
    } finally {
      span.end();
    }
  }
}
