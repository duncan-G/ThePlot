import * as jspb from 'google-protobuf'



export class StartRunRequest extends jspb.Message {
  getScreenplayId(): string;
  setScreenplayId(value: string): StartRunRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): StartRunRequest.AsObject;
  static toObject(includeInstance: boolean, msg: StartRunRequest): StartRunRequest.AsObject;
  static serializeBinaryToWriter(message: StartRunRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): StartRunRequest;
  static deserializeBinaryFromReader(message: StartRunRequest, reader: jspb.BinaryReader): StartRunRequest;
}

export namespace StartRunRequest {
  export type AsObject = {
    screenplayId: string,
  }
}

export class StartRunResponse extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): StartRunResponse;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): StartRunResponse.AsObject;
  static toObject(includeInstance: boolean, msg: StartRunResponse): StartRunResponse.AsObject;
  static serializeBinaryToWriter(message: StartRunResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): StartRunResponse;
  static deserializeBinaryFromReader(message: StartRunResponse, reader: jspb.BinaryReader): StartRunResponse;
}

export namespace StartRunResponse {
  export type AsObject = {
    runId: string,
  }
}

export class CompleteVoiceDeterminationRequest extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): CompleteVoiceDeterminationRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): CompleteVoiceDeterminationRequest.AsObject;
  static toObject(includeInstance: boolean, msg: CompleteVoiceDeterminationRequest): CompleteVoiceDeterminationRequest.AsObject;
  static serializeBinaryToWriter(message: CompleteVoiceDeterminationRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): CompleteVoiceDeterminationRequest;
  static deserializeBinaryFromReader(message: CompleteVoiceDeterminationRequest, reader: jspb.BinaryReader): CompleteVoiceDeterminationRequest;
}

export namespace CompleteVoiceDeterminationRequest {
  export type AsObject = {
    runId: string,
  }
}

export class CompleteVoiceDeterminationResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): CompleteVoiceDeterminationResponse.AsObject;
  static toObject(includeInstance: boolean, msg: CompleteVoiceDeterminationResponse): CompleteVoiceDeterminationResponse.AsObject;
  static serializeBinaryToWriter(message: CompleteVoiceDeterminationResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): CompleteVoiceDeterminationResponse;
  static deserializeBinaryFromReader(message: CompleteVoiceDeterminationResponse, reader: jspb.BinaryReader): CompleteVoiceDeterminationResponse;
}

export namespace CompleteVoiceDeterminationResponse {
  export type AsObject = {
  }
}

export class ReplayRunRequest extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): ReplayRunRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ReplayRunRequest.AsObject;
  static toObject(includeInstance: boolean, msg: ReplayRunRequest): ReplayRunRequest.AsObject;
  static serializeBinaryToWriter(message: ReplayRunRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ReplayRunRequest;
  static deserializeBinaryFromReader(message: ReplayRunRequest, reader: jspb.BinaryReader): ReplayRunRequest;
}

export namespace ReplayRunRequest {
  export type AsObject = {
    runId: string,
  }
}

export class ReplayRunResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ReplayRunResponse.AsObject;
  static toObject(includeInstance: boolean, msg: ReplayRunResponse): ReplayRunResponse.AsObject;
  static serializeBinaryToWriter(message: ReplayRunResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ReplayRunResponse;
  static deserializeBinaryFromReader(message: ReplayRunResponse, reader: jspb.BinaryReader): ReplayRunResponse;
}

export namespace ReplayRunResponse {
  export type AsObject = {
  }
}

export class RegenerateNodeRequest extends jspb.Message {
  getNodeId(): string;
  setNodeId(value: string): RegenerateNodeRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): RegenerateNodeRequest.AsObject;
  static toObject(includeInstance: boolean, msg: RegenerateNodeRequest): RegenerateNodeRequest.AsObject;
  static serializeBinaryToWriter(message: RegenerateNodeRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): RegenerateNodeRequest;
  static deserializeBinaryFromReader(message: RegenerateNodeRequest, reader: jspb.BinaryReader): RegenerateNodeRequest;
}

export namespace RegenerateNodeRequest {
  export type AsObject = {
    nodeId: string,
  }
}

export class RegenerateNodeResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): RegenerateNodeResponse.AsObject;
  static toObject(includeInstance: boolean, msg: RegenerateNodeResponse): RegenerateNodeResponse.AsObject;
  static serializeBinaryToWriter(message: RegenerateNodeResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): RegenerateNodeResponse;
  static deserializeBinaryFromReader(message: RegenerateNodeResponse, reader: jspb.BinaryReader): RegenerateNodeResponse;
}

export namespace RegenerateNodeResponse {
  export type AsObject = {
  }
}

export class GetRunStatusRequest extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): GetRunStatusRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetRunStatusRequest.AsObject;
  static toObject(includeInstance: boolean, msg: GetRunStatusRequest): GetRunStatusRequest.AsObject;
  static serializeBinaryToWriter(message: GetRunStatusRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetRunStatusRequest;
  static deserializeBinaryFromReader(message: GetRunStatusRequest, reader: jspb.BinaryReader): GetRunStatusRequest;
}

