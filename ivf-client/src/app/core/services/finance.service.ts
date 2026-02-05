import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Invoice } from '../models/api.models'; // Re-using api.models for now to ensure compatibility

@Injectable({ providedIn: 'root' })
export class FinanceService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    searchInvoices(query?: string, page = 1, pageSize = 20): Observable<{ items: Invoice[]; total: number }> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (query) params = params.set('q', query);
        return this.http.get<{ items: Invoice[]; total: number }>(`${this.baseUrl}/billing/invoices`, { params });
    }

    getInvoice(id: string): Observable<Invoice> {
        return this.http.get<Invoice>(`${this.baseUrl}/billing/invoices/${id}`);
    }

    createInvoice(data: { patientId: string; cycleId?: string }): Observable<Invoice> {
        return this.http.post<Invoice>(`${this.baseUrl}/billing/invoices`, data);
    }

    issueInvoice(id: string): Observable<Invoice> {
        return this.http.post<Invoice>(`${this.baseUrl}/billing/invoices/${id}/issue`, {});
    }

    recordPayment(id: string, data: { amount: number; paymentMethod: string; transactionReference?: string }): Observable<any> {
        return this.http.post<any>(`${this.baseUrl}/billing/invoices/${id}/pay`, data);
    }
}
