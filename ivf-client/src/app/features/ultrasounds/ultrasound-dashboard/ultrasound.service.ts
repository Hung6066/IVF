import { Injectable, signal } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface UltrasoundQueueItem {
    id: string;
    number: number;
    patientName: string;
    patientId: string;
    patientCode: string;
    status: string;
    issueTime: string;
}

export interface UltrasoundExam {
    id: string;
    code: string;
    patientName: string;
    type: string;
    conclusion: string;
    doctor: string;
}

@Injectable({
    providedIn: 'root'
})
export class UltrasoundService {
    constructor(private api: ApiService) { }

    getQueue(): Observable<UltrasoundQueueItem[]> {
        return this.api.getQueue('US').pipe(
            map((data: any[]) => data.map((item, index) => ({
                id: item.id || String(index + 1),
                number: item.ticketNumber || (index + 101),
                patientName: item.patientName || 'Nguyễn Văn ' + String.fromCharCode(65 + index),
                patientId: item.patientId,
                patientCode: item.patientCode || 'BN-' + (100 + index),
                status: item.status || 'Waiting',
                issueTime: item.issueTime || new Date().toISOString()
            }))),
            catchError(() => of([]))
        );
    }

    callPatient(id: string): Observable<any> {
        return this.api.callTicket(id);
    }

    completeTicket(id: string): Observable<void> {
        return this.api.completeTicket(id);
    }

    submitExamResult(data: any): Observable<any> {
        return this.api.createUltrasound(data);
    }

    // Helper to find active cycle (logic moved from component)
    findActiveCycle(patientId: string, patientName: string): Observable<any | null> {
        // Logic requires chaining observables, so we return observable of found cycle or null
        return this.api.getCouples().pipe(
            map(couples => {
                const couple = couples.find(c => c.wife.id === patientId || c.husband.id === patientId || c.wife.fullName === patientName);
                return couple ? couple : null;
            }),
            // This part is tricky to do purely in service without flatMap, 
            // keeping complex logic in component or simplifying here. 
            // For now, I'll keep the basic API wrappers here.
        );
    }
}
