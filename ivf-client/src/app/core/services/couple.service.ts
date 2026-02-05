import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Couple } from '../models/couple.models';

@Injectable({ providedIn: 'root' })
export class CoupleService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getCouple(id: string): Observable<Couple> {
        return this.http.get<Couple>(`${this.baseUrl}/couples/${id}`);
    }

    getCoupleByPatient(patientId: string): Observable<Couple> {
        return this.http.get<Couple>(`${this.baseUrl}/couples/by-patient/${patientId}`);
    }

    createCouple(couple: any): Observable<string> {
        return this.http.post<string>(`${this.baseUrl}/couples`, couple);
    }

    getCouples(): Observable<Couple[]> {
        return this.http.get<Couple[]>(`${this.baseUrl}/couples`);
    }
}
