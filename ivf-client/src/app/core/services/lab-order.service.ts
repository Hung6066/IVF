import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateLabOrderRequest,
  DeliverLabResultRequest,
  EnterLabResultRequest,
  LabOrderDto,
  LabOrderSearchResult,
  LabOrderStatistics,
} from '../models/lab-order.models';

@Injectable({ providedIn: 'root' })
export class LabOrderService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getById(id: string): Observable<LabOrderDto> {
    return this.http.get<LabOrderDto>(`${this.baseUrl}/lab/orders/${id}`);
  }

  getByPatient(patientId: string): Observable<LabOrderDto[]> {
    return this.http.get<LabOrderDto[]>(`${this.baseUrl}/lab/orders/patient/${patientId}`);
  }

  getByCycle(cycleId: string): Observable<LabOrderDto[]> {
    return this.http.get<LabOrderDto[]>(`${this.baseUrl}/lab/orders/cycle/${cycleId}`);
  }

  search(
    query?: string,
    status?: string,
    orderType?: string,
    from?: string,
    to?: string,
    page = 1,
    pageSize = 20,
  ): Observable<LabOrderSearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (status) params = params.set('status', status);
    if (orderType) params = params.set('orderType', orderType);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<LabOrderSearchResult>(`${this.baseUrl}/lab/orders`, { params });
  }

  getStatistics(): Observable<LabOrderStatistics> {
    return this.http.get<LabOrderStatistics>(`${this.baseUrl}/lab/orders/statistics`);
  }

  create(request: CreateLabOrderRequest): Observable<LabOrderDto> {
    return this.http.post<LabOrderDto>(`${this.baseUrl}/lab/orders`, request);
  }

  collectSample(orderId: string): Observable<LabOrderDto> {
    return this.http.put<LabOrderDto>(`${this.baseUrl}/lab/orders/${orderId}/collect-sample`, {});
  }

  enterResults(orderId: string, request: EnterLabResultRequest): Observable<LabOrderDto> {
    return this.http.put<LabOrderDto>(`${this.baseUrl}/lab/orders/${orderId}/results`, request);
  }

  deliverResults(orderId: string, request: DeliverLabResultRequest): Observable<LabOrderDto> {
    return this.http.put<LabOrderDto>(`${this.baseUrl}/lab/orders/${orderId}/deliver`, request);
  }
}
