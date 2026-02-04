import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

export interface Invoice {
    id: string;
    code: string;
    patient: string;
    date: string;
    total: number;
    paid: number;
    remaining: number;
    status: 'Paid' | 'Pending' | 'Partial';
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
    providedIn: 'root'
})
export class BillingService {

    constructor() { }

    getInvoices(): Observable<Invoice[]> {
        return of([
            { id: '1', code: 'HD-001', patient: 'Nguyễn T.H', date: '04/02/2024', total: 25000000, paid: 25000000, remaining: 0, status: 'Paid' },
            { id: '2', code: 'HD-002', patient: 'Trần M.L', date: '04/02/2024', total: 18000000, paid: 10000000, remaining: 8000000, status: 'Partial' },
            { id: '3', code: 'HD-003', patient: 'Lê V.A', date: '04/02/2024', total: 5000000, paid: 0, remaining: 5000000, status: 'Pending' }
        ]);
    }

    getPayments(): Observable<Payment[]> {
        return of([
            { id: '1', code: 'TT-001', invoice: 'HD-001', patient: 'Nguyễn T.H', amount: 25000000, method: 'Chuyển khoản', datetime: '04/02 09:30', cashier: 'Thu Hà' },
            { id: '2', code: 'TT-002', invoice: 'HD-002', patient: 'Trần M.L', amount: 10000000, method: 'Tiền mặt', datetime: '04/02 10:15', cashier: 'Thu Hà' }
        ]);
    }

    getRevenueChartData(): Observable<RevenueChartData[]> {
        return of([
            { day: 'T2', pct: 65 }, { day: 'T3', pct: 80 }, { day: 'T4', pct: 55 },
            { day: 'T5', pct: 90 }, { day: 'T6', pct: 75 }, { day: 'T7', pct: 40 }, { day: 'CN', pct: 20 }
        ]);
    }

    // Mock calculations
    getStats() {
        return {
            todayInvoices: 12,
            todayRevenue: 185000000,
            pendingPayments: 5,
            weekRevenue: 850000000,
            monthRevenue: 2450000000,
            quarterRevenue: 7200000000
        };
    }
}
