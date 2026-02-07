import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { QueueTicket } from '../models/queue.models';

@Injectable({ providedIn: 'root' })
export class QueueService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getQueueByDept(deptCode: string): Observable<QueueTicket[]> {
        return this.http.get<QueueTicket[]>(`${this.baseUrl}/queue/${deptCode}`);
    }

    getQueueHistory(deptCode: string): Observable<QueueTicket[]> {
        return this.http.get<QueueTicket[]>(`${this.baseUrl}/queue/${deptCode}/history`);
    }

    issueTicket(patientId: string, departmentCode: string, priority: 'Normal' | 'VIP' | 'Emergency' = 'Normal', notes?: string, cycleId?: string, serviceIds?: string[]): Observable<QueueTicket> {
        return this.http.post<QueueTicket>(`${this.baseUrl}/queue/issue`, {
            patientId,
            departmentCode,
            priority,
            notes,
            cycleId,
            serviceIds
        });
    }

    callTicket(ticketId: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/queue/${ticketId}/call`, {});
    }

    startService(ticketId: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/queue/${ticketId}/start`, {});
    }

    completeTicket(ticketId: string, notes?: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/queue/${ticketId}/complete`, { notes });
    }

    skipTicket(ticketId: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/queue/${ticketId}/skip`, {});
    }

    getPatientPendingTicket(patientId: string): Observable<QueueTicket> {
        return this.http.get<QueueTicket>(`${this.baseUrl}/queue/patient/${patientId}/pending`);
    }
}
