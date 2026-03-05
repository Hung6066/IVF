import { Injectable, inject, signal, computed } from '@angular/core';
import { TenantService } from './tenant.service';
import { TenantFeatures } from '../models/tenant.model';
import { AuthService } from './auth.service';

/**
 * Centralized service for tenant feature state.
 * Loads features once and shares them across guards, interceptors, and components.
 */
@Injectable({ providedIn: 'root' })
export class TenantFeatureService {
  private tenantService = inject(TenantService);
  private authService = inject(AuthService);

  private _features = signal<TenantFeatures | null>(null);
  private _loaded = signal(false);

  readonly features = this._features.asReadonly();
  readonly loaded = this._loaded.asReadonly();

  readonly enabledFeatures = computed(() => this._features()?.enabledFeatures ?? []);
  readonly isPlatformAdmin = computed(() => this._features()?.isPlatformAdmin ?? false);
  readonly maxUsers = computed(() => this._features()?.maxUsers ?? 0);
  readonly maxPatients = computed(() => this._features()?.maxPatients ?? 0);

  /**
   * Load features from API. Safe to call multiple times — will only fetch once per session.
   */
  load(): Promise<void> {
    if (this._loaded() || !this.authService.isAuthenticated()) {
      return Promise.resolve();
    }

    return new Promise<void>((resolve) => {
      this.tenantService.getMyFeatures().subscribe({
        next: (features) => {
          this._features.set(features);
          this._loaded.set(true);
          resolve();
        },
        error: () => {
          this._features.set(null);
          this._loaded.set(true);
          resolve();
        },
      });
    });
  }

  /** Force reload features (e.g. after plan upgrade) */
  reload(): Promise<void> {
    this._loaded.set(false);
    return this.load();
  }

  /** Check if a specific feature is enabled */
  isFeatureEnabled(featureCode: string): boolean {
    if (this.isPlatformAdmin()) return true;
    return this.enabledFeatures().includes(featureCode);
  }

  /** Clear cached state on logout */
  clear(): void {
    this._features.set(null);
    this._loaded.set(false);
  }
}
