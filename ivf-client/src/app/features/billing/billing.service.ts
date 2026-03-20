import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, map, catchError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Invoice {
  id: string;
  code: string;
  patient: string;
  date: string;
  total: number;
  paid: number;
  remaining: number;
  status: 'Paid' | 'Pending' | 'Partial' | string;
}

export interface Payment {
  id: string;
  code: string;
  invoice: string;
  patient: string;
  amount: number;
  method: string;
  datetime: string;
  cashier: string;
}

export interface RevenueChartData {
  day: string;
  pct: number;
}

@Injectable({
  providedIn: 'root',
})
export class BillingService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getInvoices(query?: string, page = 1, pageSize = 20): Observable<Invoice[]> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (query) params = params.set('q', query);
    return this.http.get<{ items: any[] }>(`${this.baseUrl}/billing/invoices`, { params }).pipe(
      map((res) =>
        res.items.map((i) => ({
          id: i.id,
          code: i.invoiceNumber,
          patient: i.patientName,
          date: new Date(i.invoiceDate).toLocaleDateString('vi-VN'),
          total: i.totalAmount,
          paid: i.paidAmount,
          remaining: i.balanceDue,
          status: i.status as Invoice['status'],
        })),
      ),
      catchError(() => of([])),
    );
  }

  getPayments(): Observable<Payment[]> {
    return of([
      {
        id: '1',
        code: 'TT-001',
        invoice: 'HD-001',
        patient: 'Nguyễn T.H',
        amount: 25000000,
        method: 'Chuyển khoản',
        datetime: '04/02 09:30',
        cashier: 'Thu Hà',
      },
      {
        id: '2',
        code: 'TT-002',
        invoice: 'HD-002',
        patient: 'Trần M.L',
        amount: 10000000,
        method: 'Tiền mặt',
        datetime: '04/02 10:15',
        cashier: 'Thu Hà',
      },
    ]);
  }

  getRevenueChartData(): Observable<RevenueChartData[]> {
    return of([
      { day: 'T2', pct: 65 },
      { day: 'T3', pct: 80 },
      { day: 'T4', pct: 55 },
      { day: 'T5', pct: 90 },
      { day: 'T6', pct: 75 },
      { day: 'T7', pct: 40 },
      { day: 'CN', pct: 20 },
    ]);
  }

  getStats() {
    return {
      todayInvoices: 0,
      todayRevenue: 0,
      pendingPayments: 0,
      weekRevenue: 0,
      monthRevenue: 0,
      quarterRevenue: 0,
    };
  }
}
