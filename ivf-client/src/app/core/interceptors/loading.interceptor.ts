import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';

const SKIP_URLS = ['/notifications/unread-count', '/hubs/'];

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const loading = inject(LoadingService);

  if (SKIP_URLS.some((url) => req.url.includes(url))) {
    return next(req);
  }

  loading.show();
  return next(req).pipe(finalize(() => loading.hide()));
};
