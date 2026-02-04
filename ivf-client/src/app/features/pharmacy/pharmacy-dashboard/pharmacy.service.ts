import { Injectable, signal } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { Observable, of } from 'rxjs';

// Define local interfaces for Mock data since real API might not exist yet for Inventory
export interface Drug {
    id: string;
    code: string;
    name: string;
    unit: string;
    stock: number;
    minStock: number;
    expiry: string;
}

export interface Prescription {
    id: string;
    patient: string;
    doctor: string;
    items: number;
    time: string;
    status: 'Pending' | 'Processing' | 'Completed';
}

export interface ImportSlip {
    id: string;
    code: string;
    date: string;
    supplier: string;
    items: number;
    total: number;
    status: string;
}

@Injectable({
    providedIn: 'root'
})
export class PharmacyService {
    // Mocks moved from Component
    private drugs: Drug[] = [
        { id: '1', code: 'GNL450', name: 'Gonal-F 450IU', unit: 'Lọ', stock: 5, minStock: 20, expiry: '06/2025' },
        { id: '2', code: 'PRG200', name: 'Progesterone 200mg', unit: 'Viên', stock: 250, minStock: 100, expiry: '12/2025' },
        { id: '3', code: 'CTR025', name: 'Cetrotide 0.25mg', unit: 'Lọ', stock: 45, minStock: 30, expiry: '09/2025' }
    ];

    private prescriptions: Prescription[] = [
        { id: '1', patient: 'Nguyễn T.A', doctor: 'BS. Trần B', items: 5, time: '09:30', status: 'Pending' },
        { id: '2', patient: 'Lê V.C', doctor: 'BS. Phạm D', items: 3, time: '10:15', status: 'Processing' },
        { id: '3', patient: 'Hoàng T.E', doctor: 'BS. Trần B', items: 2, time: '08:45', status: 'Completed' }
    ];

    private imports: ImportSlip[] = [
        { id: '1', code: 'NK-001', date: '01/02/2024', supplier: 'Dược phẩm ABC', items: 15, total: 125000000, status: 'Hoàn thành' }
    ];

    constructor(private api: ApiService) { }

    getDrugs(): Observable<Drug[]> {
        return of(this.drugs);
    }

    getPrescriptions(): Observable<Prescription[]> {
        // In real app, call api.getPrescriptions()
        return of(this.prescriptions);
    }

    getImports(): Observable<ImportSlip[]> {
        return of(this.imports);
    }

    // Helper logic could be here, but usually services just return Observables
}
