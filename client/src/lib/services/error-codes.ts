// This file is auto-generated. Do not edit manually.
// To regenerate, run: node src/protoc-gen/generate_error_codes.js

export const ErrorCodes = {
    MissingParameter: "1000",
    InvalidParameter: "1001",
    InvalidLength: "1002",
    ResourceExhausted: "9998",
    Unexpected: "9999",
} as const

export type KnownErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes]