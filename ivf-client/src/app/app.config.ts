import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { securityInterceptor } from './core/interceptors/security.interceptor';
import { consentInterceptor } from './core/interceptors/consent.interceptor';
import { tenantLimitInterceptor } from './core/interceptors/tenant-limit.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([
        loadingInterceptor,
        securityInterceptor,
        authInterceptor,
        consentInterceptor,
        tenantLimitInterceptor,
        errorInterceptor,
      ]),
    ),
    provideAnimations(),
  ],
};
