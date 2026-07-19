import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from './notification.service';

/** Set on requests whose errors are handled locally (e.g. the live pattern test). */
export const SILENT_ERRORS = new HttpContextToken<boolean>(() => false);

export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (!req.context.get(SILENT_ERRORS)) {
        const detail =
          typeof err.error === 'object' && err.error !== null && 'error' in err.error
            ? String((err.error as { error: unknown }).error)
            : err.message;
        notifications.error(detail);
      }
      return throwError(() => err);
    }),
  );
};
