import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  InventoryItemDto,
  StockTransactionDto,
  CreateInventoryItemRequest,
  UpdateInventoryItemRequest,
  ImportStockRequest,
  RecordUsageRequest,
  AdjustStockRequest,
  InventorySearchResult,
} from '../models/inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  search(
    query?: string,
    category?: string,
    lowStockOnly?: boolean,
    page = 1,
    pageSize = 20,
  ): Observable<InventorySearchResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (category) params = params.set('category', category);
    if (lowStockOnly) params = params.set('lowStockOnly', true);
    return this.http.get<InventorySearchResult>(`${this.baseUrl}/inventory`, { params });
  }

  getById(id: string): Observable<InventoryItemDto> {
    return this.http.get<InventoryItemDto>(`${this.baseUrl}/inventory/${id}`);
  }

  getLowStockAlerts(): Observable<InventoryItemDto[]> {
    return this.http.get<InventoryItemDto[]>(`${this.baseUrl}/inventory/alerts/low-stock`);
  }

  getExpiringItems(days = 30): Observable<InventoryItemDto[]> {
    const params = new HttpParams().set('days', days);
    return this.http.get<InventoryItemDto[]>(`${this.baseUrl}/inventory/alerts/expiring`, {
      params,
    });
  }

  getTransactions(itemId: string, page = 1, pageSize = 20): Observable<StockTransactionDto[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<StockTransactionDto[]>(
      `${this.baseUrl}/inventory/${itemId}/transactions`,
      { params },
    );
  }

  create(request: CreateInventoryItemRequest): Observable<InventoryItemDto> {
    return this.http.post<InventoryItemDto>(`${this.baseUrl}/inventory`, request);
  }

  update(id: string, request: UpdateInventoryItemRequest): Observable<InventoryItemDto> {
    return this.http.put<InventoryItemDto>(`${this.baseUrl}/inventory/${id}`, request);
  }

  importStock(request: ImportStockRequest): Observable<StockTransactionDto> {
    return this.http.post<StockTransactionDto>(`${this.baseUrl}/inventory/import`, request);
  }

  recordUsage(request: RecordUsageRequest): Observable<StockTransactionDto> {
    return this.http.post<StockTransactionDto>(`${this.baseUrl}/inventory/usage`, request);
  }

  adjustStock(request: AdjustStockRequest): Observable<StockTransactionDto> {
    return this.http.post<StockTransactionDto>(`${this.baseUrl}/inventory/adjust`, request);
  }
}
