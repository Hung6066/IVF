import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardStats, CycleSuccessRates, MonthlyRevenue } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ReportService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getDashboardStats(): Observable<DashboardStats> {
        return this.http.get<DashboardStats>(`${this.baseUrl}/reports/dashboard`);
    }

    getCycleSuccessRates(year?: number): Observable<CycleSuccessRates> {
        const params = year ? new HttpParams().set('year', year) : {};
        return this.http.get<CycleSuccessRates>(`${this.baseUrl}/reports/cycles/success-rates`, { params });
    }

    getMonthlyRevenue(year: number): Observable<MonthlyRevenue[]> {
        return this.http.get<MonthlyRevenue[]>(`${this.baseUrl}/reports/revenue/monthly`, {
            params: new HttpParams().set('year', year)
        });
    }
}

