import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLog, AuditSearchParams } from '../models/audit.models';

@Injectable({ providedIn: 'root' })
export class AuditService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getRecentAuditLogs(take = 100): Observable<AuditLog[]> {
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/recent`, {
            params: new HttpParams().set('take', take)
        });
    }

    getEntityAuditLogs(entityType: string, entityId: string): Observable<AuditLog[]> {
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/entity/${entityType}/${entityId}`);
    }

    getUserAuditLogs(userId: string, take = 100): Observable<AuditLog[]> {
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/user/${userId}`, {
            params: new HttpParams().set('take', take)
        });
    }

    searchAuditLogs(params: AuditSearchParams): Observable<AuditLog[]> {
        let httpParams = new HttpParams();
        if (params.entityType) httpParams = httpParams.set('entityType', params.entityType);
        if (params.action) httpParams = httpParams.set('action', params.action);
        if (params.userId) httpParams = httpParams.set('userId', params.userId);
        if (params.from) httpParams = httpParams.set('from', params.from.toISOString());
        if (params.to) httpParams = httpParams.set('to', params.to.toISOString());
        httpParams = httpParams.set('page', params.page || 1).set('pageSize', params.pageSize || 50);
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/search`, { params: httpParams });
    }
}
