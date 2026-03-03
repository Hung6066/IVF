import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpErrorResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ConsentBannerService } from '../services/consent-banner.service';

/**
 * Consent Enforcement Interceptor.
 *
 * Catches 403 responses with code CONSENT_REQUIRED from the backend
 * and notifies the ConsentBannerService to display a warning banner
 * with the specific missing consent types.
 */
export const consentInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  const consentBanner = inject(ConsentBannerService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 403 && error.error?.code === 'CONSENT_REQUIRED') {
        const missing: string[] = error.error.missingConsents ?? [];
        consentBanner.showMissingConsents(missing);
      }
      return throwError(() => error);
    }),
  );
};
