import { Injectable } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { Observable } from 'rxjs';
import { QueueTicket } from '../../../core/models/api.models';

@Injectable({
    providedIn: 'root'
})
export class InjectionService {

    constructor(private api: ApiService) { }

    getQueue(): Observable<QueueTicket[]> {
        return this.api.getQueue('TM'); // TM = Tiem
    }

    callPatient(ticketId: string): Observable<any> {
        return this.api.callTicket(ticketId);
    }

    completeTicket(ticketId: string): Observable<any> {
        return this.api.completeTicket(ticketId);
    }
}
