import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePrescriptionRequest,
  PrescriptionDto,
  PrescriptionSearchResult,
  PrescriptionStatistics,
} from '../models/prescription.models';

@Injectable({ providedIn: 'root' })
export class PrescriptionService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getById(id: string): Observable<PrescriptionDto> {
    return this.http.get<PrescriptionDto>(`${this.baseUrl}/prescriptions/${id}`);
  }

  getByPatient(patientId: string): Observable<PrescriptionDto[]> {
    return this.http.get<PrescriptionDto[]>(`${this.baseUrl}/prescriptions/patient/${patientId}`);
  }

  getByCycle(cycleId: string): Observable<PrescriptionDto[]> {
    return this.http.get<PrescriptionDto[]>(`${this.baseUrl}/prescriptions/cycle/${cycleId}`);
  }

  search(
    query?: string,
    status?: string,
    from?: string,
    to?: string,
    page = 1,
    pageSize = 20,
  ): Observable<PrescriptionSearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (status) params = params.set('status', status);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<PrescriptionSearchResult>(`${this.baseUrl}/prescriptions`, { params });
  }

  getStatistics(): Observable<PrescriptionStatistics> {
    return this.http.get<PrescriptionStatistics>(`${this.baseUrl}/prescriptions/statistics`);
  }

  create(request: CreatePrescriptionRequest): Observable<PrescriptionDto> {
    return this.http.post<PrescriptionDto>(`${this.baseUrl}/prescriptions`, request);
  }

  addItem(
    prescriptionId: string,
    item: {
      drugName: string;
      quantity: number;
      drugCode?: string;
      dosage?: string;
      frequency?: string;
      duration?: string;
    },
  ): Observable<PrescriptionDto> {
    return this.http.post<PrescriptionDto>(
      `${this.baseUrl}/prescriptions/${prescriptionId}/items`,
      item,
    );
  }

  enter(prescriptionId: string, enteredByUserId: string): Observable<PrescriptionDto> {
    return this.http.put<PrescriptionDto>(`${this.baseUrl}/prescriptions/${prescriptionId}/enter`, {
      enteredByUserId,
    });
  }

  print(prescriptionId: string): Observable<PrescriptionDto> {
    return this.http.put<PrescriptionDto>(
      `${this.baseUrl}/prescriptions/${prescriptionId}/print`,
      {},
    );
  }

  dispense(prescriptionId: string, dispensedByUserId: string): Observable<PrescriptionDto> {
    return this.http.put<PrescriptionDto>(
      `${this.baseUrl}/prescriptions/${prescriptionId}/dispense`,
      {
        dispensedByUserId,
      },
    );
  }

  cancel(prescriptionId: string): Observable<PrescriptionDto> {
    return this.http.put<PrescriptionDto>(
      `${this.baseUrl}/prescriptions/${prescriptionId}/cancel`,
      {},
    );
  }

  updateNotes(prescriptionId: string, notes?: string): Observable<PrescriptionDto> {
    return this.http.put<PrescriptionDto>(`${this.baseUrl}/prescriptions/${prescriptionId}/notes`, {
      notes,
    });
  }
}
