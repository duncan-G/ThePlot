import * as jspb from 'google-protobuf'



export class StartRunRequest extends jspb.Message {
  getScreenplayId(): string;
  setScreenplayId(value: string): StartRunRequest;

  getCancelActive(): boolean;
  setCancelActive(value: boolean): StartRunRequest;

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
    cancelActive: boolean,
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

export class GetLatestRunForScreenplayRequest extends jspb.Message {
  getScreenplayId(): string;
  setScreenplayId(value: string): GetLatestRunForScreenplayRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetLatestRunForScreenplayRequest.AsObject;
  static toObject(includeInstance: boolean, msg: GetLatestRunForScreenplayRequest): GetLatestRunForScreenplayRequest.AsObject;
  static serializeBinaryToWriter(message: GetLatestRunForScreenplayRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetLatestRunForScreenplayRequest;
  static deserializeBinaryFromReader(message: GetLatestRunForScreenplayRequest, reader: jspb.BinaryReader): GetLatestRunForScreenplayRequest;
}

export namespace GetLatestRunForScreenplayRequest {
  export type AsObject = {
    screenplayId: string,
  }
}

export class GetNodeAudioRequest extends jspb.Message {
  getNodeId(): string;
  setNodeId(value: string): GetNodeAudioRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetNodeAudioRequest.AsObject;
  static toObject(includeInstance: boolean, msg: GetNodeAudioRequest): GetNodeAudioRequest.AsObject;
  static serializeBinaryToWriter(message: GetNodeAudioRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetNodeAudioRequest;
  static deserializeBinaryFromReader(message: GetNodeAudioRequest, reader: jspb.BinaryReader): GetNodeAudioRequest;
}

export namespace GetNodeAudioRequest {
  export type AsObject = {
    nodeId: string,
  }
}

export class GetNodeAudioResponse extends jspb.Message {
  getAudioBase64(): string;
  setAudioBase64(value: string): GetNodeAudioResponse;

  getAudioFormat(): string;
  setAudioFormat(value: string): GetNodeAudioResponse;

  getMimeType(): string;
  setMimeType(value: string): GetNodeAudioResponse;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetNodeAudioResponse.AsObject;
  static toObject(includeInstance: boolean, msg: GetNodeAudioResponse): GetNodeAudioResponse.AsObject;
  static serializeBinaryToWriter(message: GetNodeAudioResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetNodeAudioResponse;
  static deserializeBinaryFromReader(message: GetNodeAudioResponse, reader: jspb.BinaryReader): GetNodeAudioResponse;
}

export namespace GetNodeAudioResponse {
  export type AsObject = {
    audioBase64: string,
    audioFormat: string,
    mimeType: string,
  }
}

export class RunSummary extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): RunSummary;

  getStatus(): string;
  setStatus(value: string): RunSummary;

  getPhase(): string;
  setPhase(value: string): RunSummary;

  getCreatedAt(): string;
  setCreatedAt(value: string): RunSummary;

  getTotalNodes(): number;
  setTotalNodes(value: number): RunSummary;

  getSucceededNodes(): number;
  setSucceededNodes(value: number): RunSummary;

  getFailedNodes(): number;
  setFailedNodes(value: number): RunSummary;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): RunSummary.AsObject;
  static toObject(includeInstance: boolean, msg: RunSummary): RunSummary.AsObject;
  static serializeBinaryToWriter(message: RunSummary, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): RunSummary;
  static deserializeBinaryFromReader(message: RunSummary, reader: jspb.BinaryReader): RunSummary;
}

export namespace RunSummary {
  export type AsObject = {
    runId: string,
    status: string,
    phase: string,
    createdAt: string,
    totalNodes: number,
    succeededNodes: number,
    failedNodes: number,
  }
}

export class ListRunsForScreenplayRequest extends jspb.Message {
  getScreenplayId(): string;
  setScreenplayId(value: string): ListRunsForScreenplayRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ListRunsForScreenplayRequest.AsObject;
  static toObject(includeInstance: boolean, msg: ListRunsForScreenplayRequest): ListRunsForScreenplayRequest.AsObject;
  static serializeBinaryToWriter(message: ListRunsForScreenplayRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ListRunsForScreenplayRequest;
  static deserializeBinaryFromReader(message: ListRunsForScreenplayRequest, reader: jspb.BinaryReader): ListRunsForScreenplayRequest;
}

export namespace ListRunsForScreenplayRequest {
  export type AsObject = {
    screenplayId: string,
  }
}

export class ListRunsForScreenplayResponse extends jspb.Message {
  getRunsList(): Array<RunSummary>;
  setRunsList(value: Array<RunSummary>): ListRunsForScreenplayResponse;
  clearRunsList(): ListRunsForScreenplayResponse;
  addRuns(value?: RunSummary, index?: number): RunSummary;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ListRunsForScreenplayResponse.AsObject;
  static toObject(includeInstance: boolean, msg: ListRunsForScreenplayResponse): ListRunsForScreenplayResponse.AsObject;
  static serializeBinaryToWriter(message: ListRunsForScreenplayResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ListRunsForScreenplayResponse;
  static deserializeBinaryFromReader(message: ListRunsForScreenplayResponse, reader: jspb.BinaryReader): ListRunsForScreenplayResponse;
}

export namespace ListRunsForScreenplayResponse {
  export type AsObject = {
    runsList: Array<RunSummary.AsObject>,
  }
}

export class NodeLineDetail extends jspb.Message {
  getElementId(): string;
  setElementId(value: string): NodeLineDetail;

