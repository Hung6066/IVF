import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SecurityScore,
  LoginHistoryEntry,
  RateLimitStatus,
  RateLimitEvent,
  CreateRateLimitRequest,
  UpdateRateLimitRequest,
  RateLimitCustomConfig,
  GeoSecurityData,
  GeoBlockRule,
  CreateGeoBlockRuleRequest,
  ThreatOverview,
  AccountLockout,
  LockAccountRequest,
  WhitelistedIp,
  AddIpWhitelistRequest,
  UpdateIpWhitelistRequest,
  UserDevice,
  PasskeyCredential,
  PasskeyRegisterBeginRequest,
  PasskeyRegisterCompleteRequest,
  RenamePasskeyRequest,
  MfaSettings,
  TotpSetupResponse,
  TotpVerifyRequest,
  SmsRegisterRequest,
  SmsVerifyRequest,
} from '../models/advanced-security.model';

@Injectable({ providedIn: 'root' })
export class AdvancedSecurityService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/security/advanced`;

  // ─── Security Score ───
  getSecurityScore(): Observable<SecurityScore> {
    return this.http.get<SecurityScore>(`${this.baseUrl}/score`);
  }

  // ─── Login History ───
  getLoginHistory(count = 50): Observable<LoginHistoryEntry[]> {
    const params = new HttpParams().set('count', count);
    return this.http.get<LoginHistoryEntry[]>(`${this.baseUrl}/login-history`, { params });
  }

  // ─── Rate Limit ───
  getRateLimitStatus(): Observable<RateLimitStatus> {
    return this.http.get<RateLimitStatus>(`${this.baseUrl}/rate-limits`);
  }

  getRateLimitEvents(hours = 24): Observable<RateLimitEvent[]> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<RateLimitEvent[]>(`${this.baseUrl}/rate-limit-events`, { params });
  }

  createRateLimit(request: CreateRateLimitRequest): Observable<RateLimitCustomConfig> {
    return this.http.post<RateLimitCustomConfig>(`${this.baseUrl}/rate-limits`, request);
  }

  updateRateLimit(id: string, request: UpdateRateLimitRequest): Observable<RateLimitCustomConfig> {
    return this.http.put<RateLimitCustomConfig>(`${this.baseUrl}/rate-limits/${id}`, request);
  }

  deleteRateLimit(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/rate-limits/${id}`);
  }

  // ─── Geo Security ───
  getGeoSecurityData(hours = 48): Observable<GeoSecurityData> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<GeoSecurityData>(`${this.baseUrl}/geo-events`, { params });
  }

  createGeoBlockRule(request: CreateGeoBlockRuleRequest): Observable<GeoBlockRule> {
    return this.http.post<GeoBlockRule>(`${this.baseUrl}/geo-rules`, request);
  }

  deleteGeoBlockRule(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/geo-rules/${id}`);
  }

  // ─── Threats ───
  getThreatOverview(hours = 24): Observable<ThreatOverview> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<ThreatOverview>(`${this.baseUrl}/threats`, { params });
  }

  // ─── Account Lockouts ───
  getAccountLockouts(): Observable<AccountLockout[]> {
    return this.http.get<AccountLockout[]>(`${this.baseUrl}/lockouts`);
  }

  lockAccount(request: LockAccountRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/lockouts`, request);
  }

  unlockAccount(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/lockouts/${id}/unlock`, {});
  }

  // ─── IP Whitelist ───
  getIpWhitelist(): Observable<WhitelistedIp[]> {
    return this.http.get<WhitelistedIp[]>(`${this.baseUrl}/ip-whitelist`);
  }

  addIpToWhitelist(request: AddIpWhitelistRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/ip-whitelist`, request);
  }

  updateIpWhitelist(
    id: string,
    request: UpdateIpWhitelistRequest,
  ): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.baseUrl}/ip-whitelist/${id}`, request);
  }

  removeIpFromWhitelist(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/ip-whitelist/${id}`);
  }

  // ─── Devices ───
  getUserDevices(userId: string): Observable<UserDevice[]> {
    return this.http.get<UserDevice[]>(`${this.baseUrl}/devices/${userId}`);
  }

  trustDevice(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/devices/${id}/trust`, {});
  }

  removeDevice(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/devices/${id}`);
  }

  // ─── Passkeys / WebAuthn ───
  getPasskeys(userId: string): Observable<PasskeyCredential[]> {
    return this.http.get<PasskeyCredential[]>(`${this.baseUrl}/passkeys/${userId}`);
  }

  beginPasskeyRegistration(request: PasskeyRegisterBeginRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/passkeys/register/begin`, request);
  }

  completePasskeyRegistration(
    request: PasskeyRegisterCompleteRequest,
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/passkeys/register/complete`,
      request,
    );
  }

  beginPasskeyAuthentication(userId: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/passkeys/authenticate/begin`, { userId });
  }

  completePasskeyAuthentication(request: any): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/passkeys/authenticate/complete`,
      request,
    );
  }

  revokePasskey(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/passkeys/${id}/revoke`, {});
  }

  renamePasskey(id: string, request: RenamePasskeyRequest): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.baseUrl}/passkeys/${id}/rename`, request);
  }

  // ─── TOTP ───
  setupTotp(userId: string): Observable<TotpSetupResponse> {
    return this.http.post<TotpSetupResponse>(`${this.baseUrl}/totp/setup`, { userId });
  }

  verifyTotp(
    request: TotpVerifyRequest,
  ): Observable<{ message: string; recoveryCodes?: string[] }> {
    return this.http.post<{ message: string; recoveryCodes?: string[] }>(
      `${this.baseUrl}/totp/verify`,
      request,
    );
  }

  validateTotp(request: TotpVerifyRequest): Observable<{ valid: boolean }> {
    return this.http.post<{ valid: boolean }>(`${this.baseUrl}/totp/validate`, request);
  }

  // ─── SMS OTP ───
  registerSmsOtp(request: SmsRegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/sms-otp/register`, request);
  }

  verifySmsOtp(request: SmsVerifyRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/sms-otp/verify`, request);
  }

  sendSmsOtp(userId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/sms-otp/send`, { userId });
  }

  // ─── MFA Settings ───
  getMfaSettings(userId: string): Observable<MfaSettings> {
    return this.http.get<MfaSettings>(`${this.baseUrl}/mfa/${userId}`);
  }

  disableMfa(userId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/mfa/${userId}/disable`, {});
  }
}
