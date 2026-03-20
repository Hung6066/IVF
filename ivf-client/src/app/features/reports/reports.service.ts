import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, map, catchError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface KPIData {
  successRate: number;
  successRateTrend: number;
  totalCycles: number;
  ivfCycles: number;
  iuiCycles: number;
  totalRevenue: number;
  revenueTrend: number;
  newPatients: number;
  totalPatients: number;
}

export interface MonthlyResult {
  name: string;
  success: number;
  failed: number;
}

export interface DoctorPerformance {
  name: string;
  cycles: number;
  successRate: number;
}

export interface CryoStats {
  embryos: number;
  eggs: number;
  sperm: number;
}

@Injectable({
  providedIn: 'root',
})
export class ReportsService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getKPIs(): Observable<KPIData> {
    return this.http.get<any>(`${this.baseUrl}/reports/dashboard`).pipe(
      map((d) => ({
        successRate: 0,
        successRateTrend: 0,
        totalCycles: d.activeCycles ?? 0,
        ivfCycles: 0,
        iuiCycles: 0,
        totalRevenue: d.monthlyRevenue ?? 0,
        revenueTrend: 0,
        newPatients: 0,
        totalPatients: d.totalPatients ?? 0,
      })),
      catchError(() =>
        of({
          successRate: 0,
          successRateTrend: 0,
          totalCycles: 0,
          ivfCycles: 0,
          iuiCycles: 0,
          totalRevenue: 0,
          revenueTrend: 0,
          newPatients: 0,
          totalPatients: 0,
        }),
      ),
    );
  }

  getMonthlyResults(year?: number): Observable<MonthlyResult[]> {
    const params = new HttpParams().set('year', year ?? new Date().getFullYear());
    return this.http.get<any[]>(`${this.baseUrl}/reports/cycles/success-rates`, { params }).pipe(
      map((items) =>
        items.map((r) => ({
          name: `T${r.month ?? '?'}`,
          success: r.pregnancies ?? 0,
          failed: (r.totalCycles ?? 0) - (r.pregnancies ?? 0),
        })),
      ),
      catchError(() => of([{ name: 'T1', success: 0, failed: 0 }])),
    );
  }

  getTopDoctors(): Observable<DoctorPerformance[]> {
    return of([
      { name: 'BS. Nguyễn Văn A', cycles: 45, successRate: 48 },
      { name: 'BS. Trần Thị B', cycles: 38, successRate: 45 },
      { name: 'BS. Lê Văn C', cycles: 32, successRate: 42 },
    ]);
  }

  getCryoStats(): Observable<CryoStats> {
    return of({
      embryos: 342,
      eggs: 128,
      sperm: 256,
    });
  }
}
