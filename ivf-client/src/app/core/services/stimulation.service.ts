import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface StimulationTrackerDto {
  cycleId: string;
  lastMenstruation?: string;
  startDate?: string;
  startDay?: number;
  follicleScans: FollicleScanDto[];
  currentEndometriumThickness?: number;
  currentFolliclesReady?: number;
  triggerDrug?: string;
  triggerDate?: string;
  triggerTime?: string;
  triggerGiven: boolean;
  lhLab?: number;
  e2Lab?: number;
  p4Lab?: number;
  procedureType?: string;
  aspirationDate?: string;
}

export interface FollicleScanDto {
  id: string;
  cycleId: string;
  scanDate: string;
  cycleDay: number;
  size12Follicle?: number;
  size14Follicle?: number;
  totalFollicles?: number;
  endometriumThickness?: number;
  endometriumPattern?: string;
  e2?: number;
  lh?: number;
  p4?: number;
  notes?: string;
  scanType: string;
  createdAt: string;
}

export interface FollicleChartPointDto {
  date: string;
  cycleDay: number;
  size12?: number;
  size14?: number;
  total?: number;
  endometrium?: number;
  e2?: number;
}

export interface MedicationScheduleItemDto {
  drugName: string;
  posology?: string;
  duration: number;
  sortOrder: number;
}

@Injectable({ providedIn: 'root' })
export class StimulationService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/stimulation`;

  getTracker(cycleId: string): Observable<StimulationTrackerDto> {
    return this.http.get<StimulationTrackerDto>(`${this.baseUrl}/cycle/${cycleId}`);
  }

  getFollicleChart(cycleId: string): Observable<FollicleChartPointDto[]> {
    return this.http.get<FollicleChartPointDto[]>(`${this.baseUrl}/cycle/${cycleId}/chart`);
  }

  getMedicationSchedule(cycleId: string): Observable<MedicationScheduleItemDto[]> {
    return this.http.get<MedicationScheduleItemDto[]>(
      `${this.baseUrl}/cycle/${cycleId}/medications`,
    );
  }

  recordFollicleScan(
    cycleId: string,
    data: {
      scanDate: string;
      cycleDay: number;
      size12Follicle?: number;
      size14Follicle?: number;
      totalFollicles?: number;
      endometriumThickness?: number;
      endometriumPattern?: string;
      e2?: number;
      lh?: number;
      p4?: number;
      notes?: string;
      scanType?: string;
    },
  ): Observable<StimulationTrackerDto> {
    return this.http.post<StimulationTrackerDto>(`${this.baseUrl}/cycle/${cycleId}/scan`, data);
  }

  recordTriggerShot(
    cycleId: string,
    data: {
      triggerDrug: string;
      triggerDate: string;
      triggerTime: string;
      triggerDrug2?: string;
      triggerDate2?: string;
      triggerTime2?: string;
      lhLab?: number;
      e2Lab?: number;
      p4Lab?: number;
    },
  ): Observable<StimulationTrackerDto> {
    return this.http.post<StimulationTrackerDto>(`${this.baseUrl}/cycle/${cycleId}/trigger`, data);
  }

  evaluateReadiness(
    cycleId: string,
    decision: string,
    reason?: string,
  ): Observable<{ decision: string }> {
    return this.http.post<{ decision: string }>(`${this.baseUrl}/cycle/${cycleId}/evaluate`, {
      decision,
      reason,
    });
  }
}
