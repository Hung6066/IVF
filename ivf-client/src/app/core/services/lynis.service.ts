import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  LynisHostsResponse,
  LynisReport,
  LynisReportsResponse,
  LynisScanResponse,
  LynisScanStatus,
} from '../models/lynis.model';

@Injectable({ providedIn: 'root' })
export class LynisService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/lynis`;

  getHosts(): Observable<LynisHostsResponse> {
    return this.http.get<LynisHostsResponse>(`${this.baseUrl}/hosts`);
  }

  getReports(hostname?: string): Observable<LynisReportsResponse> {
    let params = new HttpParams();
    if (hostname) params = params.set('hostname', hostname);
    return this.http.get<LynisReportsResponse>(`${this.baseUrl}/reports`, { params });
  }

  getReport(hostname: string, date: string): Observable<LynisReport> {
    return this.http.get<LynisReport>(`${this.baseUrl}/reports/${hostname}/${date}`);
  }

  getLatestReport(hostname: string): Observable<LynisReport> {
    return this.http.get<LynisReport>(`${this.baseUrl}/reports/${hostname}/latest`);
  }

  triggerScan(hostname: string): Observable<LynisScanResponse> {
    return this.http.post<LynisScanResponse>(`${this.baseUrl}/scan`, { hostname });
  }

  getScanStatus(hostname: string): Observable<LynisScanStatus> {
    return this.http.get<LynisScanStatus>(`${this.baseUrl}/scan/${hostname}/status`);
  }
}
