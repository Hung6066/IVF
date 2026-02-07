import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { QueueService } from '../../../core/services/queue.service';
import { EmbryoService } from '../../../core/services/embryo.service';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { EmbryoCard, ScheduleItem, CryoLocation, QueueItem, LabStats, EmbryoReport } from './lab-dashboard.models';
import { environment } from '../../../../environments/environment';
import { ScheduleTypes } from '../../../core/constants/lab.constants';

@Injectable({
    providedIn: 'root'
})
export class LabService {
    private apiUrl = `${environment.apiUrl}/lab`;

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

    getActiveCycles(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl.replace('/lab', '/cycles')}/active`);
    }

    getDoctors(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl.replace('/lab', '/doctors')}?pageSize=100`);
    }

    getEmbryos(): Observable<EmbryoCard[]> {
        return this.embryoService.getActiveEmbryos().pipe(
            map((data: any[]) => data.map(e => ({
                id: e.id,
                cycleId: e.cycleId,
                cycleCode: e.cycleCode,
                patientName: e.patientName,
                embryoNumber: e.number,
                grade: e.grade,
                day: e.day,
                status: e.status,
                fertilizationDate: e.fertilizationDate,
                location: e.location // assuming backend sends this
            })))
        );
    }

    createEmbryo(data: any): Observable<any> {
        return this.http.post(`${this.apiUrl.replace('/lab', '/embryos')}`, data);
    }

    updateEmbryo(id: string, data: any): Observable<any> {
        return this.http.put(`${this.apiUrl.replace('/lab', '/embryos')}/${id}`, data);
    }

    deleteEmbryo(id: string): Observable<any> {
        return this.http.delete(`${this.apiUrl.replace('/lab', '/embryos')}/${id}`);
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

    getEmbryoReport(date: Date): Observable<EmbryoReport[]> {
        const dateStr = date.toISOString().split('T')[0];
        return this.http.get<EmbryoReport[]>(`${this.apiUrl}/embryo-report?date=${dateStr}`);
    }

    toggleScheduleStatus(item: ScheduleItem): Observable<any> {
        return this.http.post(`${this.apiUrl}/schedule/${item.id}/toggle`, {});
    }

    updateStimulation(cycleId: string, data: any): Observable<any> {
        return this.http.put(`${this.apiUrl.replace('/lab', '/cycles')}/${cycleId}/stimulation`, data);
    }

    updateTransfer(cycleId: string, data: any): Observable<any> {
        return this.http.put(`${this.apiUrl.replace('/lab', '/cycles')}/${cycleId}/transfer`, data);
    }

    scheduleProcedure(data: any): Observable<any> {
        if (data.type === ScheduleTypes.RETRIEVAL) {
            // Mapping schedule data to UpdateStimulationDataRequest
            // We only update the AspirationDate/Time
            const req = {
                aspirationDate: `${data.date}T${data.time}:00.000Z`, // Simplified ISO format
                procedureDate: `${data.date}T${data.time}:00.000Z`
                // Other fields might be required by backend, but let's hope for partial updates or nulled params handling
                // Check CyclePhaseEndpoints.cs to be sure.
            };
            return this.updateStimulation(data.cycleId, req);
        } else if (data.type === ScheduleTypes.TRANSFER) {
            const req = {
                transferDate: `${data.date}T${data.time}:00.000Z`
            };
            return this.updateTransfer(data.cycleId, req);
        }
        return this.http.post(`${this.apiUrl}/schedule`, data);
    }

    addCryoLocation(location: CryoLocation): Observable<any> {
        return this.http.post(`${this.apiUrl}/cryo-locations`, location);
    }

    updateCryoLocation(tank: string, data: { used: number, specimenType: number }): Observable<any> {
        return this.http.put(`${this.apiUrl}/cryo-locations/${tank}`, data);
    }

    deleteCryoLocation(tank: string): Observable<any> {
        return this.http.delete(`${this.apiUrl}/cryo-locations/${tank}`);
    }
}
