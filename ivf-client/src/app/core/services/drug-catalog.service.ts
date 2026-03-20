import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DrugCatalogDto, DrugCategory, PagedResult } from '../models/clinical-management.models';

export interface CreateDrugRequest {
  code: string;
  name: string;
  genericName: string;
  category: DrugCategory;
  unit: string;
  activeIngredient?: string;
  defaultDosage?: string;
  notes?: string;
}

export interface UpdateDrugRequest {
  name: string;
  genericName: string;
  category: DrugCategory;
  unit: string;
  activeIngredient?: string;
  defaultDosage?: string;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class DrugCatalogService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  search(
    query?: string,
    category?: DrugCategory,
    isActive?: boolean,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<DrugCatalogDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    if (category) params = params.set('category', category);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<PagedResult<DrugCatalogDto>>(`${this.baseUrl}/drug-catalog`, { params });
  }

  getActive(): Observable<DrugCatalogDto[]> {
    return this.http.get<DrugCatalogDto[]>(`${this.baseUrl}/drug-catalog/active`);
  }

  getByCategory(category: DrugCategory): Observable<DrugCatalogDto[]> {
    return this.http.get<DrugCatalogDto[]>(`${this.baseUrl}/drug-catalog/category/${category}`);
  }

  getById(id: string): Observable<DrugCatalogDto> {
    return this.http.get<DrugCatalogDto>(`${this.baseUrl}/drug-catalog/${id}`);
  }

  create(request: CreateDrugRequest): Observable<DrugCatalogDto> {
    return this.http.post<DrugCatalogDto>(`${this.baseUrl}/drug-catalog`, request);
  }

  update(id: string, request: UpdateDrugRequest): Observable<DrugCatalogDto> {
    return this.http.put<DrugCatalogDto>(`${this.baseUrl}/drug-catalog/${id}`, request);
  }

  toggleActive(id: string, activate: boolean): Observable<DrugCatalogDto> {
    return this.http.put<DrugCatalogDto>(`${this.baseUrl}/drug-catalog/${id}/toggle-active`, {
      activate,
    });
  }
}
