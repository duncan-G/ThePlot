import * as jspb from 'google-protobuf'



export class UploadTokenRequest extends jspb.Message {
  getFilename(): string;
  setFilename(value: string): UploadTokenRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): UploadTokenRequest.AsObject;
  static toObject(includeInstance: boolean, msg: UploadTokenRequest): UploadTokenRequest.AsObject;
  static serializeBinaryToWriter(message: UploadTokenRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): UploadTokenRequest;
  static deserializeBinaryFromReader(message: UploadTokenRequest, reader: jspb.BinaryReader): UploadTokenRequest;
}

export namespace UploadTokenRequest {
  export type AsObject = {
    filename: string,
  }
}

export class UploadTokenResponse extends jspb.Message {
  getUploadUrl(): string;
  setUploadUrl(value: string): UploadTokenResponse;

  getBlobName(): string;
  setBlobName(value: string): UploadTokenResponse;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): UploadTokenResponse.AsObject;
  static toObject(includeInstance: boolean, msg: UploadTokenResponse): UploadTokenResponse.AsObject;
  static serializeBinaryToWriter(message: UploadTokenResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): UploadTokenResponse;
  static deserializeBinaryFromReader(message: UploadTokenResponse, reader: jspb.BinaryReader): UploadTokenResponse;
}

export namespace UploadTokenResponse {
  export type AsObject = {
    uploadUrl: string,
    blobName: string,
  }
}

