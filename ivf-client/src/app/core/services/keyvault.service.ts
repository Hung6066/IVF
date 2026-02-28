import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  VaultStatus,
  ApiKeyResponse,
  CreateApiKeyRequest,
  RotateKeyRequest,
  InitializeVaultRequest,
  SecretEntry,
  SecretDetail,
  SecretCreateRequest,
  WrapKeyRequest,
  WrappedKeyResult,
  UnwrapKeyRequest,
  UnwrapKeyResult,
  EncryptDataRequest,
  EncryptedPayload,
  DecryptDataRequest,
  DecryptDataResult,
  AutoUnsealStatus,
  ConfigureAutoUnsealRequest,
  VaultSettings,
  SaveVaultSettingsRequest,
  TestConnectionResult,
  VaultPolicy,
  PolicyCreateRequest,
  VaultUserPolicy,
  UserPolicyAssignRequest,
  VaultLease,
  DynamicCredential,
  DynamicCredentialCreateRequest,
  VaultToken,
  TokenCreateRequest,
  TokenCreateResponse,
  VaultAuditLogResponse,
  SecretImportRequest,
  SecretImportResult,
  EncryptionConfigResponse,
  EncryptionConfigCreateRequest,
  EncryptionConfigUpdateRequest,
  FieldAccessPolicyResponse,
  FieldAccessPolicyCreateRequest,
  FieldAccessPolicyUpdateRequest,
  SecurityDashboard,
  DbTableSchema,
} from '../models/keyvault.model';

