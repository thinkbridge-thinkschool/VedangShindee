import { HttpBackend, HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AppError, ProblemDetails } from '../models/app-error.model';

const FRIENDLY_MESSAGES: Record<number, string> = {
  400: 'Please check your input and try again.',
  401: 'Please log in to continue.',
  403: 'You do not have permission to do this.',
  404: 'The requested item was not found.',
  500: 'Server error. Please try again later.',
};

function isProblemDetails(body: unknown): body is ProblemDetails {
  return typeof body === 'object' && body !== null && 'title' in body;
}

function toAppError(error: HttpErrorResponse): AppError {
  let friendlyMessage: string;
  let raw: ProblemDetails | undefined;

  if (error.status === 0) {
    friendlyMessage = 'Cannot connect. Check your connection.';
  } else if (isProblemDetails(error.error)) {
    raw = error.error;
    friendlyMessage = raw.detail ?? raw.title;
  } else {
    friendlyMessage = FRIENDLY_MESSAGES[error.status] ?? `Unexpected error (${error.status}).`;
  }

  return { status: error.status, friendlyMessage, raw };
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const handler = inject(HttpBackend);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // On 401 from a non-auth endpoint, try to silently refresh and retry.
      if (error.status === 401 && !req.url.includes('/api/auth/')) {
        const refreshToken = localStorage.getItem('refresh_token');
        if (refreshToken) {
          // Use HttpBackend directly to bypass interceptors (avoids infinite loop).
          const bare = new HttpClient(handler);
          return bare
            .post<{ access_token: string; refresh_token: string; expires_in: number }>(
              '/api/auth/refresh',
              { refresh_token: refreshToken }
            )
            .pipe(
              switchMap(tokens => {
                localStorage.setItem('access_token', tokens.access_token);
                localStorage.setItem('refresh_token', tokens.refresh_token);
                return next(
                  req.clone({ setHeaders: { Authorization: `Bearer ${tokens.access_token}` } })
                );
              }),
              catchError(() => {
                // Refresh failed — clear stale tokens and surface 401 to the UI.
                localStorage.removeItem('access_token');
                localStorage.removeItem('refresh_token');
                return throwError(() => toAppError(error));
              })
            );
        }
      }
      return throwError(() => toAppError(error));
    })
  );
};
