import { Injectable } from '@angular/core';
import { QueueService } from '../../../core/services/queue.service';
import { Observable } from 'rxjs';
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

    completeTicket(ticketId: string): Observable<any> {
        return this.queueService.completeTicket(ticketId);
    }
}
