import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  UserSignature,
  UserSignatureListResponse,
  CertProvisionResult,
  UserTestSignResult,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class UserSignatureService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/user-signatures`;

  // ─── Current User ───────────────────────────────────────

  getMySignature(): Observable<UserSignature> {
    return this.http.get<UserSignature>(`${this.baseUrl}/me`);
  }

  uploadMySignature(signatureImageBase64: string): Observable<UserSignature> {
    return this.http.post<UserSignature>(`${this.baseUrl}/me`, { signatureImageBase64 });
  }

  deleteMySignature(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/me`);
  }

  // ─── Admin: User Management ─────────────────────────────

  listSignatures(activeOnly?: boolean): Observable<UserSignatureListResponse> {
    const params = activeOnly ? `?activeOnly=${activeOnly}` : '';
    return this.http.get<UserSignatureListResponse>(`${this.baseUrl}${params}`);
  }

  getUserSignature(userId: string): Observable<UserSignature> {
    return this.http.get<UserSignature>(`${this.baseUrl}/users/${userId}`);
  }

  uploadUserSignature(userId: string, signatureImageBase64: string): Observable<UserSignature> {
    return this.http.post<UserSignature>(`${this.baseUrl}/users/${userId}`, {
      signatureImageBase64,
    });
  }

  // ─── Certificate Provisioning ───────────────────────────

  provisionCertificate(userId: string): Observable<CertProvisionResult> {
    return this.http.post<CertProvisionResult>(
      `${this.baseUrl}/users/${userId}/provision-certificate`,
      {},
    );
  }

  testUserSigning(userId: string): Observable<UserTestSignResult> {
    return this.http.post<UserTestSignResult>(`${this.baseUrl}/users/${userId}/test-sign`, {});
  }

  // ─── Image URLs ─────────────────────────────────────────

  getSignatureImageUrl(signatureId: string): string {
    return `${this.baseUrl}/${signatureId}/image`;
  }

  getUserSignatureImageUrl(userId: string): string {
    return `${this.baseUrl}/users/${userId}/image`;
  }
}