@Injectable({ providedIn: 'root' })
export class KeyVaultService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/keyvault`;

  getVaultStatus(): Observable<VaultStatus> {
    return this.http.get<VaultStatus>(`${this.baseUrl}/status`);
  }

  initializeVault(req: InitializeVaultRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/initialize`, req);
  }

  createKey(req: CreateApiKeyRequest): Observable<ApiKeyResponse> {
    return this.http.post<ApiKeyResponse>(`${this.baseUrl}/keys`, req);
  }

  getKey(serviceName: string, keyName: string): Observable<ApiKeyResponse> {
    return this.http.get<ApiKeyResponse>(
      `${this.baseUrl}/keys/${encodeURIComponent(serviceName)}/${encodeURIComponent(keyName)}`,
    );
  }

  rotateKey(req: RotateKeyRequest): Observable<ApiKeyResponse> {
    return this.http.post<ApiKeyResponse>(`${this.baseUrl}/keys/rotate`, req);
  }

  deactivateKey(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/keys/${encodeURIComponent(id)}`);
  }

  getExpiringKeys(withinDays: number): Observable<ApiKeyResponse[]> {
    return this.http.get<ApiKeyResponse[]>(`${this.baseUrl}/keys/expiring`, {
      params: { withinDays: withinDays.toString() },
    });
  }

  checkHealth(): Observable<{ healthy: boolean }> {
    return this.http.get<{ healthy: boolean }>(`${this.baseUrl}/health`);
  }

  // ─── Secrets Management ───────────────────────────
  listSecrets(prefix?: string): Observable<SecretEntry[]> {
    const params: Record<string, string> = {};
    if (prefix) params['prefix'] = prefix;
    return this.http.get<SecretEntry[]>(`${this.baseUrl}/secrets`, { params });
  }

  getSecret(name: string): Observable<SecretDetail> {
    const encodedPath = name
      .split('/')
      .map((s) => encodeURIComponent(s))
      .join('/');
    return this.http.get<SecretDetail>(`${this.baseUrl}/secrets/${encodedPath}`);
  }

  createSecret(req: SecretCreateRequest): Observable<{ success: boolean; name: string }> {
    return this.http.post<{ success: boolean; name: string }>(`${this.baseUrl}/secrets`, req);
  }

  deleteSecret(name: string): Observable<{ success: boolean; name: string }> {
    const encodedPath = name
      .split('/')
      .map((s) => encodeURIComponent(s))
      .join('/');
    return this.http.delete<{ success: boolean; name: string }>(
      `${this.baseUrl}/secrets/${encodedPath}`,
    );
  }

  // ─── Key Wrap / Unwrap (Envelope Encryption) ─────
  wrapKey(req: WrapKeyRequest): Observable<WrappedKeyResult> {
    return this.http.post<WrappedKeyResult>(`${this.baseUrl}/wrap`, req);
  }

  unwrapKey(req: UnwrapKeyRequest): Observable<UnwrapKeyResult> {
    return this.http.post<UnwrapKeyResult>(`${this.baseUrl}/unwrap`, req);
  }

  encrypt(req: EncryptDataRequest): Observable<EncryptedPayload> {
    return this.http.post<EncryptedPayload>(`${this.baseUrl}/encrypt`, req);
  }

  decrypt(req: DecryptDataRequest): Observable<DecryptDataResult> {
    return this.http.post<DecryptDataResult>(`${this.baseUrl}/decrypt`, req);
  }

  // ─── Auto-Unseal ─────────────────────────────────
  getAutoUnsealStatus(): Observable<AutoUnsealStatus> {
    return this.http.get<AutoUnsealStatus>(`${this.baseUrl}/auto-unseal/status`);
  }

  configureAutoUnseal(req: ConfigureAutoUnsealRequest): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/auto-unseal/configure`, req);
  }

  autoUnseal(): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/auto-unseal/unseal`, {});
  }

  // ─── Settings ────────────────────────────────────
  getSettings(): Observable<VaultSettings> {
    return this.http.get<VaultSettings>(`${this.baseUrl}/settings`);
  }

  saveSettings(req: SaveVaultSettingsRequest): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/settings`, req);
  }

  testConnection(): Observable<TestConnectionResult> {
    return this.http.post<TestConnectionResult>(`${this.baseUrl}/test-connection`, {});
  }

  // ─── Policies ────────────────────────────────────
  getPolicies(): Observable<VaultPolicy[]> {
    return this.http.get<VaultPolicy[]>(`${this.baseUrl}/policies`);
  }

  createPolicy(req: PolicyCreateRequest): Observable<{ success: boolean; id: string }> {
    return this.http.post<{ success: boolean; id: string }>(`${this.baseUrl}/policies`, req);
  }

  updatePolicy(id: string, req: PolicyCreateRequest): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/policies/${id}`, req);
  }

  deletePolicy(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/policies/${id}`);
  }

  // ─── User Policies ──────────────────────────────
  getUserPolicies(): Observable<VaultUserPolicy[]> {
    return this.http.get<VaultUserPolicy[]>(`${this.baseUrl}/user-policies`);
  }

  assignUserPolicy(req: UserPolicyAssignRequest): Observable<{ success: boolean; id: string }> {
    return this.http.post<{ success: boolean; id: string }>(`${this.baseUrl}/user-policies`, req);
  }

  removeUserPolicy(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/user-policies/${id}`);
  }

  // ─── Leases ──────────────────────────────────────
  getLeases(includeExpired?: boolean): Observable<VaultLease[]> {
    const params: Record<string, string> = {};
    if (includeExpired) params['includeExpired'] = 'true';
    return this.http.get<VaultLease[]>(`${this.baseUrl}/leases`, { params });
  }

  createLease(req: {
    secretPath: string;
    ttlSeconds: number;
    renewable: boolean;
  }): Observable<{ success: boolean; id: string; leaseId: string; expiresAt: string }> {
    return this.http.post<{ success: boolean; id: string; leaseId: string; expiresAt: string }>(
      `${this.baseUrl}/leases`,
      req,
    );
  }

  revokeLease(leaseId: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(
      `${this.baseUrl}/leases/${encodeURIComponent(leaseId)}/revoke`,
      {},
    );
  }

  renewLease(
    leaseId: string,
    incrementSeconds: number,
  ): Observable<{ success: boolean; expiresAt: string }> {
    return this.http.post<{ success: boolean; expiresAt: string }>(
      `${this.baseUrl}/leases/${encodeURIComponent(leaseId)}/renew`,
      { incrementSeconds },
    );
  }

  // ─── Dynamic Credentials ────────────────────────
  getDynamicCredentials(): Observable<DynamicCredential[]> {
    return this.http.get<DynamicCredential[]>(`${this.baseUrl}/dynamic`);
  }

  createDynamicCredential(
    req: DynamicCredentialCreateRequest,
  ): Observable<{ success: boolean; id: string; leaseId: string }> {
    return this.http.post<{ success: boolean; id: string; leaseId: string }>(
      `${this.baseUrl}/dynamic`,
      req,
    );
  }

  revokeDynamicCredential(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/dynamic/${id}`);
  }

  // ─── Tokens ──────────────────────────────────────
  getTokens(): Observable<VaultToken[]> {
    return this.http.get<VaultToken[]>(`${this.baseUrl}/tokens`);
  }

  createToken(req: TokenCreateRequest): Observable<TokenCreateResponse> {
    return this.http.post<TokenCreateResponse>(`${this.baseUrl}/tokens`, req);
  }

  revokeToken(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/tokens/${id}`);
  }

  // ─── Audit Logs ──────────────────────────────────
  getAuditLogs(
    page?: number,
    pageSize?: number,
    action?: string,
  ): Observable<VaultAuditLogResponse> {
    const params: Record<string, string> = {};
    if (page) params['page'] = page.toString();
    if (pageSize) params['pageSize'] = pageSize.toString();
    if (action) params['action'] = action;
    return this.http.get<VaultAuditLogResponse>(`${this.baseUrl}/audit-logs`, { params });
  }

  // ─── Import ──────────────────────────────────────
  importSecrets(req: SecretImportRequest): Observable<SecretImportResult> {
    return this.http.post<SecretImportResult>(`${this.baseUrl}/secrets/import`, req);
  }

  // ─── Encryption Configs ─────────────────────────
  getEncryptionConfigs(): Observable<EncryptionConfigResponse[]> {
    return this.http.get<EncryptionConfigResponse[]>(`${this.baseUrl}/encryption-configs`);
  }

  createEncryptionConfig(
    req: EncryptionConfigCreateRequest,
  ): Observable<{ success: boolean; id: string }> {
    return this.http.post<{ success: boolean; id: string }>(
      `${this.baseUrl}/encryption-configs`,
      req,
    );
  }

  updateEncryptionConfig(
    id: string,
    req: EncryptionConfigUpdateRequest,
  ): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/encryption-configs/${id}`, req);
  }

  toggleEncryptionConfig(id: string): Observable<{ success: boolean; isEnabled: boolean }> {
    return this.http.put<{ success: boolean; isEnabled: boolean }>(
      `${this.baseUrl}/encryption-configs/${id}/toggle`,
      {},
    );
  }

  deleteEncryptionConfig(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/encryption-configs/${id}`);
  }

  // ─── Field Access Policies ──────────────────────
  getFieldAccessPolicies(): Observable<FieldAccessPolicyResponse[]> {
    return this.http.get<FieldAccessPolicyResponse[]>(`${this.baseUrl}/field-access-policies`);
  }

  createFieldAccessPolicy(
    req: FieldAccessPolicyCreateRequest,
  ): Observable<{ success: boolean; id: string }> {
    return this.http.post<{ success: boolean; id: string }>(
      `${this.baseUrl}/field-access-policies`,
      req,
    );
  }

  updateFieldAccessPolicy(
    id: string,
    req: FieldAccessPolicyUpdateRequest,
  ): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/field-access-policies/${id}`, req);
  }

  deleteFieldAccessPolicy(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/field-access-policies/${id}`);
  }

  // ─── Security Dashboard ─────────────────────────
  getSecurityDashboard(): Observable<SecurityDashboard> {
    return this.http.get<SecurityDashboard>(`${this.baseUrl}/security-dashboard`);
  }

  // ─── DB Schema Introspection ────────────────────
  getDbSchema(): Observable<DbTableSchema[]> {
    return this.http.get<DbTableSchema[]>(`${this.baseUrl}/db-schema`);
  }
}
