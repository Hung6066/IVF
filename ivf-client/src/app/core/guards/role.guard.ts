import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard that checks if the current user has one of the required roles.
 * Usage in routes:
 *   { path: 'admin', canActivate: [roleGuard('Admin')], ... }
 *   { path: 'lab', canActivate: [roleGuard('Admin', 'LabTech', 'Embryologist')], ... }
 */
export function roleGuard(...roles: string[]): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    // Admin always passes
    if (authService.hasRole('Admin')) {
      return true;
    }

    if (roles.some((role) => authService.hasRole(role))) {
      return true;
    }

    router.navigate(['/dashboard'], {
      queryParams: { accessDenied: true },
    });
    return false;
  };
}
