import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Patient,
  PatientListResponse,
  PatientAdvancedSearchParams,
  UpdateDemographicsRequest,
  UpdateEmergencyContactRequest,
  UpdateConsentRequest,
  SetRiskRequest,
  PatientAnalytics,
  PatientAuditTrail,
  PatientStatus,
} from '../models/patient.models';

@Injectable({ providedIn: 'root' })
export class PatientService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ==================== CORE CRUD ====================
  searchPatients(
    query?: string,
    page = 1,
    pageSize = 20,
    gender?: string,
  ): Observable<PatientListResponse> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);

    if (query) params = params.set('q', query);
    if (gender) params = params.set('gender', gender);

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

  // ==================== ADVANCED SEARCH ====================
  advancedSearch(params: PatientAdvancedSearchParams): Observable<PatientListResponse> {
    let httpParams = new HttpParams();
    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.gender) httpParams = httpParams.set('gender', params.gender);
    if (params.patientType) httpParams = httpParams.set('patientType', params.patientType);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.priority) httpParams = httpParams.set('priority', params.priority);
    if (params.riskLevel) httpParams = httpParams.set('riskLevel', params.riskLevel);
    if (params.bloodType) httpParams = httpParams.set('bloodType', params.bloodType);
    if (params.dobFrom) httpParams = httpParams.set('dobFrom', params.dobFrom);
    if (params.dobTo) httpParams = httpParams.set('dobTo', params.dobTo);
    if (params.createdFrom) httpParams = httpParams.set('createdFrom', params.createdFrom);
    if (params.createdTo) httpParams = httpParams.set('createdTo', params.createdTo);
    if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDesc !== undefined) httpParams = httpParams.set('sortDesc', params.sortDesc);
    httpParams = httpParams.set('page', params.page ?? 1);
    httpParams = httpParams.set('pageSize', params.pageSize ?? 20);

    return this.http.get<PatientListResponse>(`${this.baseUrl}/patients/search/advanced`, {
      params: httpParams,
    });
  }

  // ==================== DEMOGRAPHICS & CONTACTS ====================
  updateDemographics(request: UpdateDemographicsRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/patients/${request.id}/demographics`, request);
  }

  updateEmergencyContact(
    patientId: string,
    request: UpdateEmergencyContactRequest,
  ): Observable<Patient> {
    return this.http.put<Patient>(
      `${this.baseUrl}/patients/${patientId}/emergency-contact`,
      request,
    );
  }

  updateMedicalNotes(patientId: string, medicalNotes: string | null): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/patients/${patientId}/medical-notes`, {
      medicalNotes,
    });
  }

  // ==================== CONSENT & COMPLIANCE ====================
  updateConsent(patientId: string, request: UpdateConsentRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/patients/${patientId}/consent`, request);
  }

  anonymizePatient(patientId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/patients/${patientId}/anonymize`, {});
  }

  // ==================== RISK & STATUS ====================
  setRiskLevel(patientId: string, request: SetRiskRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/patients/${patientId}/risk`, request);
  }

  changeStatus(patientId: string, status: PatientStatus): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/patients/${patientId}/status`, { status });
  }

  recordVisit(patientId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/patients/${patientId}/record-visit`, {});
  }

  // ==================== ANALYTICS ====================
  getAnalytics(): Observable<PatientAnalytics> {
    return this.http.get<PatientAnalytics>(`${this.baseUrl}/patients/analytics`);
  }

  // ==================== AUDIT TRAIL ====================
  getAuditTrail(patientId: string, page = 1, pageSize = 50): Observable<PatientAuditTrail> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PatientAuditTrail>(`${this.baseUrl}/patients/${patientId}/audit-trail`, {
      params,
    });
  }

  // ==================== FOLLOW-UP & DATA RETENTION ====================
  getFollowUpPatients(days = 90): Observable<Patient[]> {
    const params = new HttpParams().set('days', days);
    return this.http.get<Patient[]>(`${this.baseUrl}/patients/follow-up`, { params });
  }

  getExpiredDataRetention(): Observable<Patient[]> {
    return this.http.get<Patient[]>(`${this.baseUrl}/patients/data-retention/expired`);
  }
}
