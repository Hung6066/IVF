import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  FetProtocolDto,
  CreateFetProtocolRequest,
  UpdateHormoneTherapyRequest,
  RecordEndometriumCheckRequest,
  RecordThawingRequest,
  ScheduleTransferRequest,
  FetSearchResult,
} from '../models/fet.models';

@Injectable({ providedIn: 'root' })
export class FetService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/fet`;

  getById(id: string): Observable<FetProtocolDto> {
    return this.http.get<FetProtocolDto>(`${this.baseUrl}/${id}`);
  }

  getByCycle(cycleId: string): Observable<FetProtocolDto> {
    return this.http.get<FetProtocolDto>(`${this.baseUrl}/cycle/${cycleId}`);
  }

  search(query?: string, status?: string, page = 1, pageSize = 20): Observable<FetSearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (status) params = params.set('status', status);
    return this.http.get<FetSearchResult>(this.baseUrl, { params });
  }

  create(data: CreateFetProtocolRequest): Observable<FetProtocolDto> {
    return this.http.post<FetProtocolDto>(this.baseUrl, data);
  }

  updateHormoneTherapy(id: string, data: UpdateHormoneTherapyRequest): Observable<FetProtocolDto> {
    return this.http.put<FetProtocolDto>(`${this.baseUrl}/${id}/hormones`, data);
  }

  recordEndometriumCheck(
    id: string,
    data: RecordEndometriumCheckRequest,
  ): Observable<FetProtocolDto> {
    return this.http.put<FetProtocolDto>(`${this.baseUrl}/${id}/endometrium`, data);
  }

  recordThawing(id: string, data: RecordThawingRequest): Observable<FetProtocolDto> {
    return this.http.put<FetProtocolDto>(`${this.baseUrl}/${id}/thawing`, data);
  }

  scheduleTransfer(id: string, data: ScheduleTransferRequest): Observable<FetProtocolDto> {
    return this.http.put<FetProtocolDto>(`${this.baseUrl}/${id}/schedule-transfer`, data);
  }

  markTransferred(id: string): Observable<FetProtocolDto> {
    return this.http.post<FetProtocolDto>(`${this.baseUrl}/${id}/transferred`, {});
  }

  cancel(id: string, reason?: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/cancel`, { reason });
  }
}
