import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SecurityEvent,
  SecurityDashboard,
  ThreatAssessment,
  IpIntelligence,
  DeviceTrust,
  SessionInfo,
  AssessRequest,
} from '../models/security.model';

@Injectable({ providedIn: 'root' })
export class SecurityService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/security`;

  getDashboard(): Observable<SecurityDashboard> {
    return this.http.get<SecurityDashboard>(`${this.baseUrl}/dashboard`);
  }

  getRecentEvents(count = 50): Observable<SecurityEvent[]> {
    const params = new HttpParams().set('count', count);
    return this.http.get<SecurityEvent[]>(`${this.baseUrl}/events/recent`, { params });
  }

  getUserEvents(userId: string, hours = 24): Observable<SecurityEvent[]> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<SecurityEvent[]>(`${this.baseUrl}/events/user/${userId}`, { params });
  }

  getIpEvents(ipAddress: string, hours = 24): Observable<SecurityEvent[]> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<SecurityEvent[]>(
      `${this.baseUrl}/events/ip/${encodeURIComponent(ipAddress)}`,
      { params },
    );
  }

  getHighSeverityEvents(hours = 24): Observable<SecurityEvent[]> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<SecurityEvent[]>(`${this.baseUrl}/events/high-severity`, { params });
  }

  assessThreat(request: AssessRequest): Observable<ThreatAssessment> {
    return this.http.post<ThreatAssessment>(`${this.baseUrl}/assess`, request);
  }

  getActiveSessions(userId: string): Observable<SessionInfo[]> {
    return this.http.get<SessionInfo[]>(`${this.baseUrl}/sessions/${userId}`);
  }

  revokeSession(sessionId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/sessions/${sessionId}`);
  }

  checkIpIntelligence(ipAddress: string): Observable<IpIntelligence> {
    return this.http.get<IpIntelligence>(
      `${this.baseUrl}/ip-intelligence/${encodeURIComponent(ipAddress)}`,
    );
  }

  checkDeviceTrust(userId: string, fingerprint: string): Observable<DeviceTrust> {
    return this.http.get<DeviceTrust>(
      `${this.baseUrl}/device-trust/${userId}/${encodeURIComponent(fingerprint)}`,
    );
  }
}
