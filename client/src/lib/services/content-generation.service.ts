import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { context, trace, SpanStatusCode } from '@opentelemetry/api';
import { ContentGenerationServiceClient } from './content_generation/Content_generationServiceClientPb';
import {
  StartRunRequest,
  CompleteVoiceDeterminationRequest,
  RegenerateNodeRequest,
  GetRunStatusRequest,
  StreamRunStatusRequest,
  RunStatusUpdate,
  GenerationNodeStatusMessage,
  GetRunStatusResponse,
} from './content_generation/content_generation_pb';
import { SERVER_URL } from '../server-url.token';
import { createTraceUnaryInterceptor } from '../grpc-trace.interceptor';

export interface NodeStatusInfo {
  nodeId: string;
  kind: string;
  status: string;
  retryCount: number;
  lastError: string;
  elementIds: string[];
  sceneId: string;
}

export interface RunStatusUpdateEvent {
  runId: string;
  runStatus: string;
  phase: string;
  errorMessage: string;
  nodes: NodeStatusInfo[];
}

export type ElementGenStatus = 'idle' | 'pending' | 'running' | 'succeeded' | 'failed';

const tracer = trace.getTracer('theplot-content-generation');

@Injectable({ providedIn: 'root' })
export class ContentGenerationService {
  private readonly client = new ContentGenerationServiceClient(inject(SERVER_URL) + '/api', null, {
    unaryInterceptors: [createTraceUnaryInterceptor()],
  });

  async startRun(screenplayId: string): Promise<string> {
    const request = new StartRunRequest();
    request.setScreenplayId(screenplayId);
    const response = await this.client.startRun(request);
    return response.getRunId();
  }

  async completeVoiceDetermination(runId: string): Promise<void> {
    const request = new CompleteVoiceDeterminationRequest();
    request.setRunId(runId);
    await this.client.completeVoiceDetermination(request);
  }

  async regenerateNode(nodeId: string): Promise<void> {
    const request = new RegenerateNodeRequest();
    request.setNodeId(nodeId);
    await this.client.regenerateNode(request);
  }

  streamRunStatus(runId: string): Observable<RunStatusUpdateEvent> {
    return new Observable<RunStatusUpdateEvent>(subscriber => {
      const span = tracer.startSpan('stream-run-status', {
        attributes: { 'generation.run_id': runId },
      });
      const ctx = trace.setSpan(context.active(), span);

      const request = new StreamRunStatusRequest();
      request.setRunId(runId);

      const stream = context.with(ctx, () => this.client.streamRunStatus(request));

      stream.on('data', (response: RunStatusUpdate) => {
        subscriber.next({
          runId: response.getRunId(),
          runStatus: response.getRunStatus(),
          phase: response.getPhase(),
          errorMessage: response.getErrorMessage(),
          nodes: response.getNodesList().map(mapNodeStatus),
        });
      });

      stream.on('error', (err: Error) => {
        span.setStatus({ code: SpanStatusCode.ERROR, message: err.message });
        span.end();
        subscriber.error(err);
      });

      stream.on('end', () => {
        span.setStatus({ code: SpanStatusCode.OK });
        span.end();
        subscriber.complete();
      });

      return () => {
        stream.cancel();
        span.end();
      };
    });
  }
}

function mapNodeStatus(msg: GenerationNodeStatusMessage): NodeStatusInfo {
  return {
    nodeId: msg.getNodeId(),
    kind: msg.getKind(),
    status: msg.getStatus(),
    retryCount: msg.getRetryCount(),
    lastError: msg.getLastError(),
    elementIds: msg.getElementIdsList(),
    sceneId: msg.getSceneId(),
  };
}
