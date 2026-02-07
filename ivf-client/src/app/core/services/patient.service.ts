import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Patient, PatientListResponse } from '../models/patient.models';

@Injectable({ providedIn: 'root' })
export class PatientService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    searchPatients(query?: string, page = 1, pageSize = 20, gender?: string): Observable<PatientListResponse> {
        let params = new HttpParams()
            .set('page', page)
            .set('pageSize', pageSize);

        if (query) {
            params = params.set('q', query);
        }

        if (gender) {
            params = params.set('gender', gender);
        }

        return this.http.get<PatientListResponse>(`${this.baseUrl}/patients`, { params });
    }

    getPatient(id: string): Observable<Patient> {
        return this.http.get<Patient>(`${this.baseUrl}/patients/${id}`);
    }

    createPatient(patient: Partial<Patient>): Observable<Patient> {
        return this.http.post<Patient>(`${this.baseUrl}/patients`, patient);
    }

    updatePatient(id: string, patient: Partial<Patient>): Observable<Patient> {
        return this.http.put<Patient>(`${this.baseUrl}/patients/${id}`, patient);
    }

    deletePatient(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/patients/${id}`);
    }
}
