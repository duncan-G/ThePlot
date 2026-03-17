import * as jspb from 'google-protobuf'



export class StreamImportStatusRequest extends jspb.Message {
  getBlobName(): string;
  setBlobName(value: string): StreamImportStatusRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): StreamImportStatusRequest.AsObject;
  static toObject(includeInstance: boolean, msg: StreamImportStatusRequest): StreamImportStatusRequest.AsObject;
  static serializeBinaryToWriter(message: StreamImportStatusRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): StreamImportStatusRequest;
  static deserializeBinaryFromReader(message: StreamImportStatusRequest, reader: jspb.BinaryReader): StreamImportStatusRequest;
}

export namespace StreamImportStatusRequest {
  export type AsObject = {
    blobName: string,
  }
}

export class ImportStatusEvent extends jspb.Message {
  getKind(): string;
  setKind(value: string): ImportStatusEvent;

  getScreenplayId(): string;
  setScreenplayId(value: string): ImportStatusEvent;

  getErrorMessage(): string;
  setErrorMessage(value: string): ImportStatusEvent;

  getStartPage(): number;
  setStartPage(value: number): ImportStatusEvent;

  getEndPage(): number;
  setEndPage(value: number): ImportStatusEvent;

  getTotalPages(): number;
  setTotalPages(value: number): ImportStatusEvent;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ImportStatusEvent.AsObject;
  static toObject(includeInstance: boolean, msg: ImportStatusEvent): ImportStatusEvent.AsObject;
  static serializeBinaryToWriter(message: ImportStatusEvent, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ImportStatusEvent;
  static deserializeBinaryFromReader(message: ImportStatusEvent, reader: jspb.BinaryReader): ImportStatusEvent;
}

export namespace ImportStatusEvent {
  export type AsObject = {
    kind: string,
    screenplayId: string,
    errorMessage: string,
    startPage: number,
    endPage: number,
    totalPages: number,
  }
}

export class GetScreenplayRequest extends jspb.Message {
  getScreenplayId(): string;
  setScreenplayId(value: string): GetScreenplayRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetScreenplayRequest.AsObject;
  static toObject(includeInstance: boolean, msg: GetScreenplayRequest): GetScreenplayRequest.AsObject;
  static serializeBinaryToWriter(message: GetScreenplayRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetScreenplayRequest;
  static deserializeBinaryFromReader(message: GetScreenplayRequest, reader: jspb.BinaryReader): GetScreenplayRequest;
}

export namespace GetScreenplayRequest {
  export type AsObject = {
    screenplayId: string,
  }
}

export class GetScreenplayResponse extends jspb.Message {
  getId(): string;
  setId(value: string): GetScreenplayResponse;

  getTitle(): string;
  setTitle(value: string): GetScreenplayResponse;

  getAuthorsList(): Array<string>;
  setAuthorsList(value: Array<string>): GetScreenplayResponse;
  clearAuthorsList(): GetScreenplayResponse;
  addAuthors(value: string, index?: number): GetScreenplayResponse;

  getScenesList(): Array<SceneMessage>;
  setScenesList(value: Array<SceneMessage>): GetScreenplayResponse;
  clearScenesList(): GetScreenplayResponse;
  addScenes(value?: SceneMessage, index?: number): SceneMessage;

  getTotalPages(): number;
  setTotalPages(value: number): GetScreenplayResponse;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GetScreenplayResponse.AsObject;
  static toObject(includeInstance: boolean, msg: GetScreenplayResponse): GetScreenplayResponse.AsObject;
  static serializeBinaryToWriter(message: GetScreenplayResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GetScreenplayResponse;
  static deserializeBinaryFromReader(message: GetScreenplayResponse, reader: jspb.BinaryReader): GetScreenplayResponse;
}

export namespace GetScreenplayResponse {
  export type AsObject = {
    id: string,
    title: string,
    authorsList: Array<string>,
    scenesList: Array<SceneMessage.AsObject>,
    totalPages: number,
  }
}

export class SceneMessage extends jspb.Message {
  getId(): string;
  setId(value: string): SceneMessage;

  getHeading(): string;
  setHeading(value: string): SceneMessage;

  getLocationType(): string;
  setLocationType(value: string): SceneMessage;

  getLocation(): string;
  setLocation(value: string): SceneMessage;

  getTimeOfDay(): string;
  setTimeOfDay(value: string): SceneMessage;

  getPage(): number;
  setPage(value: number): SceneMessage;

  getCharactersList(): Array<string>;
  setCharactersList(value: Array<string>): SceneMessage;
  clearCharactersList(): SceneMessage;
  addCharacters(value: string, index?: number): SceneMessage;

  getElementsList(): Array<SceneElementMessage>;
  setElementsList(value: Array<SceneElementMessage>): SceneMessage;
  clearElementsList(): SceneMessage;
  addElements(value?: SceneElementMessage, index?: number): SceneElementMessage;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): SceneMessage.AsObject;
  static toObject(includeInstance: boolean, msg: SceneMessage): SceneMessage.AsObject;
  static serializeBinaryToWriter(message: SceneMessage, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): SceneMessage;
  static deserializeBinaryFromReader(message: SceneMessage, reader: jspb.BinaryReader): SceneMessage;
}

export namespace SceneMessage {
  export type AsObject = {
    id: string,
    heading: string,
    locationType: string,
    location: string,
    timeOfDay: string,
    page: number,
    charactersList: Array<string>,
    elementsList: Array<SceneElementMessage.AsObject>,
  }
}

export class SceneElementMessage extends jspb.Message {
  getId(): string;
  setId(value: string): SceneElementMessage;

  getType(): string;
  setType(value: string): SceneElementMessage;

  getText(): string;
  setText(value: string): SceneElementMessage;

  getCharacter(): string;
  setCharacter(value: string): SceneElementMessage;

  getPage(): number;
  setPage(value: number): SceneElementMessage;

  getSequenceOrder(): number;
  setSequenceOrder(value: number): SceneElementMessage;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): SceneElementMessage.AsObject;
  static toObject(includeInstance: boolean, msg: SceneElementMessage): SceneElementMessage.AsObject;
  static serializeBinaryToWriter(message: SceneElementMessage, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): SceneElementMessage;
  static deserializeBinaryFromReader(message: SceneElementMessage, reader: jspb.BinaryReader): SceneElementMessage;
}

export namespace SceneElementMessage {
  export type AsObject = {
    id: string,
    type: string,
    text: string,
    character: string,
    page: number,
    sequenceOrder: number,
  }
}

