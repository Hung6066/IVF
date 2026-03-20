import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, map, catchError } from 'rxjs';
import { environment } from '../../../../environments/environment';

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
  providedIn: 'root',
})
export class PharmacyService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  private drugs: Drug[] = [
    {
      id: '1',
      code: 'GNL450',
      name: 'Gonal-F 450IU',
      unit: 'Lọ',
      stock: 5,
      minStock: 20,
      expiry: '06/2025',
    },
    {
      id: '2',
      code: 'PRG200',
      name: 'Progesterone 200mg',
      unit: 'Viên',
      stock: 250,
      minStock: 100,
      expiry: '12/2025',
    },
    {
      id: '3',
      code: 'CTR025',
      name: 'Cetrotide 0.25mg',
      unit: 'Lọ',
      stock: 45,
      minStock: 30,
      expiry: '09/2025',
    },
  ];

  private imports: ImportSlip[] = [
    {
      id: '1',
      code: 'NK-001',
      date: '01/02/2024',
      supplier: 'Dược phẩm ABC',
      items: 15,
      total: 125000000,
      status: 'Hoàn thành',
    },
  ];

  getDrugs(): Observable<Drug[]> {
    return of(this.drugs);
  }

  getPrescriptions(): Observable<Prescription[]> {
    const params = new HttpParams().set('status', 'Pending,Processing').set('pageSize', 50);
    return this.http.get<{ items: any[] }>(`${this.baseUrl}/prescriptions`, { params }).pipe(
      map((res) =>
        res.items.map((p) => ({
          id: p.id,
          patient: p.patientName,
          doctor: p.doctorName,
          items: p.items?.length ?? 0,
          time: new Date(p.prescriptionDate).toLocaleTimeString('vi-VN', {
            hour: '2-digit',
            minute: '2-digit',
          }),
          status: p.status as Prescription['status'],
        })),
      ),
      catchError(() => of([])),
    );
  }

  getImports(): Observable<ImportSlip[]> {
    return of(this.imports);
  }
}
