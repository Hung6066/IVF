import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

export interface Donor {
    id: string;
    code: string;
    bloodType: string;
    age: number | null;
    samples: number;
    status: string;
}

export interface Sample {
    id: string;
    code: string;
    donor: string;
    vials: number;
    status: string;
}

export interface Match {
    id: string;
    recipient: string;
    donor: string;
    date: string;
    status: string;
}

@Injectable({
    providedIn: 'root'
})
export class SpermBankService {

    constructor() { }

    getDonors(): Observable<Donor[]> {
        return of([
            { id: '1', code: 'NH-001', bloodType: 'O+', age: 28, samples: 5, status: 'Active' },
            { id: '2', code: 'NH-002', bloodType: 'A+', age: 32, samples: 0, status: 'Screening' }
        ]);
    }

    getSamples(): Observable<Sample[]> {
        return of([
            { id: '1', code: 'SP-001-A', donor: 'NH-001', vials: 3, status: 'Available' },
            { id: '2', code: 'SP-001-B', donor: 'NH-001', vials: 2, status: 'Quarantine' }
        ]);
    }

    getMatches(): Observable<Match[]> {
        return of([
            { id: '1', recipient: 'Nguyễn T.H', donor: 'NH-001', date: '01/02/2024', status: 'Confirmed' }
        ]);
    }
}
