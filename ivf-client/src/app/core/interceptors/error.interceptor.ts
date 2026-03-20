import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { GlobalNotificationService } from '../services/global-notification.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(GlobalNotificationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Skip auth-handled errors (401 handled by authInterceptor)
      if (error.status === 401) {
        return throwError(() => error);
      }

      let message = 'Đã xảy ra lỗi không xác định';

      switch (error.status) {
        case 0:
          message = 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng.';
          break;
        case 400:
          message = error.error?.message || error.error?.title || 'Dữ liệu không hợp lệ';
          break;
        case 403:
          message = 'Bạn không có quyền thực hiện thao tác này';
          break;
        case 404:
          message = 'Không tìm thấy dữ liệu yêu cầu';
          break;
        case 409:
          message = error.error?.message || 'Dữ liệu bị xung đột';
          break;
        case 422:
          message = error.error?.message || 'Dữ liệu không thể xử lý';
          break;
        case 429:
          message = 'Quá nhiều yêu cầu. Vui lòng thử lại sau.';
          break;
        case 500:
          message = 'Lỗi máy chủ. Vui lòng thử lại sau.';
          break;
        case 502:
        case 503:
        case 504:
          message = 'Máy chủ tạm thời không khả dụng. Vui lòng thử lại sau.';
          break;
      }

      notification.error('Lỗi', message);
      return throwError(() => error);
    }),
  );
};
