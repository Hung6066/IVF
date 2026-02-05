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

    completeTicket(ticketId: string): Observable<any> {
        return this.queueService.completeTicket(ticketId);
    }
}
