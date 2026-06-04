import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withViewTransitions } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './interceptors/auth.interceptor';
import { retryInterceptor } from './interceptors/retry.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes, withViewTransitions()),
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        errorInterceptor,
        retryInterceptor,
      ])
    ),
  ],
};
