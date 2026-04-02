import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { context, trace, SpanStatusCode } from '@opentelemetry/api';
import { ContentGenerationServiceClient } from './content_generation/Content_generationServiceClientPb';
import {
  StartRunRequest,
  CompleteVoiceDeterminationRequest,
  ReplayRunRequest,
  RegenerateNodeRequest,
  GetRunStatusRequest,
  StreamRunStatusRequest,
  RunStatusUpdate,
  GenerationNodeStatusMessage,
  GetRunStatusResponse,
  GetLatestRunForScreenplayRequest,
  GetNodeAudioRequest,
  GetNodeAudioResponse,
  ListRunsForScreenplayRequest,
  ListRunsForScreenplayResponse,
  RunSummary,
  GetRunDetailsRequest,
  GetRunDetailsResponse,
  GenerationNodeDetail,
  NodeLineDetail,
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

export interface NodeAudioData {
  audioBase64: string;
  audioFormat: string;
  mimeType: string;
}

export interface RunSummaryInfo {
  runId: string;
  status: string;
  phase: string;
  createdAt: string;
  totalNodes: number;
  succeededNodes: number;
  failedNodes: number;
}

export interface NodeLineInfo {
  elementId: string;
  characterName: string;
  type: string;
  text: string;
  voiceName: string;
  voiceDescription: string;
}

export interface NodeDetailInfo {
  nodeId: string;
  kind: string;
  status: string;
  retryCount: number;
  lastError: string;
  sceneId: string;
  elementIds: string[];
  voiceName: string;
  voiceDescription: string;
  characterName: string;
  text: string;
  lines: NodeLineInfo[];
}

export interface RunDetailsInfo {
  runId: string;
  screenplayId: string;
  phase: string;
  status: string;
  errorMessage: string;
  createdAt: string;
  nodes: NodeDetailInfo[];
}

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

  async replayRun(runId: string): Promise<void> {
    const request = new ReplayRunRequest();
    request.setRunId(runId);
    await this.client.replayRun(request);
  }

  async regenerateNode(nodeId: string): Promise<void> {
    const request = new RegenerateNodeRequest();
    request.setNodeId(nodeId);
    await this.client.regenerateNode(request);
  }

  async getLatestRunForScreenplay(screenplayId: string): Promise<RunStatusUpdateEvent & { screenplayId: string } | null> {
    const request = new GetLatestRunForScreenplayRequest();
    request.setScreenplayId(screenplayId);
    try {
      const response = await this.client.getLatestRunForScreenplay(request);
      return {
        runId: response.getRunId(),
        runStatus: response.getStatus(),
        phase: response.getPhase(),
        errorMessage: response.getErrorMessage(),
        screenplayId: response.getScreenplayId(),
        nodes: response.getNodesList().map(mapNodeStatus),
      };
    } catch {
      return null;
    }
  }

  async getNodeAudio(nodeId: string): Promise<NodeAudioData> {
    const request = new GetNodeAudioRequest();
    request.setNodeId(nodeId);
    const response = await this.client.getNodeAudio(request);
    return {
      audioBase64: response.getAudioBase64(),
      audioFormat: response.getAudioFormat(),
      mimeType: response.getMimeType(),
    };
  }

  async listRunsForScreenplay(screenplayId: string): Promise<RunSummaryInfo[]> {
    const request = new ListRunsForScreenplayRequest();
    request.setScreenplayId(screenplayId);
    const response = await this.client.listRunsForScreenplay(request);
    return response.getRunsList().map(r => ({
      runId: r.getRunId(),
      status: r.getStatus(),
      phase: r.getPhase(),
      createdAt: r.getCreatedAt(),
      totalNodes: r.getTotalNodes(),
      succeededNodes: r.getSucceededNodes(),
      failedNodes: r.getFailedNodes(),
    }));
  }

  async getRunDetails(runId: string): Promise<RunDetailsInfo> {
    const request = new GetRunDetailsRequest();
    request.setRunId(runId);
    const response = await this.client.getRunDetails(request);
    return {
      runId: response.getRunId(),
      screenplayId: response.getScreenplayId(),
      phase: response.getPhase(),
      status: response.getStatus(),
      errorMessage: response.getErrorMessage(),
      createdAt: response.getCreatedAt(),
      nodes: response.getNodesList().map(n => ({
        nodeId: n.getNodeId(),
        kind: n.getKind(),
        status: n.getStatus(),
        retryCount: n.getRetryCount(),
        lastError: n.getLastError(),
        sceneId: n.getSceneId(),
        elementIds: n.getElementIdsList(),
        voiceName: n.getVoiceName(),
        voiceDescription: n.getVoiceDescription(),
        characterName: n.getCharacterName(),
        text: n.getText(),
        lines: n.getLinesList().map(l => ({
          elementId: l.getElementId(),
          characterName: l.getCharacterName(),
          type: l.getType(),
          text: l.getText(),
          voiceName: l.getVoiceName(),
          voiceDescription: l.getVoiceDescription(),
        })),
      })),
    };
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
