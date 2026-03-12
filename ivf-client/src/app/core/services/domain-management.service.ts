import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CaddySyncResult, TenantDomainInfo } from '../models/domain-management.model';

@Injectable({ providedIn: 'root' })
export class DomainManagementService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/domains`;

  getDomains(): Observable<TenantDomainInfo[]> {
    return this.http.get<TenantDomainInfo[]>(this.baseUrl);
  }

  getPreview(): Observable<string> {
    return this.http.get(`${this.baseUrl}/preview`, { responseType: 'text' });
  }

  getCurrentConfig(): Observable<string> {
    return this.http.get(`${this.baseUrl}/current`, { responseType: 'text' });
  }

  syncConfig(): Observable<CaddySyncResult> {
    return this.http.post<CaddySyncResult>(`${this.baseUrl}/sync`, {});
  }
}
