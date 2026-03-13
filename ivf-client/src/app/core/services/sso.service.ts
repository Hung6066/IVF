import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SsoProvider } from '../models/waf.model';

@Injectable({ providedIn: 'root' })
export class SsoService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/auth/sso`;

  getProviders(): Observable<SsoProvider[]> {
    return this.http.get<SsoProvider[]>(`${this.baseUrl}/providers`);
  }

  getAuthorizeUrl(
    providerId: string,
    redirectUri: string,
    codeChallenge: string,
    state: string,
  ): Observable<{ authorizeUrl: string }> {
    const params = new URLSearchParams({
      redirectUri,
      codeChallenge,
      state,
    });
    return this.http.get<{ authorizeUrl: string }>(
      `${this.baseUrl}/${providerId}/authorize-url?${params.toString()}`,
    );
  }

  exchangeToken(
    providerId: string,
    code: string,
    redirectUri: string,
    codeVerifier: string,
  ): Observable<any> {
    return this.http.post(`${this.baseUrl}/${providerId}/token`, {
      code,
      redirectUri,
      codeVerifier,
    });
  }

  // ─── PKCE helpers ───

  async generateCodeVerifier(): Promise<string> {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return this.base64UrlEncode(array);
  }

  async generateCodeChallenge(verifier: string): Promise<string> {
    const encoder = new TextEncoder();
    const data = encoder.encode(verifier);
    const hash = await crypto.subtle.digest('SHA-256', data);
    return this.base64UrlEncode(new Uint8Array(hash));
  }

  generateState(): string {
    const array = new Uint8Array(16);
    crypto.getRandomValues(array);
    return this.base64UrlEncode(array);
  }

  private base64UrlEncode(bytes: Uint8Array): string {
    const str = btoa(String.fromCharCode(...bytes));
    return str.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }
}
