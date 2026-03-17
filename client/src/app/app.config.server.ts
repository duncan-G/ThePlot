import { mergeApplicationConfig, ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { TransferState } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import {
  OTEL_ENDPOINT,
  OTEL_ENDPOINT_KEY,
  SERVER_URL,
  SERVER_URL_KEY,
} from '../lib/server-url.token';

const serverUrl = process.env['SERVER_URL'] || '';
const otelEndpoint = (process.env['BROWSER_OTEL_ENDPOINT'] || '').replace(/\/$/, '');
const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    provideAppInitializer(() => {
      const state = inject(TransferState);
      state.set(SERVER_URL_KEY, serverUrl);
      state.set(OTEL_ENDPOINT_KEY, otelEndpoint);
    }),
    { provide: SERVER_URL, useValue: serverUrl },
    { provide: OTEL_ENDPOINT, useValue: otelEndpoint },
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
