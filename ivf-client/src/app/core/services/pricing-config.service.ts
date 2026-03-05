import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  FeatureDefinitionDto,
  PlanDefinitionDto,
  TenantFeatureDto,
  CreateFeatureRequest,
  UpdateFeatureRequest,
  CreatePlanRequest,
  UpdatePlanRequest,
  PlanFeatureItem,
} from '../models/tenant.model';

@Injectable({ providedIn: 'root' })
export class PricingConfigService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/tenants`;

  // ── Feature Definitions ──

  getFeatures(): Observable<FeatureDefinitionDto[]> {
    return this.http.get<FeatureDefinitionDto[]>(`${this.baseUrl}/feature-definitions`);
  }

  createFeature(request: CreateFeatureRequest): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}/feature-definitions`, request);
  }

  updateFeature(id: string, request: UpdateFeatureRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/feature-definitions/${id}`, request);
  }

  deleteFeature(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/feature-definitions/${id}`);
  }

  // ── Plan Definitions ──

  getPlans(): Observable<PlanDefinitionDto[]> {
    return this.http.get<PlanDefinitionDto[]>(`${this.baseUrl}/plan-definitions`);
  }

  createPlan(request: CreatePlanRequest): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}/plan-definitions`, request);
  }

  updatePlan(id: string, request: UpdatePlanRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/plan-definitions/${id}`, request);
  }

  deletePlan(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/plan-definitions/${id}`);
  }

  // ── Plan-Feature Mapping ──

  updatePlanFeatures(planId: string, featureIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/plan-definitions/${planId}/features`, {
      featureDefinitionIds: featureIds,
    });
  }

  // ── Tenant Feature Overrides ──

  getTenantFeatures(tenantId: string): Observable<TenantFeatureDto[]> {
    return this.http.get<TenantFeatureDto[]>(`${this.baseUrl}/${tenantId}/features`);
  }

  updateTenantFeatures(
    tenantId: string,
    updates: { featureDefinitionId: string; isEnabled: boolean }[],
  ): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${tenantId}/features`, { features: updates });
  }
}
