import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DnsRecord,
  CreateDnsRecordRequest,
  DnsRecordResponse,
  DnsListResponse,
} from '../models/dns-record.model';

@Injectable({ providedIn: 'root' })
export class DnsManagementService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/dns-records`;

  /**
   * Get all DNS records for the current tenant
   */
  getDnsRecords(): Observable<DnsListResponse[]> {
    return this.http.get<DnsListResponse[]>(this.baseUrl);
  }

  /**
   * Create a new DNS record
   */
  createRecord(request: CreateDnsRecordRequest): Observable<DnsRecordResponse> {
    return this.http.post<DnsRecordResponse>(this.baseUrl, request);
  }

  /**
   * Delete a DNS record
   */
  deleteRecord(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
