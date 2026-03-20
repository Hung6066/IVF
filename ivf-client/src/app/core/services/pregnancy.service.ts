import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PregnancyDto {
  cycleId: string;
  betaHcg?: number;
  betaHcgDate?: string;
  isPregnant: boolean;
  gestationalSacs?: number;
  fetalHeartbeats?: number;
  dueDate?: string;
  notes?: string;
  pregnancyStatus?: string;
}

export interface BetaHcgResultDto {
  value?: number;
  testDate?: string;
  isPregnant: boolean;
  status: string;
}

export interface FollowUpItemDto {
  scheduledDate: string;
  visitType: string;
  description: string;
  isCompleted: boolean;
}

@Injectable({ providedIn: 'root' })
export class PregnancyService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/pregnancy`;

  getByCycle(cycleId: string): Observable<PregnancyDto> {
    return this.http.get<PregnancyDto>(`${this.baseUrl}/cycle/${cycleId}`);
  }

  getBetaHcgResults(cycleId: string): Observable<BetaHcgResultDto[]> {
    return this.http.get<BetaHcgResultDto[]>(`${this.baseUrl}/cycle/${cycleId}/beta-hcg`);
  }

  getFollowUp(cycleId: string): Observable<FollowUpItemDto[]> {
    return this.http.get<FollowUpItemDto[]>(`${this.baseUrl}/cycle/${cycleId}/follow-up`);
  }

  recordBetaHcg(
    cycleId: string,
    betaHcg: number,
    testDate: string,
    notes?: string,
  ): Observable<PregnancyDto> {
    return this.http.post<PregnancyDto>(`${this.baseUrl}/cycle/${cycleId}/beta-hcg`, {
      betaHcg,
      testDate,
      notes,
    });
  }

  notifyResult(
    cycleId: string,
    channel: string,
    message?: string,
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/cycle/${cycleId}/notify`, {
      channel,
      message,
    });
  }

  recordPrenatalExam(
    cycleId: string,
    data: {
      examDate: string;
      gestationalSacs?: number;
      fetalHeartbeats?: number;
      dueDate?: string;
      ultrasoundFindings?: string;
      notes?: string;
    },
  ): Observable<PregnancyDto> {
    return this.http.post<PregnancyDto>(`${this.baseUrl}/cycle/${cycleId}/prenatal-exam`, data);
  }

  discharge(
    cycleId: string,
    outcomeNote: string,
    dischargeDate: string,
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/cycle/${cycleId}/discharge`, {
      outcomeNote,
      dischargeDate,
    });
  }
}
