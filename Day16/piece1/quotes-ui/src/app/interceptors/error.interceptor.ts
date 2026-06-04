import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
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

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Auto sign-out on expired/invalid token
      if (error.status === 401) {
        localStorage.removeItem('access_token');
        router.navigate(['/login'], {
          queryParams: { returnUrl: router.url },
        });
      }

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

      const appError: AppError = { status: error.status, friendlyMessage, raw };
      return throwError(() => appError);
    })
  );
};
