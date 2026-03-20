import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, map, catchError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Donor {
  id: string;
  code: string;
  bloodType: string;
  age: number | null;
  samples: number;
  status: string;
}

export interface Sample {
  id: string;
  code: string;
  donor: string;
  vials: number;
  status: string;
}

export interface Match {
  id: string;
  recipient: string;
  donor: string;
  date: string;
  status: string;
}

@Injectable({
  providedIn: 'root',
})
export class SpermBankService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getDonors(query?: string): Observable<Donor[]> {
    let params = new HttpParams().set('pageSize', 100);
    if (query) params = params.set('q', query);
    return this.http.get<{ items: any[] }>(`${this.baseUrl}/spermbank/donors`, { params }).pipe(
      map((res) =>
        res.items.map((d) => ({
          id: d.id,
          code: d.donorCode,
          bloodType: d.bloodType ?? '-',
          age: null,
          samples: d.totalDonations,
          status: d.status,
        })),
      ),
      catchError(() => of([])),
    );
  }

  getSamples(): Observable<Sample[]> {
    return this.http.get<any[]>(`${this.baseUrl}/spermbank/samples/available`).pipe(
      map((items) =>
        items.map((s) => ({
          id: s.id,
          code: s.sampleCode,
          donor: s.donorId,
          vials: s.vialCount ?? 0,
          status: s.isAvailable ? 'Available' : 'Reserved',
        })),
      ),
      catchError(() => of([])),
    );
  }

  getMatches(): Observable<Match[]> {
    return of([
      {
        id: '1',
        recipient: 'Nguyễn T.H',
        donor: 'NH-001',
        date: '01/02/2024',
        status: 'Confirmed',
      },
    ]);
  }
}
