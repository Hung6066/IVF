import { Injectable, signal } from '@angular/core';
import { QueueService } from '../../../core/services/queue.service';
import { EmbryoService } from '../../../core/services/embryo.service';
import { Observable, of } from 'rxjs';
import { map, delay } from 'rxjs/operators';
import { EmbryoCard, ScheduleItem, CryoLocation, QueueItem, LabStats } from './lab-dashboard.models';

@Injectable({
    providedIn: 'root'
})
export class LabService {
    constructor(private queueService: QueueService, private embryoService: EmbryoService) { }

    getQueue(): Observable<QueueItem[]> {
        return this.queueService.getQueueByDept('LAB').pipe(
            map((data: any[]) => data.map((item, index) => ({
                id: item.id || String(index),
                number: item.ticketNumber,
                patientName: item.patientName,
                patientCode: item.patientCode,
                issueTime: item.issueTime || new Date().toISOString(),
                status: item.status || 'Waiting'
            })))
        );
    }

    callPatient(id: string): Observable<any> {
        return this.queueService.callTicket(id);
    }

    getEmbryos(): Observable<EmbryoCard[]> {
        return this.embryoService.getActiveEmbryos().pipe(
            map((data: any[]) => data.map(e => ({
                id: e.id,
                cycleCode: e.cycleCode,
                patientName: e.patientName,
                embryoNumber: e.number,
                grade: e.grade,
                day: e.day,
                status: e.status
            })))
        );
    }

    getSchedule(date: Date): Observable<ScheduleItem[]> {
        // Currently no specific Schedule API, using mock for demo of "Planned" vs "Queue"
        // In production, this would query api/schedule?date=...
        const mockSchedule: ScheduleItem[] = [
            { id: '1', time: '08:00', patientName: 'Phạm T.B', cycleCode: 'CK-010', procedure: 'Chọc hút', type: 'retrieval', status: 'pending' },
            { id: '2', time: '08:30', patientName: 'Hoàng T.C', cycleCode: 'CK-011', procedure: 'Chọc hút', type: 'retrieval', status: 'pending' },
            { id: '3', time: '09:30', patientName: 'Nguyễn T.H', cycleCode: 'CK-001', procedure: 'CP D5 - 2 phôi', type: 'transfer', status: 'pending' },
            { id: '4', time: '10:00', patientName: 'Trần M.L', cycleCode: 'CK-002', procedure: 'Báo phôi D3', type: 'report', status: 'done' }
        ];
        return of(mockSchedule).pipe(delay(300));
    }

    getCryoLocations(): Observable<CryoLocation[]> {
        return this.embryoService.getCryoStats().pipe(
            map((data: any[]) => data.map(s => ({
                tank: s.tank,
                canister: s.canisterCount,
                cane: s.caneCount,
                goblet: s.gobletCount,
                available: s.available,
                used: s.used
            })))
        );
    }

    getStats(): Observable<LabStats> {
        // Placeholder for real lab stats API
        const stats: LabStats = {
            eggRetrievalCount: 3,
            cultureCount: 12,
            transferCount: 2,
            freezeCount: 5,
            totalFrozenEmbryos: 342,
            totalFrozenEggs: 128,
            totalFrozenSperm: 256
        };
        return of(stats).pipe(delay(300));
    }

    toggleScheduleStatus(item: ScheduleItem): Observable<any> {
        // Simulate API update
        return of(true).pipe(delay(200));
    }

    addCryoLocation(location: CryoLocation): Observable<any> {
        // Implement real create if needed, currently API only has stats
        return of(location).pipe(delay(200));
    }
}
