import { Injectable } from '@angular/core';
import { PatientService } from '../../../core/services/patient.service';
import { QueueService } from '../../../core/services/queue.service';
import { Patient, TicketStatus } from '../../../core/models/api.models';
import { Observable, of } from 'rxjs';

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

    constructor(private patientService: PatientService, private queueService: QueueService) { }

    searchPatients(term: string): Observable<{ items: Patient[] }> {
        return this.patientService.searchPatients(term);
    }

    issueTicket(patientId: string, departmentCode: string, priority: 'Normal' | 'VIP' | 'Emergency', notes: string, cycleId?: string, serviceIds?: string[]): Observable<any> {
        return this.queueService.issueTicket(
            patientId,
            departmentCode,
            priority,
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
