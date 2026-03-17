import { InjectionToken, makeStateKey } from '@angular/core';

export const SERVER_URL = new InjectionToken<string>('SERVER_URL');
export const SERVER_URL_KEY = makeStateKey<string>('serverUrl');

export const OTEL_ENDPOINT = new InjectionToken<string>('OTEL_ENDPOINT');
export const OTEL_ENDPOINT_KEY = makeStateKey<string>('otelEndpoint');
