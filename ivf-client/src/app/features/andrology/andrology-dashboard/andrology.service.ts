import { Injectable, signal } from '@angular/core';
import { QueueService } from '../../../core/services/queue.service';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface AndrologyQueueItem {
    id: string;
    number: number;
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
    concentration?: number;
    progressiveMotility?: number;
    nonProgressiveMotility?: number;
    immotile?: number;
    morphology?: number;
    vitality?: number;
    status: string;
}

export interface SpermWashing {
    id: string;
    cycleCode: string;
    patientName: string;
    method: string;
    prewashConc?: number;
    postwashConc?: number;
    postwashMotility?: number;
    washDate: string;
    status: string;
}

@Injectable({
    providedIn: 'root'
})
export class AndrologyService {
    constructor(private queueService: QueueService) { }

    getQueue(): Observable<AndrologyQueueItem[]> {
        return this.queueService.getQueueByDept('NAM').pipe(
            map((data: any[]) => data.map((item, index) => ({
                id: item.id || String(index),
                number: item.ticketNumber,
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

    // Mock API calls for Andrology specific data until real endpoints exist
    getAnalyses(): Observable<SemenAnalysis[]> {
        return of([
            { id: '1', patientCode: 'BN-2024-001', patientName: 'Nguyễn Văn A', analysisDate: new Date().toISOString(), volume: 3.2, concentration: 45, progressiveMotility: 42, nonProgressiveMotility: 18, immotile: 40, morphology: 8, vitality: 75, status: 'Completed' },
            { id: '2', patientCode: 'BN-2024-002', patientName: 'Trần Văn B', analysisDate: new Date().toISOString(), volume: 2.5, concentration: 12, progressiveMotility: 25, nonProgressiveMotility: 15, immotile: 60, morphology: 3, vitality: 55, status: 'Completed' },
            { id: '3', patientCode: 'BN-2024-003', patientName: 'Lê Văn C', analysisDate: new Date().toISOString(), status: 'Pending' }
        ]);
    }

    getWashings(): Observable<SpermWashing[]> {
        return of([
            { id: '1', cycleCode: 'CK-001', patientName: 'Nguyễn Văn A', method: 'Gradient', prewashConc: 45, postwashConc: 85, postwashMotility: 92, washDate: new Date().toISOString(), status: 'Completed' },
            { id: '2', cycleCode: 'CK-002', patientName: 'Trần Văn B', method: 'Swim-up', prewashConc: 12, postwashConc: 28, postwashMotility: 85, washDate: new Date().toISOString(), status: 'Completed' }
        ]);
    }

    createWashing(data: any): Observable<any> {
        // Mock creation
        return of({ ...data, id: Date.now().toString(), washDate: new Date().toISOString(), status: 'Completed' });
    }

    createAnalysis(data: any): Observable<any> {
        return of({ ...data, id: Date.now().toString(), status: 'Completed' });
    }
}
