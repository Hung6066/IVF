import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConsentFormDto,
  CreateConsentFormRequest,
  SignConsentFormRequest,
} from '../models/consent-form.models';

@Injectable({ providedIn: 'root' })
export class ConsentFormService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getByPatient(patientId: string): Observable<ConsentFormDto[]> {
    return this.http.get<ConsentFormDto[]>(`${this.baseUrl}/consent-forms/patient/${patientId}`);
  }

  getByCycle(cycleId: string): Observable<ConsentFormDto[]> {
    return this.http.get<ConsentFormDto[]>(`${this.baseUrl}/consent-forms/cycle/${cycleId}`);
  }

  getById(id: string): Observable<ConsentFormDto> {
    return this.http.get<ConsentFormDto>(`${this.baseUrl}/consent-forms/${id}`);
  }

  create(request: CreateConsentFormRequest): Observable<ConsentFormDto> {
    return this.http.post<ConsentFormDto>(`${this.baseUrl}/consent-forms`, request);
  }

  sign(id: string, request: SignConsentFormRequest): Observable<ConsentFormDto> {
    return this.http.put<ConsentFormDto>(`${this.baseUrl}/consent-forms/${id}/sign`, request);
  }

  revoke(id: string, reason: string): Observable<ConsentFormDto> {
    return this.http.put<ConsentFormDto>(`${this.baseUrl}/consent-forms/${id}/revoke`, { reason });
  }

  uploadScan(id: string, scanUrl: string): Observable<ConsentFormDto> {
    return this.http.put<ConsentFormDto>(`${this.baseUrl}/consent-forms/${id}/scan`, { scanUrl });
  }
}
