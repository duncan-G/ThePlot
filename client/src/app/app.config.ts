import { ApplicationConfig, inject, PLATFORM_ID, provideAppInitializer, provideBrowserGlobalErrorListeners, TransferState } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import {
  OTEL_ENDPOINT,
  OTEL_ENDPOINT_KEY,
  SERVER_URL,
  SERVER_URL_KEY,
} from '../lib/server-url.token';
import { initBrowserTelemetry } from '../lib/telemetry.browser';

export const appConfig: ApplicationConfig = {
  providers: [
    provideAppInitializer(() => {
      const platformId = inject(PLATFORM_ID);
      if (!isPlatformBrowser(platformId)) return;
      initBrowserTelemetry(inject(OTEL_ENDPOINT));
    }),
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(withEventReplay()),
    {
      provide: SERVER_URL,
      useFactory: (state: TransferState) => state.get(SERVER_URL_KEY, ''),
      deps: [TransferState],
    },
    {
      provide: OTEL_ENDPOINT,
      useFactory: (state: TransferState) => state.get(OTEL_ENDPOINT_KEY, ''),
      deps: [TransferState],
    },
  ]
};