  getCharacterName(): string;
  setCharacterName(value: string): NodeLineDetail;

  getType(): string;
  setType(value: string): NodeLineDetail;

  getText(): string;
  setText(value: string): NodeLineDetail;

  getVoiceName(): string;
  setVoiceName(value: string): NodeLineDetail;

  getVoiceDescription(): string;
  setVoiceDescription(value: string): NodeLineDetail;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): NodeLineDetail.AsObject;
  static toObject(includeInstance: boolean, msg: NodeLineDetail): NodeLineDetail.AsObject;
  static serializeBinaryToWriter(message: NodeLineDetail, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): NodeLineDetail;
  static deserializeBinaryFromReader(message: NodeLineDetail, reader: jspb.BinaryReader): NodeLineDetail;
}

export namespace NodeLineDetail {
  export type AsObject = {
    elementId: string,
    characterName: string,
    type: string,
    text: string,
    voiceName: string,
    voiceDescription: string,
  }
}

export class GenerationNodeDetail extends jspb.Message {
  getNodeId(): string;
  setNodeId(value: string): GenerationNodeDetail;

  getKind(): string;
  setKind(value: string): GenerationNodeDetail;

  getStatus(): string;
  setStatus(value: string): GenerationNodeDetail;

  getRetryCount(): number;
  setRetryCount(value: number): GenerationNodeDetail;

  getLastError(): string;
  setLastError(value: string): GenerationNodeDetail;

  getSceneId(): string;
  setSceneId(value: string): GenerationNodeDetail;

  getElementIdsList(): Array<string>;
  setElementIdsList(value: Array<string>): GenerationNodeDetail;
  clearElementIdsList(): GenerationNodeDetail;
  addElementIds(value: string, index?: number): GenerationNodeDetail;

  getVoiceName(): string;
  setVoiceName(value: string): GenerationNodeDetail;

  getVoiceDescription(): string;
  setVoiceDescription(value: string): GenerationNodeDetail;

  getCharacterName(): string;
  setCharacterName(value: string): GenerationNodeDetail;

  getText(): string;
  setText(value: string): GenerationNodeDetail;

  getLinesList(): Array<NodeLineDetail>;
  setLinesList(value: Array<NodeLineDetail>): GenerationNodeDetail;
  clearLinesList(): GenerationNodeDetail;
  addLines(value?: NodeLineDetail, index?: number): NodeLineDetail;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GenerationNodeDetail.AsObject;
  static toObject(includeInstance: boolean, msg: GenerationNodeDetail): GenerationNodeDetail.AsObject;
  static serializeBinaryToWriter(message: GenerationNodeDetail, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GenerationNodeDetail;
  static deserializeBinaryFromReader(message: GenerationNodeDetail, reader: jspb.BinaryReader): GenerationNodeDetail;
}

export namespace GenerationNodeDetail {
  export type AsObject = {
    nodeId: string,
    kind: string,
    status: string,
    retryCount: number,
    lastError: string,
    sceneId: string,
    elementIdsList: Array<string>,
    voiceName: string,
    voiceDescription: string,
    characterName: string,
    text: string,
    linesList: Array<NodeLineDetail.AsObject>,
  }
}

export class GetRunDetailsRequest extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): GetRunDetailsRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetRunDetailsRequest.AsObject;
  static toObject(includeInstance: boolean, msg: GetRunDetailsRequest): GetRunDetailsRequest.AsObject;
  static serializeBinaryToWriter(message: GetRunDetailsRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetRunDetailsRequest;
  static deserializeBinaryFromReader(message: GetRunDetailsRequest, reader: jspb.BinaryReader): GetRunDetailsRequest;
}

export namespace GetRunDetailsRequest {
  export type AsObject = {
    runId: string,
  }
}

export class GetRunDetailsResponse extends jspb.Message {
  getRunId(): string;
  setRunId(value: string): GetRunDetailsResponse;

  getScreenplayId(): string;
  setScreenplayId(value: string): GetRunDetailsResponse;

  getPhase(): string;
  setPhase(value: string): GetRunDetailsResponse;

  getStatus(): string;
  setStatus(value: string): GetRunDetailsResponse;

  getErrorMessage(): string;
  setErrorMessage(value: string): GetRunDetailsResponse;

  getCreatedAt(): string;
  setCreatedAt(value: string): GetRunDetailsResponse;

  getNodesList(): Array<GenerationNodeDetail>;
  setNodesList(value: Array<GenerationNodeDetail>): GetRunDetailsResponse;
  clearNodesList(): GetRunDetailsResponse;
  addNodes(value?: GenerationNodeDetail, index?: number): GenerationNodeDetail;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetRunDetailsResponse.AsObject;
  static toObject(includeInstance: boolean, msg: GetRunDetailsResponse): GetRunDetailsResponse.AsObject;
  static serializeBinaryToWriter(message: GetRunDetailsResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetRunDetailsResponse;
  static deserializeBinaryFromReader(message: GetRunDetailsResponse, reader: jspb.BinaryReader): GetRunDetailsResponse;
}

export namespace GetRunDetailsResponse {
  export type AsObject = {
    runId: string,
    screenplayId: string,
    phase: string,
    status: string,
    errorMessage: string,
    createdAt: string,
    nodesList: Array<GenerationNodeDetail.AsObject>,
  }
}

