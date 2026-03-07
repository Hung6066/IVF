import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpErrorResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError, Observable, BehaviorSubject, filter, take } from 'rxjs';
import { AuthService } from '../services/auth.service';

let isRefreshing = false;
const refreshSubject = new BehaviorSubject<boolean>(false);

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  // Skip auth for login/refresh/mfa/passkey endpoints
  if (
    req.url.includes('/auth/login') ||
    req.url.includes('/auth/refresh') ||
    req.url.includes('/auth/mfa-') ||
    req.url.includes('/auth/passkey-login')
  ) {
    return next(req);
  }

  // Add token if available
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && token) {
        if (isRefreshing) {
          // Another refresh is already in progress — wait for it to complete, then retry
          return refreshSubject.pipe(
            filter((done) => done),
            take(1),
            switchMap(() => {
              const newToken = authService.getToken();
              const newReq = req.clone({
                setHeaders: { Authorization: `Bearer ${newToken}` },
              });
              return next(newReq);
            }),
          );
        }

        isRefreshing = true;
        refreshSubject.next(false);

        return authService.refreshToken().pipe(
          switchMap(() => {
            isRefreshing = false;
            refreshSubject.next(true);
            const newToken = authService.getToken();
            const newReq = req.clone({
              setHeaders: { Authorization: `Bearer ${newToken}` },
            });
            return next(newReq);
          }),
          catchError((refreshError) => {
            isRefreshing = false;
            refreshSubject.next(true);
            authService.logout();
            return throwError(() => refreshError);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
