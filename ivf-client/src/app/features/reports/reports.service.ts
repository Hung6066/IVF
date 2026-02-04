import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

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
    providedIn: 'root'
})
export class ReportsService {

    constructor() { }

    getKPIs(): Observable<KPIData> {
        return of({
            successRate: 42,
            successRateTrend: 3.2,
            totalCycles: 156,
            ivfCycles: 98,
            iuiCycles: 58,
            totalRevenue: 2450000000,
            revenueTrend: 12,
            newPatients: 45,
            totalPatients: 1250
        });
    }

    getMonthlyResults(): Observable<MonthlyResult[]> {
        return of([
            { name: 'T1', success: 35, failed: 15 },
            { name: 'T2', success: 40, failed: 12 },
            { name: 'T3', success: 38, failed: 18 },
            { name: 'T4', success: 45, failed: 10 },
            { name: 'T5', success: 42, failed: 14 },
            { name: 'T6', success: 48, failed: 8 }
        ]);
    }

    getTopDoctors(): Observable<DoctorPerformance[]> {
        return of([
            { name: 'BS. Nguyễn Văn A', cycles: 45, successRate: 48 },
            { name: 'BS. Trần Thị B', cycles: 38, successRate: 45 },
            { name: 'BS. Lê Văn C', cycles: 32, successRate: 42 }
        ]);
    }

    getCryoStats(): Observable<CryoStats> {
        return of({
            embryos: 342,
            eggs: 128,
            sperm: 256
        });
    }
}
