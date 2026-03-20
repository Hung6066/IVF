import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ProcedureDto,
  CreateProcedureRequest,
  CompleteProcedureRequest,
  PostponeProcedureRequest,
  CancelProcedureRequest,
  ProcedureSearchResult,
} from '../models/procedure.models';

@Injectable({ providedIn: 'root' })
export class ProcedureService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getById(id: string): Observable<ProcedureDto> {
    return this.http.get<ProcedureDto>(`${this.baseUrl}/procedures/${id}`);
  }

  getByPatient(patientId: string): Observable<ProcedureDto[]> {
    return this.http.get<ProcedureDto[]>(`${this.baseUrl}/procedures/patient/${patientId}`);
  }

  getByCycle(cycleId: string): Observable<ProcedureDto[]> {
    return this.http.get<ProcedureDto[]>(`${this.baseUrl}/procedures/cycle/${cycleId}`);
  }

  getByDate(date: string): Observable<ProcedureDto[]> {
    return this.http.get<ProcedureDto[]>(`${this.baseUrl}/procedures/date/${date}`);
  }

  search(
    query?: string,
    procedureType?: string,
    status?: string,
    page = 1,
    pageSize = 20,
  ): Observable<ProcedureSearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (procedureType) params = params.set('procedureType', procedureType);
    if (status) params = params.set('status', status);
    return this.http.get<ProcedureSearchResult>(`${this.baseUrl}/procedures`, { params });
  }

  create(request: CreateProcedureRequest): Observable<ProcedureDto> {
    return this.http.post<ProcedureDto>(`${this.baseUrl}/procedures`, request);
  }

  start(id: string): Observable<ProcedureDto> {
    return this.http.put<ProcedureDto>(`${this.baseUrl}/procedures/${id}/start`, {});
  }

  complete(id: string, request: CompleteProcedureRequest): Observable<ProcedureDto> {
    return this.http.put<ProcedureDto>(`${this.baseUrl}/procedures/${id}/complete`, request);
  }

  cancel(id: string, request: CancelProcedureRequest): Observable<ProcedureDto> {
    return this.http.put<ProcedureDto>(`${this.baseUrl}/procedures/${id}/cancel`, request);
  }

  postpone(id: string, request: PostponeProcedureRequest): Observable<ProcedureDto> {
    return this.http.put<ProcedureDto>(`${this.baseUrl}/procedures/${id}/postpone`, request);
  }
}
