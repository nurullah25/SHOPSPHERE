import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SKIP_ERROR_TOAST } from '../http/http-context';
import { getErrorMessage } from '../http/error-message';
import { NotificationService } from '../services/notification.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401s are handled by the auth interceptor (refresh or redirect to login)
      if (error.status !== 401 && !req.context.get(SKIP_ERROR_TOAST)) {
        notifications.error(getErrorMessage(error));
      }
      return throwError(() => error);
    })
  );
};
