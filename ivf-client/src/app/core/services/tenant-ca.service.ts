import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TenantSubCaListResponse,
  TenantSubCaStatusDto,
  ProvisionTenantCaRequest,
  TenantCaConfigRequest,
  TenantCaProvisionResponse,
  TenantCaConfigResponse,
  TenantCaActionResponse,
  TenantUserCertProvisionResponse,
} from '../models/signing.models';

@Injectable({ providedIn: 'root' })
export class TenantCaService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/tenant-ca`;

  listTenantCAs(): Observable<TenantSubCaListResponse> {
    return this.http.get<TenantSubCaListResponse>(this.baseUrl);
  }

  getTenantCA(tenantId: string): Observable<TenantSubCaStatusDto> {
    return this.http.get<TenantSubCaStatusDto>(`${this.baseUrl}/${tenantId}`);
  }

  provisionTenantCA(
    tenantId: string,
    request: ProvisionTenantCaRequest,
  ): Observable<TenantCaProvisionResponse> {
    return this.http.post<TenantCaProvisionResponse>(
      `${this.baseUrl}/${tenantId}/provision`,
      request,
    );
  }

  updateConfig(
    tenantId: string,
    request: TenantCaConfigRequest,
  ): Observable<TenantCaConfigResponse> {
    return this.http.put<TenantCaConfigResponse>(`${this.baseUrl}/${tenantId}/config`, request);
  }

  suspendTenantCA(tenantId: string): Observable<TenantCaActionResponse> {
    return this.http.post<TenantCaActionResponse>(`${this.baseUrl}/${tenantId}/suspend`, {});
  }

  revokeTenantCA(tenantId: string): Observable<TenantCaActionResponse> {
    return this.http.post<TenantCaActionResponse>(`${this.baseUrl}/${tenantId}/revoke`, {});
  }

  provisionUserCert(
    tenantId: string,
    userId: string,
  ): Observable<TenantUserCertProvisionResponse> {
    return this.http.post<TenantUserCertProvisionResponse>(
      `${this.baseUrl}/${tenantId}/users/${userId}/provision`,
      {},
    );
  }
}
