import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  WafStatus,
  WafEvent,
  AppWafRule,
  AppWafEvent,
  AppWafEventsResponse,
  AppWafAnalytics,
  CreateWafRuleRequest,
  UpdateWafRuleRequest,
} from '../models/waf.model';

@Injectable({ providedIn: 'root' })
export class WafService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/waf`;

  // ─── Application-Level WAF Rules ───

  getRules(group?: string): Observable<AppWafRule[]> {
    let params = new HttpParams();
    if (group) params = params.set('group', group);
    return this.http.get<AppWafRule[]>(`${this.baseUrl}/rules`, { params });
  }

  getRuleById(id: string): Observable<AppWafRule> {
    return this.http.get<AppWafRule>(`${this.baseUrl}/rules/${id}`);
  }

  createRule(request: CreateWafRuleRequest): Observable<AppWafRule> {
    return this.http.post<AppWafRule>(`${this.baseUrl}/rules`, request);
  }

  updateRule(id: string, request: UpdateWafRuleRequest): Observable<AppWafRule> {
    return this.http.put<AppWafRule>(`${this.baseUrl}/rules/${id}`, request);
  }

  deleteRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/rules/${id}`);
  }

  toggleRule(id: string, enable: boolean): Observable<{ id: string; isEnabled: boolean }> {
    return this.http.put<{ id: string; isEnabled: boolean }>(
      `${this.baseUrl}/rules/${id}/toggle`,
      { id, enable }
    );
  }

  // ─── WAF Events & Analytics ───

  getWafEvents(
    page = 1,
    pageSize = 50,
    filters?: {
      dateFrom?: string;
      dateTo?: string;
      ip?: string;
      ruleGroup?: string;
      action?: string;
    }
  ): Observable<AppWafEventsResponse> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (filters?.dateFrom) params = params.set('dateFrom', filters.dateFrom);
    if (filters?.dateTo) params = params.set('dateTo', filters.dateTo);
    if (filters?.ip) params = params.set('ip', filters.ip);
    if (filters?.ruleGroup) params = params.set('ruleGroup', filters.ruleGroup);
    if (filters?.action) params = params.set('action', filters.action);

    return this.http.get<AppWafEventsResponse>(`${this.baseUrl}/events`, { params });
  }

  getAnalytics(): Observable<AppWafAnalytics> {
    return this.http.get<AppWafAnalytics>(`${this.baseUrl}/analytics`);
  }

  invalidateCache(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/cache/invalidate`, {});
  }

  // ─── Cloudflare Edge WAF (existing) ───

  getStatus(): Observable<WafStatus> {
    return this.http.get<WafStatus>(`${this.baseUrl}/cloudflare/status`);
  }

  getCloudflareEvents(limit = 50): Observable<WafEvent[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<WafEvent[]>(`${this.baseUrl}/cloudflare/events`, { params });
  }
}
