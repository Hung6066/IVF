import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  InventoryRequestDto,
  InventoryRequestStatus,
  InventoryRequestType,
  PagedResult,
} from '../models/clinical-management.models';

export interface CreateInventoryRequestRequest {
  requestType: InventoryRequestType;
  requestedByUserId: string;
  itemName: string;
  quantity: number;
  unit: string;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class InventoryRequestService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  search(
    query?: string,
    status?: InventoryRequestStatus,
    type?: InventoryRequestType,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<InventoryRequestDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (status) params = params.set('status', status);
    if (type) params = params.set('type', type);
    return this.http.get<PagedResult<InventoryRequestDto>>(`${this.baseUrl}/inventory-requests`, {
      params,
    });
  }

  getPending(): Observable<InventoryRequestDto[]> {
    return this.http.get<InventoryRequestDto[]>(`${this.baseUrl}/inventory-requests/pending`);
  }

  getById(id: string): Observable<InventoryRequestDto> {
    return this.http.get<InventoryRequestDto>(`${this.baseUrl}/inventory-requests/${id}`);
  }

  create(request: CreateInventoryRequestRequest): Observable<InventoryRequestDto> {
    return this.http.post<InventoryRequestDto>(`${this.baseUrl}/inventory-requests`, request);
  }

  approve(id: string, approvedByUserId: string): Observable<InventoryRequestDto> {
    return this.http.put<InventoryRequestDto>(`${this.baseUrl}/inventory-requests/${id}/approve`, {
      approvedByUserId,
    });
  }

  reject(id: string, reason: string): Observable<InventoryRequestDto> {
    return this.http.put<InventoryRequestDto>(`${this.baseUrl}/inventory-requests/${id}/reject`, {
      reason,
    });
  }

  fulfill(id: string, fulfilledByUserId: string): Observable<InventoryRequestDto> {
    return this.http.put<InventoryRequestDto>(`${this.baseUrl}/inventory-requests/${id}/fulfill`, {
      fulfilledByUserId,
    });
  }
}
