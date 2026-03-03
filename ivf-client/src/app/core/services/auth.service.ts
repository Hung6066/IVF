import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError, from, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, User, MfaVerifyRequest } from '../models/api.models';
import { ConsentBannerService } from './consent-banner.service';

const TOKEN_KEY = 'ivf_access_token';
const REFRESH_KEY = 'ivf_refresh_token';
const USER_KEY = 'ivf_user';
const PERMISSIONS_KEY = 'ivf_permissions';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;
  private readonly usersApiUrl = `${environment.apiUrl}/users`;

  private _user = signal<User | null>(this.loadUser());
  private _permissions = signal<string[]>(this.loadPermissions());
  private _isAuthenticated = computed(() => !!this._user() && !!this.getToken());

  readonly user = this._user.asReadonly();
  readonly permissions = this._permissions.asReadonly();
  readonly isAuthenticated = this._isAuthenticated;

  constructor(
    private http: HttpClient,
    private router: Router,
    private consentBanner: ConsentBannerService,
  ) {}

  login(credentials: LoginRequest): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/login`, credentials).pipe(
      tap((response) => {
        // Only handle auth success if we got a real token (not MFA_REQUIRED)
        if (response.accessToken) {
          this.handleAuthSuccess(response);
          this.loadUserPermissions(response.user.id);
        }
      }),
      catchError((err) => {
        console.error('Login failed:', err);
        return throwError(() => err);
      }),
    );
  }

  refreshToken(): Observable<AuthResponse> {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }
    return this.http.post<AuthResponse>(`${this.apiUrl}/refresh`, { refreshToken }).pipe(
      tap((response) => {
        this.handleAuthSuccess(response);
        this.loadUserPermissions(response.user.id);
      }),
      catchError((err) => {
        this.logout();
        return throwError(() => err);
      }),
    );
  }

  logout(): void {
    // Record logout on server (fire-and-forget)
    const token = this.getToken();
    if (token) {
      this.http.post(`${this.apiUrl}/logout`, {}).subscribe({ error: () => {} });
    }
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(PERMISSIONS_KEY);
    sessionStorage.removeItem('zt_session_id');
    sessionStorage.removeItem('zt_device_fingerprint');
    this._user.set(null);
    this._permissions.set([]);
    this.consentBanner.clear();
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  hasRole(role: string): boolean {
    return this._user()?.role === role;
  }

  hasPermission(permission: string): boolean {
    // Admin has all permissions
    if (this._user()?.role === 'Admin') return true;
    return this._permissions().includes(permission);
  }

  hasAnyPermission(permissions: string[]): boolean {
    if (this._user()?.role === 'Admin') return true;
    return permissions.some((p) => this._permissions().includes(p));
  }

  setPermissions(permissions: string[]): void {
    localStorage.setItem(PERMISSIONS_KEY, JSON.stringify(permissions));
    this._permissions.set(permissions);
  }

  // Load permissions from API for the current logged-in user
  loadUserPermissions(userId?: string): void {
    // Use /me/permissions endpoint - works for any authenticated user
    this.http.get<string[]>(`${this.apiUrl}/me/permissions`).subscribe({
      next: (permissions) => {
        this.setPermissions(permissions || []);
      },
      error: () => {
        // If cannot load permissions, set empty (fallback)
        this.setPermissions([]);
      },
    });
  }

  verifyMfa(request: MfaVerifyRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/mfa-verify`, request).pipe(
      tap((response) => {
        this.handleAuthSuccess(response);
        this.loadUserPermissions(response.user.id);
      }),
      catchError((err) => throwError(() => err)),
    );
  }

  sendMfaSms(mfaToken: string): Observable<{ message: string; devOtp?: string }> {
    return this.http.post<{ message: string; devOtp?: string }>(`${this.apiUrl}/mfa-send-sms`, {
      mfaToken,
    });
  }

  passkeyLoginBegin(username: string): Observable<{ userId: string; options: any }> {
    return this.http.post<{ userId: string; options: any }>(`${this.apiUrl}/passkey-login/begin`, {
      username,
    });
  }

  passkeyLoginComplete(userId: string, assertionResponse: any): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(
        `${this.apiUrl}/passkey-login/complete`,
        { userId, assertionResponse },
        { headers: { 'Content-Type': 'application/json' } },
      )
      .pipe(
        tap((response) => {
          this.handleAuthSuccess(response);
          this.loadUserPermissions(response.user.id);
        }),
        catchError((err) => throwError(() => err)),
      );
  }

  private handleAuthSuccess(response: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    this._user.set(response.user);

    // Extract session_id from JWT for Zero Trust token binding
    try {
      const payload = JSON.parse(atob(response.accessToken.split('.')[1]));
      if (payload.session_id) {
        sessionStorage.setItem('zt_session_id', payload.session_id);
      }
    } catch {
      /* non-critical */
    }
  }

  private loadUser(): User | null {
    const userJson = localStorage.getItem(USER_KEY);
    return userJson ? JSON.parse(userJson) : null;
  }

  private loadPermissions(): string[] {
    const permsJson = localStorage.getItem(PERMISSIONS_KEY);
    return permsJson ? JSON.parse(permsJson) : [];
  }
}