export namespace GetRunStatusRequest {
  export type AsObject = {
    runId: string,
  }
}

export class GenerationNodeStatusMessage extends jspb.Message {
  getNodeId(): string;
  setNodeId(value: string): GenerationNodeStatusMessage;

  getKind(): string;
  setKind(value: string): GenerationNodeStatusMessage;

  getStatus(): string;
  setStatus(value: string): GenerationNodeStatusMessage;

  getRetryCount(): number;
  setRetryCount(value: number): GenerationNodeStatusMessage;

  getLastError(): string;
  setLastError(value: string): GenerationNodeStatusMessage;

  getElementIdsList(): Array<string>;
  setElementIdsList(value: Array<string>): GenerationNodeStatusMessage;
  clearElementIdsList(): GenerationNodeStatusMessage;
  addElementIds(value: string, index?: number): GenerationNodeStatusMessage;

  getSceneId(): string;
  setSceneId(value: string): GenerationNodeStatusMessage;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GenerationNodeStatusMessage.AsObject;
  static toObject(includeInstance: boolean, msg: GenerationNodeStatusMessage): GenerationNodeStatusMessage.AsObject;
  static serializeBinaryToWriter(message: GenerationNodeStatusMessage, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GenerationNodeStatusMessage;
  static deserializeBinaryFromReader(message: GenerationNodeStatusMessage, reader: jspb.BinaryReader): GenerationNodeStatusMessage;
}

export namespace GenerationNodeStatusMessage {
  export type AsObject = {
    nodeId: string,
    kind: string,
    status: string,
    retryCount: number,
    lastError: string,
    elementIdsList: Array<string>,
    sceneId: string,
  }
}

export class StreamRunStatusRequest extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): StreamRunStatusRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): StreamRunStatusRequest.AsObject;
  static toObject(includeInstance: boolean, msg: StreamRunStatusRequest): StreamRunStatusRequest.AsObject;
  static serializeBinaryToWriter(message: StreamRunStatusRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): StreamRunStatusRequest;
  static deserializeBinaryFromReader(message: StreamRunStatusRequest, reader: jspb.BinaryReader): StreamRunStatusRequest;
}

export namespace StreamRunStatusRequest {
  export type AsObject = {
    runId: string,
  }
}

export class RunStatusUpdate extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): RunStatusUpdate;

  getRunStatus(): string;
  setRunStatus(value: string): RunStatusUpdate;

  getPhase(): string;
  setPhase(value: string): RunStatusUpdate;

  getErrorMessage(): string;
  setErrorMessage(value: string): RunStatusUpdate;

  getNodesList(): Array<GenerationNodeStatusMessage>;
  setNodesList(value: Array<GenerationNodeStatusMessage>): RunStatusUpdate;
  clearNodesList(): RunStatusUpdate;
  addNodes(value?: GenerationNodeStatusMessage, index?: number): GenerationNodeStatusMessage;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): RunStatusUpdate.AsObject;
  static toObject(includeInstance: boolean, msg: RunStatusUpdate): RunStatusUpdate.AsObject;
  static serializeBinaryToWriter(message: RunStatusUpdate, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): RunStatusUpdate;
  static deserializeBinaryFromReader(message: RunStatusUpdate, reader: jspb.BinaryReader): RunStatusUpdate;
}

export namespace RunStatusUpdate {
  export type AsObject = {
    runId: string,
    runStatus: string,
    phase: string,
    errorMessage: string,
    nodesList: Array<GenerationNodeStatusMessage.AsObject>,
  }
}

export class GetRunStatusResponse extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): GetRunStatusResponse;

  getScreenplayId(): string;
  setScreenplayId(value: string): GetRunStatusResponse;

  getPhase(): string;
  setPhase(value: string): GetRunStatusResponse;

  getStatus(): string;
  setStatus(value: string): GetRunStatusResponse;

  getErrorMessage(): string;
  setErrorMessage(value: string): GetRunStatusResponse;

  getNodesList(): Array<GenerationNodeStatusMessage>;
  setNodesList(value: Array<GenerationNodeStatusMessage>): GetRunStatusResponse;
  clearNodesList(): GetRunStatusResponse;
  addNodes(value?: GenerationNodeStatusMessage, index?: number): GenerationNodeStatusMessage;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetRunStatusResponse.AsObject;
  static toObject(includeInstance: boolean, msg: GetRunStatusResponse): GetRunStatusResponse.AsObject;
  static serializeBinaryToWriter(message: GetRunStatusResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetRunStatusResponse;
  static deserializeBinaryFromReader(message: GetRunStatusResponse, reader: jspb.BinaryReader): GetRunStatusResponse;
}

export namespace GetRunStatusResponse {
  export type AsObject = {
    runId: string,
    screenplayId: string,
    phase: string,
    status: string,
    errorMessage: string,
    nodesList: Array<GenerationNodeStatusMessage.AsObject>,
  }
}

