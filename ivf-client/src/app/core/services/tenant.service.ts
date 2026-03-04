import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Tenant,
  TenantListItem,
  TenantPlatformStats,
  TenantFeatures,
  CreateTenantRequest,
  UpdateTenantRequest,
  UpdateBrandingRequest,
  UpdateLimitsRequest,
  UpdateSubscriptionRequest,
  UpdateIsolationRequest,
  PricingPlan,
} from '../models/tenant.model';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/tenants`;

  getAll(
    page = 1,
    pageSize = 20,
    search?: string,
    status?: string,
  ): Observable<{ items: TenantListItem[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (status) params = params.set('status', status);
    return this.http.get<{
      items: TenantListItem[];
      totalCount: number;
      page: number;
      pageSize: number;
    }>(this.baseUrl, { params });
  }

  getById(id: string): Observable<Tenant> {
    return this.http.get<Tenant>(`${this.baseUrl}/${id}`);
  }

  getStats(): Observable<TenantPlatformStats> {
    return this.http.get<TenantPlatformStats>(`${this.baseUrl}/stats`);
  }

  getPricing(): Observable<PricingPlan[]> {
    return this.http.get<PricingPlan[]>(`${this.baseUrl}/pricing`);
  }

  create(request: CreateTenantRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, request);
  }

  update(request: UpdateTenantRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${request.id}`, request);
  }

  updateBranding(request: UpdateBrandingRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${request.id}/branding`, request);
  }

  updateLimits(request: UpdateLimitsRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${request.id}/limits`, request);
  }

  activate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/activate`, {});
  }

  suspend(id: string, reason?: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/suspend`, { id, reason });
  }

  cancel(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/cancel`, { id });
  }

  updateSubscription(request: UpdateSubscriptionRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${request.tenantId}/subscription`, request);
  }

  updateIsolation(id: string, request: UpdateIsolationRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/isolation`, request);
  }

  getMyFeatures(): Observable<TenantFeatures> {
    return this.http.get<TenantFeatures>(`${this.baseUrl}/my-features`);
  }
}
