import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConditionalAccessPolicy,
  CreateConditionalAccessRequest,
  IncidentResponseRule,
  CreateIncidentRuleRequest,
  SecurityIncident,
  DataRetentionPolicy,
  CreateDataRetentionRequest,
  ImpersonationRequest,
  CreateImpersonationRequest,
  PermissionDelegation,
  CreateDelegationRequest,
  UserBehaviorProfile,
  NotificationPreference,
  CreateNotificationPrefRequest,
  PagedResult,
} from '../models/enterprise-security.model';

@Injectable({ providedIn: 'root' })
export class EnterpriseSecurityService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/security/enterprise`;

  // ─── Conditional Access Policies ───

  getConditionalAccessPolicies(): Observable<ConditionalAccessPolicy[]> {
    return this.http.get<ConditionalAccessPolicy[]>(`${this.baseUrl}/conditional-access`);
  }

  getConditionalAccessPolicy(id: string): Observable<ConditionalAccessPolicy> {
    return this.http.get<ConditionalAccessPolicy>(`${this.baseUrl}/conditional-access/${id}`);
  }

  createConditionalAccessPolicy(
    request: CreateConditionalAccessRequest,
  ): Observable<ConditionalAccessPolicy> {
    return this.http.post<ConditionalAccessPolicy>(`${this.baseUrl}/conditional-access`, request);
  }

  updateConditionalAccessPolicy(
    id: string,
    request: CreateConditionalAccessRequest,
  ): Observable<ConditionalAccessPolicy> {
    return this.http.put<ConditionalAccessPolicy>(
      `${this.baseUrl}/conditional-access/${id}`,
      request,
    );
  }

  enableConditionalAccessPolicy(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/conditional-access/${id}/enable`,
      {},
    );
  }

  disableConditionalAccessPolicy(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/conditional-access/${id}/disable`,
      {},
    );
  }

  deleteConditionalAccessPolicy(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/conditional-access/${id}`);
  }

  // ─── Incident Response Rules ───

  getIncidentRules(): Observable<IncidentResponseRule[]> {
    return this.http.get<IncidentResponseRule[]>(`${this.baseUrl}/incident-rules`);
  }

  createIncidentRule(request: CreateIncidentRuleRequest): Observable<IncidentResponseRule> {
    return this.http.post<IncidentResponseRule>(`${this.baseUrl}/incident-rules`, request);
  }

  updateIncidentRule(
    id: string,
    request: CreateIncidentRuleRequest,
  ): Observable<IncidentResponseRule> {
    return this.http.put<IncidentResponseRule>(`${this.baseUrl}/incident-rules/${id}`, request);
  }

  deleteIncidentRule(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/incident-rules/${id}`);
  }

  // ─── Security Incidents ───

  getIncidents(
    page = 1,
    pageSize = 20,
    status?: string,
    severity?: string,
  ): Observable<PagedResult<SecurityIncident>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (severity) params = params.set('severity', severity);
    return this.http.get<PagedResult<SecurityIncident>>(`${this.baseUrl}/incidents`, { params });
  }

  getIncident(id: string): Observable<SecurityIncident> {
    return this.http.get<SecurityIncident>(`${this.baseUrl}/incidents/${id}`);
  }

  investigateIncident(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/incidents/${id}/investigate`, {});
  }

  resolveIncident(id: string, resolution: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/incidents/${id}/resolve`, {
      resolution,
    });
  }

  closeIncident(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/incidents/${id}/close`, {});
  }

  markIncidentFalsePositive(id: string, resolution: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/incidents/${id}/false-positive`, {
      resolution,
    });
  }

  // ─── Data Retention Policies ───

  getDataRetentionPolicies(): Observable<DataRetentionPolicy[]> {
    return this.http.get<DataRetentionPolicy[]>(`${this.baseUrl}/data-retention`);
  }

  createDataRetentionPolicy(request: CreateDataRetentionRequest): Observable<DataRetentionPolicy> {
    return this.http.post<DataRetentionPolicy>(`${this.baseUrl}/data-retention`, request);
  }

  updateDataRetentionPolicy(
    id: string,
    request: { retentionDays: number; action: string; description?: string },
  ): Observable<DataRetentionPolicy> {
    return this.http.put<DataRetentionPolicy>(`${this.baseUrl}/data-retention/${id}`, request);
  }

  deleteDataRetentionPolicy(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/data-retention/${id}`);
  }

  // ─── Impersonation ───

  getImpersonationRequests(
    page = 1,
    pageSize = 20,
    status?: string,
  ): Observable<PagedResult<ImpersonationRequest>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<ImpersonationRequest>>(`${this.baseUrl}/impersonation`, {
      params,
    });
  }

  createImpersonationRequest(
    request: CreateImpersonationRequest,
  ): Observable<ImpersonationRequest> {
    return this.http.post<ImpersonationRequest>(`${this.baseUrl}/impersonation`, request);
  }

  approveImpersonation(
    id: string,
    durationMinutes = 30,
  ): Observable<{ message: string; sessionToken: string }> {
    return this.http.post<{ message: string; sessionToken: string }>(
      `${this.baseUrl}/impersonation/${id}/approve`,
      { durationMinutes },
    );
  }

  denyImpersonation(id: string, reason?: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/impersonation/${id}/deny`, {
      reason,
    });
  }

  endImpersonation(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/impersonation/${id}/end`, {});
  }

  // ─── Permission Delegation ───

  getActiveDelegations(): Observable<PermissionDelegation[]> {
    return this.http.get<PermissionDelegation[]>(`${this.baseUrl}/delegations`);
  }

  createDelegation(request: CreateDelegationRequest): Observable<PermissionDelegation> {
    return this.http.post<PermissionDelegation>(`${this.baseUrl}/delegations`, request);
  }

  revokeDelegation(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/delegations/${id}/revoke`, {});
  }

  // ─── Behavioral Analytics ───

  getBehaviorProfiles(): Observable<UserBehaviorProfile[]> {
    return this.http.get<UserBehaviorProfile[]>(`${this.baseUrl}/behavior-profiles`);
  }

  getBehaviorProfile(userId: string): Observable<UserBehaviorProfile> {
    return this.http.get<UserBehaviorProfile>(`${this.baseUrl}/behavior-profiles/${userId}`);
  }

  refreshBehaviorProfile(userId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/behavior-profiles/${userId}/refresh`,
      {},
    );
  }

  // ─── Notification Preferences ───

  getNotificationPreferences(userId: string): Observable<NotificationPreference[]> {
    return this.http.get<NotificationPreference[]>(
      `${this.baseUrl}/notification-preferences/${userId}`,
    );
  }

  createNotificationPreference(
    request: CreateNotificationPrefRequest,
  ): Observable<NotificationPreference> {
    return this.http.post<NotificationPreference>(
      `${this.baseUrl}/notification-preferences`,
      request,
    );
  }

  deleteNotificationPreference(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/notification-preferences/${id}`);
  }
}
