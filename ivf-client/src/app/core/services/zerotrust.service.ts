import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ZTPolicyResponse,
  ZTAccessDecision,
  UpdateZTPolicyRequest,
} from '../models/zerotrust.model';

@Injectable({ providedIn: 'root' })
export class ZeroTrustService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/zerotrust`;

  getAllPolicies(): Observable<ZTPolicyResponse[]> {
    return this.http.get<ZTPolicyResponse[]>(`${this.baseUrl}/policies`);
  }

  updatePolicy(req: UpdateZTPolicyRequest): Observable<ZTPolicyResponse> {
    return this.http.put<ZTPolicyResponse>(`${this.baseUrl}/policies`, req);
  }

  checkAccess(req: Record<string, unknown>): Observable<ZTAccessDecision> {
    return this.http.post<ZTAccessDecision>(`${this.baseUrl}/check`, req);
  }
}
