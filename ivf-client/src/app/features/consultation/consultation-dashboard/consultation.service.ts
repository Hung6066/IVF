import { Injectable } from '@angular/core';
import { QueueService } from '../../../core/services/queue.service';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { QueueTicket } from '../../../core/models/api.models';

@Injectable({
    providedIn: 'root'
})
export class ConsultationService {

    constructor(private queueService: QueueService) { }

    getQueue(): Observable<QueueTicket[]> {
        return this.queueService.getQueueByDept('TV'); // TV = Tu Van
    }

    callPatient(ticketId: string): Observable<any> {
        return this.queueService.callTicket(ticketId);
    }

    completeTicket(ticketId: string, notes?: string): Observable<any> {
        return this.queueService.completeTicket(ticketId, notes);
    }

    getHistory(): Observable<any[]> {
        // Fetch completed tickets for this department from history endpoint
        return this.queueService.getQueueHistory('TV');
    }

    startService(ticketId: string): Observable<any> {
        return this.queueService.startService(ticketId);
    }

    skipTicket(ticketId: string): Observable<any> {
        return this.queueService.skipTicket(ticketId);
    }
}
