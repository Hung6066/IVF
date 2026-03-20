import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CycleFeeDto } from '../models/clinical-management.models';

export interface CreateCycleFeeRequest {
  cycleId: string;
  patientId: string;
  feeType: string;
  description: string;
  amount: number;
  isOneTimePerCycle: boolean;
}

@Injectable({ providedIn: 'root' })
export class CycleFeeService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getByCycle(cycleId: string): Observable<CycleFeeDto[]> {
    return this.http.get<CycleFeeDto[]>(`${this.baseUrl}/cycle-fees/cycle/${cycleId}`);
  }

  getById(id: string): Observable<CycleFeeDto> {
    return this.http.get<CycleFeeDto>(`${this.baseUrl}/cycle-fees/${id}`);
  }

  checkFeeExists(cycleId: string, feeType: string): Observable<boolean> {
    const params = new HttpParams().set('feeType', feeType);
    return this.http.get<boolean>(`${this.baseUrl}/cycle-fees/cycle/${cycleId}/check`, { params });
  }

  create(request: CreateCycleFeeRequest): Observable<CycleFeeDto> {
    return this.http.post<CycleFeeDto>(`${this.baseUrl}/cycle-fees`, request);
  }

  waive(id: string, reason: string, waivedByUserId: string): Observable<CycleFeeDto> {
    return this.http.put<CycleFeeDto>(`${this.baseUrl}/cycle-fees/${id}/waive`, {
      reason,
      waivedByUserId,
    });
  }

  refund(id: string): Observable<CycleFeeDto> {
    return this.http.put<CycleFeeDto>(`${this.baseUrl}/cycle-fees/${id}/refund`, {});
  }
}
