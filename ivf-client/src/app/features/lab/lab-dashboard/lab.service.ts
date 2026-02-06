import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { QueueService } from '../../../core/services/queue.service';
import { EmbryoService } from '../../../core/services/embryo.service';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { EmbryoCard, ScheduleItem, CryoLocation, QueueItem, LabStats } from './lab-dashboard.models';
import { environment } from '../../../../environments/environment';

@Injectable({
    providedIn: 'root'
})
export class LabService {
    private apiUrl = `${environment.apiUrl}/api/lab`;

    constructor(
        private http: HttpClient,
        private queueService: QueueService,
        private embryoService: EmbryoService
    ) { }

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
                status: e.status,
                location: e.location // assuming backend sends this
            })))
        );
    }

    getSchedule(date: Date): Observable<ScheduleItem[]> {
        const dateStr = date.toISOString().split('T')[0];
        return this.http.get<ScheduleItem[]>(`${this.apiUrl}/schedule?date=${dateStr}`);
    }

    getCryoLocations(): Observable<CryoLocation[]> {
        return this.http.get<CryoLocation[]>(`${this.apiUrl}/cryo-locations`);
    }

    getStats(): Observable<LabStats> {
        return this.http.get<LabStats>(`${this.apiUrl}/stats`);
    }

    toggleScheduleStatus(item: ScheduleItem): Observable<any> {
        return this.http.post(`${this.apiUrl}/schedule/${item.id}/toggle`, {});
    }

    addCryoLocation(location: CryoLocation): Observable<any> {
        return this.http.post(`${this.apiUrl}/cryo-locations`, location);
    }

    deleteCryoLocation(tank: string): Observable<any> {
        return this.http.delete(`${this.apiUrl}/cryo-locations/${tank}`);
    }
}
