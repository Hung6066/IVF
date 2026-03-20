import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MedicationAdministrationDto } from '../models/clinical-management.models';

export interface RecordMedicationAdminRequest {
  patientId: string;
  cycleId?: string;
  medicationName: string;
  dosage: string;
  route: string;
  administeredAt: string;
  administeredByUserId: string;
  isTriggerShot: boolean;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class MedicationAdminService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getByCycle(cycleId: string): Observable<MedicationAdministrationDto[]> {
    return this.http.get<MedicationAdministrationDto[]>(
      `${this.baseUrl}/medication-admin/cycle/${cycleId}`,
    );
  }

  getByPatient(patientId: string): Observable<MedicationAdministrationDto[]> {
    return this.http.get<MedicationAdministrationDto[]>(
      `${this.baseUrl}/medication-admin/patient/${patientId}`,
    );
  }

  getById(id: string): Observable<MedicationAdministrationDto> {
    return this.http.get<MedicationAdministrationDto>(`${this.baseUrl}/medication-admin/${id}`);
  }

  record(request: RecordMedicationAdminRequest): Observable<MedicationAdministrationDto> {
    return this.http.post<MedicationAdministrationDto>(`${this.baseUrl}/medication-admin`, request);
  }

  markSkipped(id: string, reason?: string): Observable<MedicationAdministrationDto> {
    return this.http.put<MedicationAdministrationDto>(
      `${this.baseUrl}/medication-admin/${id}/skip`,
      { reason },
    );
  }

  markRefused(id: string, reason?: string): Observable<MedicationAdministrationDto> {
    return this.http.put<MedicationAdministrationDto>(
      `${this.baseUrl}/medication-admin/${id}/refuse`,
      { reason },
    );
  }
}
