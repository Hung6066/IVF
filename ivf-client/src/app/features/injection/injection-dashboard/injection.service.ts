import { Injectable } from '@angular/core';
import { QueueService } from '../../../core/services/queue.service';
import { Observable } from 'rxjs';
import { QueueTicket } from '../../../core/models/api.models';

@Injectable({
    providedIn: 'root'
})
export class InjectionService {

    constructor(private queueService: QueueService) { }

    getQueue(): Observable<QueueTicket[]> {
        return this.queueService.getQueueByDept('TM'); // TM = Tiem
    }

    callPatient(ticketId: string): Observable<any> {
        return this.queueService.callTicket(ticketId);
    }

    getHistory(): Observable<any[]> {
        return this.queueService.getQueueHistory('TM');
    }

    completeTicket(ticketId: string, notes?: string): Observable<any> {
        return this.queueService.completeTicket(ticketId, notes);
    }

    startService(ticketId: string): Observable<any> {
        return this.queueService.startService(ticketId);
    }

    skipTicket(ticketId: string): Observable<any> {
        return this.queueService.skipTicket(ticketId);
    }
}
