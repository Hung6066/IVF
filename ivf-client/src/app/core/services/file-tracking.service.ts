import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FileTrackingDto, PagedResult } from '../models/clinical-management.models';

export interface CreateFileTrackingRequest {
  patientId: string;
  fileCode: string;
  currentLocation: string;
  notes?: string;
}

export interface TransferFileRequest {
  toLocation: string;
  transferredByUserId: string;
  reason?: string;
}

@Injectable({ providedIn: 'root' })
export class FileTrackingService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  search(
    query?: string,
    status?: string,
    location?: string,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<FileTrackingDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (status) params = params.set('status', status);
    if (location) params = params.set('location', location);
    return this.http.get<PagedResult<FileTrackingDto>>(`${this.baseUrl}/file-tracking`, { params });
  }

  getById(id: string): Observable<FileTrackingDto> {
    return this.http.get<FileTrackingDto>(`${this.baseUrl}/file-tracking/${id}`);
  }

  getByPatient(patientId: string): Observable<FileTrackingDto[]> {
    return this.http.get<FileTrackingDto[]>(`${this.baseUrl}/file-tracking/patient/${patientId}`);
  }

  getByLocation(location: string): Observable<FileTrackingDto[]> {
    return this.http.get<FileTrackingDto[]>(
      `${this.baseUrl}/file-tracking/location/${encodeURIComponent(location)}`,
    );
  }

  create(request: CreateFileTrackingRequest): Observable<FileTrackingDto> {
    return this.http.post<FileTrackingDto>(`${this.baseUrl}/file-tracking`, request);
  }

  transfer(id: string, request: TransferFileRequest): Observable<FileTrackingDto> {
    return this.http.put<FileTrackingDto>(`${this.baseUrl}/file-tracking/${id}/transfer`, request);
  }

  markReceived(id: string): Observable<FileTrackingDto> {
    return this.http.put<FileTrackingDto>(`${this.baseUrl}/file-tracking/${id}/received`, {});
  }

  markLost(id: string, notes?: string): Observable<FileTrackingDto> {
    return this.http.put<FileTrackingDto>(`${this.baseUrl}/file-tracking/${id}/lost`, { notes });
  }
}
