import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { context, trace, SpanStatusCode } from '@opentelemetry/api';
import { ScreenplayServiceClient } from './screenplay/ScreenplayServiceClientPb';
import {
  StreamImportStatusRequest,
  ImportStatusEvent,
  GetScreenplayRequest,
  GetScreenplayResponse,
  ListScreenplaysRequest,
  ListScreenplaysResponse,
  ScreenplaySummary as ScreenplaySummaryPb,
  SceneMessage,
  SceneElementMessage,
} from './screenplay/screenplay_pb';
import { SERVER_URL } from '../server-url.token';
import { createTraceUnaryInterceptor } from '../grpc-trace.interceptor';

export interface ImportEvent {
  kind: string;
  screenplayId: string;
  errorMessage: string;
  startPage: number;
  endPage: number;
  totalPages: number;
}

export interface ScreenplayScene {
  id: string;
  heading: string;
  locationType: string;
  location: string;
  timeOfDay: string;
  page: number;
  characters: string[];
  elements: ScreenplayElement[];
}

export interface ScreenplayElement {
  id: string;
  type: string;
  text: string;
  character: string;
  page: number;
  sequenceOrder: number;
}

export interface ScreenplayData {
  id: string;
  title: string;
  authors: string[];
  scenes: ScreenplayScene[];
  totalPages: number;
}

export interface ScreenplaySummary {
  id: string;
  title: string;
  authors: string[];
  totalPages: number;
  dateCreated: string;
}

export interface ScreenplayListPage {
  items: ScreenplaySummary[];
  nextPageToken: string;
}

const tracer = trace.getTracer('theplot-screenplay');

@Injectable({ providedIn: 'root' })
export class ScreenplayService {
  private readonly client = new ScreenplayServiceClient(inject(SERVER_URL) + '/api', null, {
    unaryInterceptors: [createTraceUnaryInterceptor()],
  });

  streamImportStatus(blobName: string): Observable<ImportEvent> {
    return new Observable<ImportEvent>(subscriber => {
      const span = tracer.startSpan('stream-import-status', {
        attributes: { 'import.blob_name': blobName },
      });
      const ctx = trace.setSpan(context.active(), span);

      const request = new StreamImportStatusRequest();
      request.setBlobName(blobName);

      const stream = context.with(ctx, () =>
        this.client.streamImportStatus(request),
      );

      stream.on('data', (response: ImportStatusEvent) => {
        subscriber.next({
          kind: response.getKind(),
          screenplayId: response.getScreenplayId(),
          errorMessage: response.getErrorMessage(),
          startPage: response.getStartPage(),
          endPage: response.getEndPage(),
          totalPages: response.getTotalPages(),
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

  async listScreenplays(pageSize = 20, pageToken = ''): Promise<ScreenplayListPage> {
    const request = new ListScreenplaysRequest();
    request.setPageSize(pageSize);
    if (pageToken) {
      request.setPageToken(pageToken);
    }

    const response = await this.client.listScreenplays(request);
    return {
      items: response.getItemsList().map(mapSummary),
      nextPageToken: response.getNextPageToken(),
    };
  }

  async getScreenplay(screenplayId: string): Promise<ScreenplayData> {
    const request = new GetScreenplayRequest();
    request.setScreenplayId(screenplayId);

    const response = await this.client.getScreenplay(request);
    return mapResponse(response);
  }
}

function mapSummary(s: ScreenplaySummaryPb): ScreenplaySummary {
  return {
    id: s.getId(),
    title: s.getTitle(),
    authors: s.getAuthorsList(),
    totalPages: s.getTotalPages(),
    dateCreated: s.getDateCreated(),
  };
}

function mapResponse(res: GetScreenplayResponse): ScreenplayData {
  return {
    id: res.getId(),
    title: res.getTitle(),
    authors: res.getAuthorsList(),
    scenes: res.getScenesList().map(mapScene),
    totalPages: res.getTotalPages(),
  };
}

function mapScene(scene: SceneMessage): ScreenplayScene {
  return {
    id: scene.getId(),
    heading: scene.getHeading(),
    locationType: scene.getLocationType(),
    location: scene.getLocation(),
    timeOfDay: scene.getTimeOfDay(),
    page: scene.getPage(),
    characters: scene.getCharactersList(),
    elements: scene.getElementsList().map(mapElement),
  };
}

function mapElement(el: SceneElementMessage): ScreenplayElement {
  return {
    id: el.getId(),
    type: el.getType(),
    text: el.getText(),
    character: el.getCharacter(),
    page: el.getPage(),
    sequenceOrder: el.getSequenceOrder(),
  };
}
