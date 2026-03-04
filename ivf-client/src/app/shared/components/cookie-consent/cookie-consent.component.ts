import { Component, signal, effect, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { animate, style, transition, trigger } from '@angular/animations';

export interface CookieConsent {
  essential: boolean;
  security: boolean;
  analytics: boolean;
  consentedAt: string;
  version: string;
}

const CONSENT_KEY = 'ivf_cookie_consent';
const CONSENT_VERSION = '1.0';

@Component({
  selector: 'app-cookie-consent',
  standalone: true,
  imports: [CommonModule],
  animations: [
    trigger('slideUp', [
      transition(':enter', [
        style({ transform: 'translateY(100%)', opacity: 0 }),
        animate('300ms ease-out', style({ transform: 'translateY(0)', opacity: 1 })),
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ transform: 'translateY(100%)', opacity: 0 })),
      ]),
    ]),
  ],
  template: `
    @if (showBanner()) {
      <div class="cookie-overlay" @slideUp>
        <div class="cookie-banner">
          <div class="cookie-header">
            <h3>🍪 Cookie & Privacy Settings</h3>
          </div>

          @if (!showDetails()) {
            <div class="cookie-summary">
              <p>
                We use essential cookies to ensure the secure operation of the IVF Information
                System. No advertising or third-party tracking cookies are used.
                <a (click)="showDetails.set(true)" class="details-link">Manage preferences</a>
              </p>
              <div class="cookie-actions">
                <button class="btn btn-accept" (click)="acceptAll()">Accept All</button>
                <button class="btn btn-essential" (click)="acceptEssentialOnly()">
                  Essential Only
                </button>
              </div>
            </div>
          } @else {
            <div class="cookie-details">
              <div class="cookie-category">
                <div class="category-header">
                  <label class="toggle-label">
                    <input type="checkbox" [checked]="true" disabled />
                    <span class="toggle-text">Essential Cookies</span>
                    <span class="badge badge-required">Required</span>
                  </label>
                </div>
                <p class="category-desc">
                  Required for system operation: session management, authentication (JWT), CSRF
                  protection. These cannot be disabled.
                </p>
              </div>

              <div class="cookie-category">
                <div class="category-header">
                  <label class="toggle-label">
                    <input type="checkbox" [checked]="true" disabled />
                    <span class="toggle-text">Security Cookies</span>
                    <span class="badge badge-required">Required</span>
                  </label>
                </div>
                <p class="category-desc">
                  Used for threat detection, session validation, and protecting your account.
                  Required for HIPAA/GDPR compliance.
                </p>
              </div>

              <div class="cookie-category">
                <div class="category-header">
                  <label class="toggle-label">
                    <input
                      type="checkbox"
                      [checked]="analyticsEnabled()"
                      (change)="analyticsEnabled.set(!analyticsEnabled())"
                    />
                    <span class="toggle-text">Analytics Cookies</span>
                    <span class="badge badge-optional">Optional</span>
                  </label>
                </div>
                <p class="category-desc">
                  Help us understand system usage patterns to improve the user experience. No data
                  is shared with third parties.
                </p>
              </div>

              <div class="cookie-actions">
                <button class="btn btn-accept" (click)="savePreferences()">Save Preferences</button>
                <button class="btn btn-back" (click)="showDetails.set(false)">Back</button>
              </div>
            </div>
          }

          <div class="cookie-footer">
            <a href="/privacy-policy" target="_blank" class="footer-link">Privacy Policy</a>
            <span class="separator">|</span>
            <a href="/cookie-policy" target="_blank" class="footer-link">Cookie Policy</a>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .cookie-overlay {
        position: fixed;
        bottom: 0;
        left: 0;
        right: 0;
        z-index: 9999;
        padding: 0 16px 16px;
        pointer-events: none;
      }

      .cookie-banner {
        pointer-events: auto;
        max-width: 720px;
        margin: 0 auto;
        background: #ffffff;
        border-radius: 16px;
        box-shadow: 0 -4px 24px rgba(0, 0, 0, 0.15);
        padding: 24px;
        border: 1px solid #e5e7eb;
      }

      .cookie-header h3 {
        margin: 0 0 12px 0;
        font-size: 18px;
        font-weight: 600;
        color: #1f2937;
      }

      .cookie-summary p {
        margin: 0 0 16px 0;
        font-size: 14px;
        color: #4b5563;
        line-height: 1.5;
      }

      .details-link {
        color: #2563eb;
        cursor: pointer;
        text-decoration: underline;
        font-weight: 500;
      }

      .details-link:hover {
        color: #1d4ed8;
      }

      .cookie-actions {
        display: flex;
        gap: 12px;
        flex-wrap: wrap;
      }

      .btn {
        padding: 10px 24px;
        border: none;
        border-radius: 8px;
        font-size: 14px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.2s;
      }

      .btn-accept {
        background: #2563eb;
        color: white;
      }

      .btn-accept:hover {
        background: #1d4ed8;
      }

      .btn-essential {
        background: #f3f4f6;
        color: #374151;
        border: 1px solid #d1d5db;
      }

      .btn-essential:hover {
        background: #e5e7eb;
      }

      .btn-back {
        background: #f3f4f6;
        color: #374151;
        border: 1px solid #d1d5db;
      }

      .btn-back:hover {
        background: #e5e7eb;
      }

      .cookie-details {
        display: flex;
        flex-direction: column;
        gap: 16px;
        margin-bottom: 16px;
      }

      .cookie-category {
        padding: 12px 16px;
        background: #f9fafb;
        border-radius: 8px;
        border: 1px solid #e5e7eb;
      }

      .category-header {
        display: flex;
        align-items: center;
      }

      .toggle-label {
        display: flex;
        align-items: center;
        gap: 8px;
        cursor: pointer;
        font-size: 14px;
      }

      .toggle-text {
        font-weight: 600;
        color: #1f2937;
      }

      .badge {
        font-size: 11px;
        padding: 2px 8px;
        border-radius: 12px;
        font-weight: 600;
      }

      .badge-required {
        background: #dbeafe;
        color: #1d4ed8;
      }

      .badge-optional {
        background: #f3f4f6;
        color: #6b7280;
      }

      .category-desc {
        margin: 8px 0 0 26px;
        font-size: 13px;
        color: #6b7280;
        line-height: 1.4;
      }

      .cookie-footer {
        margin-top: 12px;
        padding-top: 12px;
        border-top: 1px solid #e5e7eb;
        text-align: center;
        font-size: 12px;
      }

      .footer-link {
        color: #6b7280;
        text-decoration: none;
      }

      .footer-link:hover {
        color: #2563eb;
        text-decoration: underline;
      }

      .separator {
        margin: 0 8px;
        color: #d1d5db;
      }

      input[type='checkbox'] {
        width: 18px;
        height: 18px;
        accent-color: #2563eb;
        cursor: pointer;
      }

      input[type='checkbox']:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
    `,
  ],
})
export class CookieConsentComponent implements OnInit {
  showBanner = signal(false);
  showDetails = signal(false);
  analyticsEnabled = signal(false);

  ngOnInit(): void {
    const stored = localStorage.getItem(CONSENT_KEY);
    if (!stored) {
      this.showBanner.set(true);
      return;
    }

    try {
      const consent: CookieConsent = JSON.parse(stored);
      if (consent.version !== CONSENT_VERSION) {
        this.showBanner.set(true);
      }
    } catch {
      this.showBanner.set(true);
    }
  }

  acceptAll(): void {
    this.saveConsent({ essential: true, security: true, analytics: true });
  }

  acceptEssentialOnly(): void {
    this.saveConsent({ essential: true, security: true, analytics: false });
  }

  savePreferences(): void {
    this.saveConsent({
      essential: true,
      security: true,
      analytics: this.analyticsEnabled(),
    });
  }

  private saveConsent(preferences: Omit<CookieConsent, 'consentedAt' | 'version'>): void {
    const consent: CookieConsent = {
      ...preferences,
      consentedAt: new Date().toISOString(),
      version: CONSENT_VERSION,
    };
    localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));
    this.showBanner.set(false);
  }
}
