import { Injectable, signal, inject } from '@angular/core';
import { QueueService } from '../../../core/services/queue.service';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { HttpClient, HttpParams } from '@angular/common/http';

export interface AndrologyQueueItem {
    id: string;
    number: number;
    patientId: string;
    patientName: string;
    patientCode: string;
    status: string;
    issueTime: string;
}

export interface SemenAnalysis {
    id: string;
    patientName: string;
    patientCode: string;
    analysisDate: string;
    volume?: number;
    appearance?: string;
    liquefaction?: string;
    ph?: number;
    concentration?: number;
    totalCount?: number;
    progressiveMotility?: number;
    nonProgressiveMotility?: number;
    immotile?: number;
    normalMorphology?: number;
    vitality?: number;
    status: string;
    cycleId?: string;
    cycleCode?: string;
    notes?: string;
}

export interface SpermWashing {
    id: string;
    cycleCode: string;
    patientName: string;
    method: string;
    preWashConcentration?: number;
    postWashConcentration?: number;
    postWashMotility?: number;
    washDate: string;
    status: string;
    notes?: string;
}

import { PatientService } from '../../../core/services/patient.service';
import { CycleService } from '../../../core/services/cycle.service';
import { Patient } from '../../../core/models/patient.models';
import { TreatmentCycle } from '../../../core/models/cycle.models';
import { environment } from '../../../../environments/environment';

@Injectable({
    providedIn: 'root'
})
export class AndrologyService {
    private patientService = inject(PatientService);
    private cycleService = inject(CycleService);

    constructor(private queueService: QueueService) { }

    getPatients(): Observable<Patient[]> {
        return this.patientService.searchPatients('', 1, 100).pipe(
            map(res => res.items), // Assuming response has items array
            catchError(() => of([]))
        );
    }

    getCycles(): Observable<TreatmentCycle[]> {
        return this.cycleService.searchCycles().pipe(
            catchError(() => of([]))
        );
    }

    getQueue(): Observable<AndrologyQueueItem[]> {
        return this.queueService.getQueueByDept('NAM').pipe(
            map((data: any[]) => data.map((item, index) => ({
                id: item.id || String(index),
                number: item.ticketNumber,
                patientId: item.patientId,
                patientName: item.patientName,
                patientCode: item.patientCode,
                status: item.status || 'Waiting',
                issueTime: item.issueTime || new Date().toISOString()
            }))),
            catchError(() => of([]))
        );
    }

    callPatient(id: string): Observable<any> {
        return this.queueService.callTicket(id);
    }

    startService(id: string): Observable<any> {
        return this.queueService.startService(id);
    }

    completeTicket(id: string): Observable<any> {
        return this.queueService.completeTicket(id);
    }

    skipTicket(id: string): Observable<any> {
        return this.queueService.skipTicket(id);
    }

    private baseUrl = environment.apiUrl + '/andrology';
    private http = inject(HttpClient);

    // ... (queue methods remain same)

    getAnalyses(query?: string, fromDate?: string, toDate?: string, status?: string): Observable<SemenAnalysis[]> {
        let params = new HttpParams();
        if (query) params = params.set('q', query);
        if (fromDate) params = params.set('from', fromDate);
        if (toDate) params = params.set('to', toDate);
        if (status) params = params.set('status', status);

        return this.http.get<{ items: SemenAnalysis[], total: number }>(`${this.baseUrl}/analyses`, { params }).pipe(
            map(res => res.items),
            catchError(() => of([]))
        );
    }

    getWashings(method?: string, fromDate?: string, toDate?: string): Observable<SpermWashing[]> {
        let params = new HttpParams();
        if (method) params = params.set('method', method);
        if (fromDate) params = params.set('from', fromDate);
        if (toDate) params = params.set('to', toDate);

        return this.http.get<{ items: SpermWashing[], total: number }>(`${this.baseUrl}/washings`, { params }).pipe(
            map(res => res.items),
            catchError(() => of([]))
        );
    }

    getStatistics(): Observable<any> {
        return this.http.get(`${this.baseUrl}/statistics`).pipe(
            catchError(() => of({ todayAnalyses: 0, todayWashings: 0, pendingAnalyses: 0, avgConcentration: 0 }))
        );
    }

    createWashing(data: any): Observable<any> {
        return this.http.post(`${this.baseUrl}/washings`, data);
    }

    updateWashing(id: string, data: any): Observable<any> {
        return this.http.put(`${this.baseUrl}/washings/${id}`, data);
    }

    createAnalysis(data: any): Observable<any> {
        return this.http.post(`${this.baseUrl}`, data);
    }

    updateAnalysisMacroscopic(id: string, data: any): Observable<any> {
        return this.http.put(`${this.baseUrl}/${id}/macroscopic`, data);
    }

    updateAnalysisMicroscopic(id: string, data: any): Observable<any> {
        return this.http.put(`${this.baseUrl}/${id}/microscopic`, data);
    }
}
