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
  AvailableTenantDto,
  TenantWorkersResponse,
  TenantEnrolledUsersResponse,
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

  deleteTenantCA(tenantId: string): Observable<TenantCaActionResponse> {
    return this.http.delete<TenantCaActionResponse>(`${this.baseUrl}/${tenantId}`);
  }

  listAvailableTenants(): Observable<AvailableTenantDto[]> {
    return this.http.get<AvailableTenantDto[]>(`${this.baseUrl}/available-tenants`);
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

  getTenantWorkers(tenantId: string): Observable<TenantWorkersResponse> {
    return this.http.get<TenantWorkersResponse>(`${this.baseUrl}/${tenantId}/workers`);
  }

  getEnrolledUsers(tenantId: string): Observable<TenantEnrolledUsersResponse> {
    return this.http.get<TenantEnrolledUsersResponse>(`${this.baseUrl}/${tenantId}/enrolled-users`);
  }
}
