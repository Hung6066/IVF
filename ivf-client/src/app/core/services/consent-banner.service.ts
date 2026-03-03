import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

/** Maps consent type keys to Vietnamese display labels */
const CONSENT_LABELS: Record<string, string> = {
  data_processing: 'Xử lý dữ liệu',
  medical_records: 'Hồ sơ y tế',
  marketing: 'Tiếp thị',
  analytics: 'Phân tích',
  research: 'Nghiên cứu',
  third_party: 'Chia sẻ bên thứ 3',
  biometric_data: 'Dữ liệu sinh trắc',
  cookies: 'Cookie',
};

/**
 * Maps frontend routes to required consent types.
 * Mirrors backend ConsentEnforcementMiddleware.PathConsentMap.
 */
const ROUTE_CONSENT_MAP: Record<string, string[]> = {
  '/patients': ['data_processing', 'medical_records'],
  '/reception': ['data_processing', 'medical_records'],
  '/couples': ['data_processing', 'medical_records'],
  '/consultation': ['data_processing', 'medical_records'],
  '/ultrasound': ['data_processing', 'medical_records'],
  '/lab': ['data_processing', 'medical_records'],
  '/andrology': ['data_processing', 'medical_records'],
  '/injection': ['data_processing', 'medical_records'],
  '/sperm-bank': ['data_processing', 'biometric_data'],
  '/reports': ['data_processing', 'analytics'],
  '/forms': ['data_processing'],
};

@Injectable({ providedIn: 'root' })
export class ConsentBannerService {
  private readonly http = inject(HttpClient);

  /** Current list of missing consent type keys */
  readonly missingConsents = signal<string[]>([]);

  /** Set of valid consent types the current user has */
  readonly validConsents = signal<Set<string>>(new Set());

  /** Whether consent status has been loaded */
  readonly loaded = signal(false);

  /** Whether the banner should be visible */
  readonly visible = computed(() => this.missingConsents().length > 0 && !this._dismissed());

  /** User temporarily dismissed the banner */
  private readonly _dismissed = signal(false);

  /** Get display label for a consent type key */
  getLabel(type: string): string {
    return CONSENT_LABELS[type] ?? type;
  }

  /** Load current user's consent status from backend */
  loadConsentStatus(): void {
    this.http
      .get<{
        validConsents: string[];
        missingConsents: string[];
      }>(`${environment.apiUrl}/user-consents/my-status`)
      .subscribe({
        next: (res) => {
          this.validConsents.set(new Set(res.validConsents));
          this.missingConsents.set(res.missingConsents);
          this.loaded.set(true);
        },
        error: () => {
          this.loaded.set(true);
        },
      });
  }

  /**
   * Check if a menu route requires consent the user hasn't granted.
   * Returns the list of missing consent types for this route, or empty if OK.
   */
  getMissingForRoute(route: string): string[] {
    if (!this.loaded()) return [];
    const required = this.getRequiredConsents(route);
    if (required.length === 0) return [];
    const valid = this.validConsents();
    return required.filter((t) => !valid.has(t));
  }

  /** Whether a menu item's route is blocked by missing consent */
  isRouteBlocked(route: string): boolean {
    return this.getMissingForRoute(route).length > 0;
  }

  /** Called by the consent interceptor when a 403 CONSENT_REQUIRED is returned */
  showMissingConsents(types: string[]): void {
    this._dismissed.set(false);
    const current = new Set(this.missingConsents());
    types.forEach((t) => current.add(t));
    this.missingConsents.set([...current]);
    // Also update validConsents
    const valid = new Set(this.validConsents());
    types.forEach((t) => valid.delete(t));
    this.validConsents.set(valid);
  }

  /** User dismissed the banner (will reappear on next 403) */
  dismiss(): void {
    this._dismissed.set(true);
  }

  /** Clear all state (on logout) */
  clear(): void {
    this.missingConsents.set([]);
    this.validConsents.set(new Set());
    this.loaded.set(false);
    this._dismissed.set(false);
  }

  private getRequiredConsents(route: string): string[] {
    for (const [prefix, types] of Object.entries(ROUTE_CONSENT_MAP)) {
      if (route.startsWith(prefix)) return types;
    }
    return [];
  }
}
