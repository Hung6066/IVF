import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { WafStatus, WafEvent } from '../models/waf.model';

@Injectable({ providedIn: 'root' })
export class WafService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/waf`;

  getStatus(): Observable<WafStatus> {
    return this.http.get<WafStatus>(`${this.baseUrl}/status`);
  }

  getEvents(limit = 50): Observable<WafEvent[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<WafEvent[]>(`${this.baseUrl}/events`, { params });
  }
}
