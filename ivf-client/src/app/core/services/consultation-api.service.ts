import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConsultationDto,
  ConsultationSearchResult,
  CreateConsultationRequest,
  RecordClinicalDataRequest,
  RecordDiagnosisRequest,
} from '../models/consultation.models';

@Injectable({ providedIn: 'root' })
export class ConsultationApiService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getById(id: string): Observable<ConsultationDto> {
    return this.http.get<ConsultationDto>(`${this.baseUrl}/consultations/${id}`);
  }

  getByPatient(patientId: string): Observable<ConsultationDto[]> {
    return this.http.get<ConsultationDto[]>(`${this.baseUrl}/consultations/patient/${patientId}`);
  }

  getByCycle(cycleId: string): Observable<ConsultationDto[]> {
    return this.http.get<ConsultationDto[]>(`${this.baseUrl}/consultations/cycle/${cycleId}`);
  }

  search(
    query?: string,
    status?: string,
    type?: string,
    from?: string,
    to?: string,
    page = 1,
    pageSize = 20,
  ): Observable<ConsultationSearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (status) params = params.set('status', status);
    if (type) params = params.set('type', type);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ConsultationSearchResult>(`${this.baseUrl}/consultations`, { params });
  }

  create(request: CreateConsultationRequest): Observable<ConsultationDto> {
    return this.http.post<ConsultationDto>(`${this.baseUrl}/consultations`, request);
  }

  start(consultationId: string): Observable<ConsultationDto> {
    return this.http.put<ConsultationDto>(
      `${this.baseUrl}/consultations/${consultationId}/start`,
      {},
    );
  }

  recordClinicalData(
    consultationId: string,
    data: RecordClinicalDataRequest,
  ): Observable<ConsultationDto> {
    return this.http.put<ConsultationDto>(
      `${this.baseUrl}/consultations/${consultationId}/clinical-data`,
      data,
    );
  }

  recordDiagnosis(
    consultationId: string,
    data: RecordDiagnosisRequest,
  ): Observable<ConsultationDto> {
    return this.http.put<ConsultationDto>(
      `${this.baseUrl}/consultations/${consultationId}/diagnosis`,
      data,
    );
  }

  complete(consultationId: string): Observable<ConsultationDto> {
    return this.http.put<ConsultationDto>(
      `${this.baseUrl}/consultations/${consultationId}/complete`,
      {},
    );
  }

  cancel(consultationId: string): Observable<ConsultationDto> {
    return this.http.put<ConsultationDto>(
      `${this.baseUrl}/consultations/${consultationId}/cancel`,
      {},
    );
  }
}
