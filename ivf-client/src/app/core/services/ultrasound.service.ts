import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Ultrasound } from '../models/medical.models';

@Injectable({ providedIn: 'root' })
export class UltrasoundService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getUltrasoundsByCycle(cycleId: string): Observable<Ultrasound[]> {
        return this.http.get<Ultrasound[]>(`${this.baseUrl}/ultrasounds/cycle/${cycleId}`);
    }

    createUltrasound(data: Partial<Ultrasound>): Observable<Ultrasound> {
        return this.http.post<Ultrasound>(`${this.baseUrl}/ultrasounds`, data);
    }
}
