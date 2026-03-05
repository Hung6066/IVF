import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TenantFeatureService } from '../services/tenant-feature.service';

/**
 * Route guard that checks if a required feature is enabled for the current tenant.
 * Usage in routes:
 *   { path: 'billing', canActivate: [featureGuard('billing')], ... }
 */
export function featureGuard(featureCode: string): CanActivateFn {
  return async () => {
    const featureService = inject(TenantFeatureService);
    const router = inject(Router);

    // Ensure features are loaded
    await featureService.load();

    if (featureService.isFeatureEnabled(featureCode)) {
      return true;
    }

    // Redirect to dashboard with query param so UI can show a message
    router.navigate(['/dashboard'], {
      queryParams: { featureBlocked: featureCode },
    });
    return false;
  };
}
