import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

/**
 * Tenant Limit & Feature Gate Interceptor.
 *
 * Catches 403 responses with codes TENANT_LIMIT_EXCEEDED, FEATURE_NOT_ENABLED,
 * and TENANT_SUSPENDED, displaying user-friendly messages.
 */
export const tenantLimitInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 403 && error.error?.code) {
        const code = error.error.code;

        if (code === 'TENANT_SUSPENDED') {
          localStorage.removeItem('token');
          localStorage.removeItem('refreshToken');
          showAlert(
            error.error.error ||
              'Trung tâm của bạn đã bị tạm ngưng hoạt động. Vui lòng liên hệ quản trị viên.',
          );
          router.navigate(['/login']);
        } else if (code === 'TENANT_LIMIT_EXCEEDED') {
          const msg = buildLimitMessage(
            error.error.limitType,
            error.error.currentCount,
            error.error.maxAllowed,
          );
          showAlert(msg);
        } else if (code === 'FEATURE_NOT_ENABLED') {
          showAlert(
            `Tính năng "${error.error.featureCode}" chưa được kích hoạt.\nVui lòng liên hệ quản trị viên hoặc nâng cấp gói dịch vụ.`,
          );
        }
      }
      return throwError(() => error);
    }),
  );
};

function buildLimitMessage(limitType: string, current: number, max: number): string {
  const labels: Record<string, string> = {
    MaxUsers: 'số lượng người dùng',
    MaxPatientsPerMonth: 'số lượng bệnh nhân/tháng',
    StorageLimitMb: 'dung lượng lưu trữ (MB)',
  };
  const label = labels[limitType] ?? limitType;
  return `Đã vượt giới hạn ${label}: ${current}/${max}.\nVui lòng liên hệ quản trị viên hoặc nâng cấp gói dịch vụ.`;
}

function showAlert(message: string) {
  // Uses native alert for simplicity; can be replaced with a toast service
  setTimeout(() => alert(message), 0);
}
