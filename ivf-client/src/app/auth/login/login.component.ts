import { Component, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SsoService } from '../../core/services/sso.service';
import { SsoProvider } from '../../core/models/waf.model';
import { coerceToArrayBuffer, coerceToBase64Url } from '../../core/utils/webauthn.utils';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <div class="login-header">
          <div class="logo">🏥</div>
          <h1>IVF Information System</h1>
          <p>Hệ thống quản lý thụ tinh trong ống nghiệm</p>
        </div>

        <!-- Step 1: Credentials -->
        @if (step() === 'credentials') {
          <form (ngSubmit)="onSubmit()" class="login-form">
            @if (error()) {
              <div
                class="error-message"
                [class.error-locked]="
                  errorCode() === 'ACCOUNT_LOCKED' || errorCode() === 'BRUTE_FORCE_LOCKED'
                "
              >
                <span class="error-icon">{{ getErrorIcon() }}</span>
                <div>
                  <div>{{ error() }}</div>
                  @if (lockUnlocksAt()) {
                    <div class="error-detail">Mở khóa lúc: {{ lockUnlocksAt() }}</div>
                  }
                </div>
              </div>
            }

            <div class="form-group">
              <label for="username">Tên đăng nhập</label>
              <input
                type="text"
                id="username"
                [(ngModel)]="username"
                name="username"
                placeholder="Nhập tên đăng nhập"
                required
                [disabled]="loading()"
              />
            </div>

            <div class="form-group">
              <label for="password">Mật khẩu</label>
              <input
                type="password"
                id="password"
                [(ngModel)]="password"
                name="password"
                placeholder="Nhập mật khẩu"
                required
                [disabled]="loading()"
              />
            </div>

            <button type="submit" class="btn-login" [disabled]="loading()">
              @if (loading()) {
                <span class="spinner"></span> Đang đăng nhập...
              } @else {
                Đăng nhập
              }
            </button>

            <div class="divider"><span>hoặc</span></div>

            <button
              type="button"
              class="btn-passkey"
              (click)="onPasskeyLogin()"
              [disabled]="loading()"
            >
              🔑 Đăng nhập bằng Passkey
            </button>

            @if (ssoProviders().length > 0) {
              <div class="divider"><span>SSO</span></div>
              @for (provider of ssoProviders(); track provider.id) {
                <button
                  type="button"
                  class="btn-sso"
                  (click)="onSsoLogin(provider)"
                  [disabled]="loading()"
                >
                  @if (provider.iconUrl) {
                    <img [src]="provider.iconUrl" [alt]="provider.displayName" class="sso-icon" />
                  } @else {
                    <span class="sso-icon-fallback">🌐</span>
                  }
                  {{ provider.displayName }}
                </button>
              }
            }
          </form>
        }

        <!-- Step 2: MFA Verification -->
        @if (step() === 'mfa') {
          <div class="login-form">
            @if (error()) {
              <div class="error-message">
                <span class="error-icon">⚠️</span>
                <div>{{ error() }}</div>
              </div>
            }

            <div class="mfa-info">
              <div class="mfa-icon">🔐</div>
              <h3>Xác thực hai yếu tố</h3>
              @if (mfaMethod() === 'totp') {
                <p>Nhập mã từ ứng dụng xác thực (Google Authenticator, Authy...)</p>
              } @else {
                <p>Nhập mã OTP đã gửi đến số điện thoại của bạn</p>
              }
            </div>

            <div class="form-group">
              <label for="mfaCode">Mã xác thực</label>
              <input
                type="text"
                id="mfaCode"
                [(ngModel)]="mfaCode"
                name="mfaCode"
                placeholder="Nhập mã 6 số"
                maxlength="6"
                pattern="[0-9]*"
                inputmode="numeric"
                autocomplete="one-time-code"
                [disabled]="loading()"
                (keyup.enter)="onMfaVerify()"
              />
            </div>

            @if (mfaMethod() === 'sms') {
              <button
                type="button"
                class="btn-resend"
                (click)="onResendSms()"
                [disabled]="loading()"
              >
                📱 Gửi lại mã OTP
              </button>
            }

            <button
              type="button"
              class="btn-login"
              (click)="onMfaVerify()"
              [disabled]="loading() || !mfaCode"
            >
              @if (loading()) {
                <span class="spinner"></span> Đang xác thực...
              } @else {
                Xác nhận
              }
            </button>

            <button type="button" class="btn-back" (click)="onBackToLogin()">
              ← Quay lại đăng nhập
            </button>
          </div>
        }

        <div class="login-footer">
          <p>© 2026 IVF System - Version 1.0</p>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .login-container {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        padding: 1rem;
        position: relative;
        overflow: hidden;
      }

      /* Abstract Background Shapes */
      .login-container::before {
        content: '';
        position: absolute;
        width: 300px;
        height: 300px;
        background: rgba(255, 255, 255, 0.1);
        border-radius: 50%;
        top: -50px;
        left: -50px;
        z-index: 0;
      }

      .login-container::after {
        content: '';
        position: absolute;
        width: 250px;
        height: 250px;
        background: rgba(255, 255, 255, 0.15);
        border-radius: 50%;
        bottom: -50px;
        right: -50px;
        z-index: 0;
      }

      .login-card {
        background: rgba(255, 255, 255, 0.95);
        backdrop-filter: blur(20px);
        -webkit-backdrop-filter: blur(20px);
        border-radius: 24px;
        box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
        padding: 3rem;
        width: 100%;
        max-width: 420px;
        position: relative;
        z-index: 1;
        border: 1px solid rgba(255, 255, 255, 0.5);
        animation: slideUp 0.5s ease-out;
      }

      @keyframes slideUp {
        from {
          transform: translateY(20px);
          opacity: 0;
        }
        to {
          transform: translateY(0);
          opacity: 1;
        }
      }

      .login-header {
        text-align: center;
        margin-bottom: 2.5rem;
      }

      .logo {
        font-size: 3.5rem;
        margin-bottom: 0.5rem;
        filter: drop-shadow(0 4px 6px rgba(0, 0, 0, 0.1));
      }

      .login-header h1 {
        font-size: 1.5rem;
        font-weight: 700;
        color: #1a1a2e;
        margin: 0 0 0.5rem;
        letter-spacing: -0.5px;
      }

      .login-header p {
        color: #6b7280;
        font-size: 0.875rem;
        margin: 0;
      }

      .login-form {
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
      }

      .form-group {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }

      .form-group label {
        font-size: 0.875rem;
        font-weight: 500;
        color: #374151;
        margin-left: 0.25rem;
      }

      .form-group input {
        padding: 0.875rem 1rem;
        border: 1px solid #e5e7eb;
        border-radius: 12px;
        font-size: 1rem;
        transition: all 0.2s;
        background: rgba(255, 255, 255, 0.8);
      }

      .form-group input:focus {
        outline: none;
        border-color: #667eea;
        box-shadow: 0 0 0 4px rgba(102, 126, 234, 0.15);
        background: white;
      }

      .btn-login {
        padding: 1rem;
        margin-top: 1rem;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
        border: none;
        border-radius: 12px;
        font-size: 1rem;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.3s;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        box-shadow: 0 4px 6px -1px rgba(102, 126, 234, 0.4);
      }

      .btn-login:hover:not(:disabled) {
        transform: translateY(-2px);
        box-shadow: 0 10px 15px -3px rgba(102, 126, 234, 0.5);
      }

      .btn-login:disabled {
        opacity: 0.7;
        cursor: not-allowed;
      }

      .spinner {
        width: 20px;
        height: 20px;
        border: 2px solid rgba(255, 255, 255, 0.3);
        border-top-color: white;
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
      }

      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }

      .error-message {
        background: #fef2f2;
        border: 1px solid #fecaca;
        color: #dc2626;
        padding: 0.75rem 1rem;
        border-radius: 12px;
        font-size: 0.875rem;
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
      }

      .error-locked {
        background: #fff7ed;
        border-color: #fed7aa;
        color: #c2410c;
      }

      .error-icon {
        font-size: 1.1rem;
        flex-shrink: 0;
        margin-top: 1px;
      }
      .error-detail {
        font-size: 0.75rem;
        margin-top: 0.25rem;
        opacity: 0.8;
      }

      .divider {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        color: #9ca3af;
        font-size: 0.8rem;
      }
      .divider::before,
      .divider::after {
        content: '';
        flex: 1;
        height: 1px;
        background: #e5e7eb;
      }

      .btn-passkey {
        padding: 0.875rem;
        background: white;
        color: #374151;
        border: 1px solid #d1d5db;
        border-radius: 12px;
        font-size: 0.95rem;
        font-weight: 500;
        cursor: pointer;
        transition: all 0.2s;
      }
      .btn-passkey:hover:not(:disabled) {
        background: #f9fafb;
        border-color: #667eea;
        color: #667eea;
      }
      .btn-passkey:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }

      .btn-sso {
        padding: 0.75rem;
        background: white;
        color: #374151;
        border: 1px solid #d1d5db;
        border-radius: 12px;
        font-size: 0.9rem;
        font-weight: 500;
        cursor: pointer;
        transition: all 0.2s;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
      }
      .btn-sso:hover:not(:disabled) {
        background: #f9fafb;
        border-color: #667eea;
        color: #667eea;
      }
      .btn-sso:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      .sso-icon {
        width: 20px;
        height: 20px;
        object-fit: contain;
      }
      .sso-icon-fallback {
        font-size: 1.1rem;
      }

      .mfa-info {
        text-align: center;
        margin-bottom: 0.5rem;
      }
      .mfa-icon {
        font-size: 2.5rem;
        margin-bottom: 0.5rem;
      }
      .mfa-info h3 {
        font-size: 1.1rem;
        color: #1a1a2e;
        margin: 0 0 0.5rem;
      }
      .mfa-info p {
        font-size: 0.85rem;
        color: #6b7280;
        margin: 0;
      }

      .btn-resend {
        padding: 0.5rem;
        background: none;
        border: none;
        color: #667eea;
        font-size: 0.85rem;
        cursor: pointer;
        text-align: center;
      }
      .btn-resend:hover {
        text-decoration: underline;
      }

      .btn-back {
        padding: 0.5rem;
        background: none;
        border: none;
        color: #6b7280;
        font-size: 0.85rem;
        cursor: pointer;
        text-align: center;
      }
      .btn-back:hover {
        color: #374151;
      }

      .login-footer {
        margin-top: 2.5rem;
        text-align: center;
      }

      .login-footer p {
        color: #9ca3af;
        font-size: 0.75rem;
        margin: 0;
      }
    `,
  ],
})
export class LoginComponent implements OnInit {
  username = '';
  password = '';
  mfaCode = '';
  loading = signal(false);
  error = signal<string | null>(null);
  errorCode = signal<string | null>(null);
  step = signal<'credentials' | 'mfa'>('credentials');
  mfaToken = signal<string | null>(null);
  mfaMethod = signal<string>('totp');
  lockUnlocksAt = signal<string | null>(null);
  ssoProviders = signal<SsoProvider[]>([]);

  constructor(
    private authService: AuthService,
    private ssoService: SsoService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadSsoProviders();
    this.handleSsoCallback();
  }

  private loadSsoProviders(): void {
    this.ssoService.getProviders().subscribe({
      next: (providers) => this.ssoProviders.set(providers),
      error: () => {}, // SSO not configured — no providers shown
    });
  }

  async onSsoLogin(provider: SsoProvider): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const codeVerifier = await this.ssoService.generateCodeVerifier();
      const codeChallenge = await this.ssoService.generateCodeChallenge(codeVerifier);
      const state = this.ssoService.generateState();

      // Store PKCE state for callback
      sessionStorage.setItem('sso_code_verifier', codeVerifier);
      sessionStorage.setItem('sso_state', state);
      sessionStorage.setItem('sso_provider', provider.id);

      const redirectUri = `${window.location.origin}/login`;

      this.ssoService.getAuthorizeUrl(provider.id, redirectUri, codeChallenge, state).subscribe({
        next: (res) => {
          window.location.href = res.authorizeUrl;
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Không thể kết nối tới nhà cung cấp SSO');
        },
      });
    } catch {
      this.loading.set(false);
      this.error.set('Lỗi tạo PKCE challenge');
    }
  }

  private handleSsoCallback(): void {
    const params = new URLSearchParams(window.location.search);
    const code = params.get('code');
    const state = params.get('state');

    if (!code || !state) return;

    const savedState = sessionStorage.getItem('sso_state');
    const codeVerifier = sessionStorage.getItem('sso_code_verifier');
    const providerId = sessionStorage.getItem('sso_provider');

    // Clean up
    sessionStorage.removeItem('sso_state');
    sessionStorage.removeItem('sso_code_verifier');
    sessionStorage.removeItem('sso_provider');

    // Clear URL params
    window.history.replaceState({}, '', window.location.pathname);

    if (state !== savedState || !codeVerifier || !providerId) {
      this.error.set('Phiên SSO không hợp lệ. Vui lòng thử lại.');
      return;
    }

    this.loading.set(true);
    const redirectUri = `${window.location.origin}/login`;

    this.ssoService.exchangeToken(providerId, code, redirectUri, codeVerifier).subscribe({
      next: (response) => {
        if (response.accessToken) {
          // Store the tokens (same as normal login)
          localStorage.setItem('ivf_access_token', response.accessToken);
          localStorage.setItem('ivf_refresh_token', response.refreshToken);
          localStorage.setItem('ivf_user', JSON.stringify(response.user));
          this.router.navigate(['/dashboard']);
        } else {
          this.loading.set(false);
          this.error.set(response.error || 'Đăng nhập SSO thất bại');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || 'Đăng nhập SSO thất bại');
      },
    });
  }

  getErrorIcon(): string {
    switch (this.errorCode()) {
      case 'ACCOUNT_LOCKED':
        return '🔒';
      case 'BRUTE_FORCE_LOCKED':
        return '🛡️';
      case 'ZT_BLOCKED':
        return '🚫';
      default:
        return '⚠️';
    }
  }

  onSubmit(): void {
    if (!this.username || !this.password) {
      this.error.set('Vui lòng nhập đầy đủ thông tin');
      this.errorCode.set(null);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.errorCode.set(null);

    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: (response: any) => {
        // Check if MFA or step-up auth is required (server returns code with 200)
        if (response.code === 'MFA_REQUIRED' || response.code === 'STEP_UP_REQUIRED') {
          this.mfaToken.set(response.mfaToken);
          this.mfaMethod.set(response.mfaMethod || 'totp');
          this.step.set('mfa');
          this.loading.set(false);

          // Auto-send SMS OTP if method is SMS
          if (response.mfaMethod === 'sms') {
            this.onResendSms();
          }
          return;
        }
        // Normal successful login
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        const body = err.error;
        if (body?.code) {
          this.errorCode.set(body.code);
          this.error.set(body.error || 'Đăng nhập thất bại');
          if (body.unlocksAt) {
            this.lockUnlocksAt.set(new Date(body.unlocksAt).toLocaleString('vi-VN'));
          }
        } else if (err.status === 401) {
          this.error.set('Sai tên đăng nhập hoặc mật khẩu');
        } else {
          this.error.set('Lỗi kết nối máy chủ');
        }
      },
    });
  }

  onMfaVerify(): void {
    const token = this.mfaToken();
    if (!token || !this.mfaCode) return;

    this.loading.set(true);
    this.error.set(null);

    this.authService.verifyMfa({ mfaToken: token, code: this.mfaCode }).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        const body = err.error;
        if (body?.code === 'MFA_EXPIRED') {
          this.error.set('Phiên xác thực MFA đã hết hạn. Vui lòng đăng nhập lại.');
          setTimeout(() => this.onBackToLogin(), 2000);
        } else {
          this.error.set(body?.error || 'Mã xác thực không đúng');
        }
        this.mfaCode = '';
      },
    });
  }

  onResendSms(): void {
    const token = this.mfaToken();
    if (!token) return;
    this.authService.sendMfaSms(token).subscribe({
      next: () => this.error.set(null),
      error: (err) => this.error.set(err.error?.error || 'Không thể gửi mã OTP'),
    });
  }

  onBackToLogin(): void {
    this.step.set('credentials');
    this.mfaToken.set(null);
    this.mfaCode = '';
    this.error.set(null);
    this.errorCode.set(null);
    this.lockUnlocksAt.set(null);
    this.password = '';
  }

  async onPasskeyLogin(): Promise<void> {
    if (!this.username) {
      this.error.set('Vui lòng nhập tên đăng nhập trước');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.errorCode.set(null);

    try {
      // Step 1: Get assertion options from server
      const beginResponse = await new Promise<{ userId: string; options: any }>(
        (resolve, reject) => {
          this.authService.passkeyLoginBegin(this.username).subscribe({
            next: resolve,
            error: reject,
          });
        },
      );

      const options = beginResponse.options;

      // Convert challenge and allowCredentials
      options.challenge = coerceToArrayBuffer(options.challenge);
      if (options.allowCredentials) {
        options.allowCredentials = options.allowCredentials.map((c: any) => ({
          ...c,
          id: coerceToArrayBuffer(c.id),
        }));
      }

      // Step 2: Browser WebAuthn API
      const credential = (await navigator.credentials.get({
        publicKey: options,
      })) as PublicKeyCredential;
      const assertionResponse = credential.response as AuthenticatorAssertionResponse;

      const assertionPayload = {
        id: credential.id,
        rawId: coerceToBase64Url(new Uint8Array(credential.rawId)),
        type: credential.type,
        response: {
          authenticatorData: coerceToBase64Url(new Uint8Array(assertionResponse.authenticatorData)),
          clientDataJSON: coerceToBase64Url(new Uint8Array(assertionResponse.clientDataJSON)),
          signature: coerceToBase64Url(new Uint8Array(assertionResponse.signature)),
          userHandle: assertionResponse.userHandle
            ? coerceToBase64Url(new Uint8Array(assertionResponse.userHandle))
            : null,
        },
      };

      // Step 3: Send to server to complete
      this.authService.passkeyLoginComplete(beginResponse.userId, assertionPayload).subscribe({
        next: () => {
          this.router.navigate(['/dashboard']);
        },
        error: (err) => {
          this.loading.set(false);
          const body = err.error;
          this.error.set(body?.error || 'Xác thực Passkey thất bại');
          this.errorCode.set(body?.code || null);
        },
      });
    } catch (err: any) {
      this.loading.set(false);
      if (err.error?.error) {
        this.error.set(err.error.error);
        this.errorCode.set(err.error.code || null);
      } else if (err.name === 'NotAllowedError') {
        this.error.set('Xác thực Passkey bị hủy hoặc hết thời gian');
      } else {
        this.error.set('Không thể xác thực bằng Passkey');
      }
    }
  }
}
