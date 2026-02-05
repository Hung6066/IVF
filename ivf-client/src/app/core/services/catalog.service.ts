import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ServiceItem } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getServices(query?: string, category?: string, page = 1, pageSize = 50): Observable<any> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (query) params = params.set('q', query);
        if (category) params = params.set('category', category);
        return this.http.get<any>(`${this.baseUrl}/services`, { params });
    }

    getServiceCategories(): Observable<{ name: string; value: number }[]> {
        return this.http.get<{ name: string; value: number }[]>(`${this.baseUrl}/services/categories`);
    }

    createService(data: { code: string; name: string; category: string; unitPrice: number; unit?: string; description?: string }): Observable<any> {
        return this.http.post(`${this.baseUrl}/services`, data);
    }

    updateService(id: string, data: { name: string; category: string; unitPrice: number; unit?: string; description?: string }): Observable<any> {
        return this.http.put(`${this.baseUrl}/services/${id}`, data);
    }

    toggleService(id: string): Observable<{ isActive: boolean }> {
        return this.http.patch<{ isActive: boolean }>(`${this.baseUrl}/services/${id}/toggle`, {});
    }
}
