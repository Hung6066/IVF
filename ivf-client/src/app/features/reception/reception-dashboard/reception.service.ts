import { Injectable } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { Observable, of } from 'rxjs';
import { Patient, TicketStatus } from '../../../core/models/api.models';

export interface CheckinRecord {
    id: string;
    time: string;
    patientName: string;
    department: string;
}

export interface QueueStat {
    department: string;
    count: number;
}

@Injectable({
    providedIn: 'root'
})
export class ReceptionService {

    constructor(private api: ApiService) { }

    searchPatients(term: string): Observable<{ items: Patient[] }> {
        return this.api.searchPatients(term);
    }

    issueTicket(patientId: string, departmentCode: string, priority: string, notes: string, cycleId?: string, serviceIds?: string[]): Observable<any> {
        return this.api.issueTicket(
            patientId,
            departmentCode,
            priority as any,
            notes,
            cycleId,
            serviceIds
        );
    }

    // Mock for now until API endpoint exists
    getRecentCheckins(): Observable<CheckinRecord[]> {
        return of([
            { id: '1', time: new Date().toISOString(), patientName: 'Nguyễn Thị A', department: 'Tư vấn' },
            { id: '2', time: new Date().toISOString(), patientName: 'Lê Văn B', department: 'Siêu âm' },
            { id: '3', time: new Date().toISOString(), patientName: 'Trần Thị C', department: 'Xét nghiệm' }
        ]);
    }
}
