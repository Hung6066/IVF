import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Embryo } from '../models/medical.models';

@Injectable({ providedIn: 'root' })
export class EmbryoService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    getEmbryosByCycle(cycleId: string): Observable<Embryo[]> {
        return this.http.get<Embryo[]>(`${this.baseUrl}/embryos/cycle/${cycleId}`);
    }

    getActiveEmbryos(): Observable<Embryo[]> {
        return this.http.get<Embryo[]>(`${this.baseUrl}/embryos/active`);
    }

    getCryoStats(): Observable<any[]> {
        return this.http.get<any[]>(`${this.baseUrl}/embryos/cryo-stats`);
    }

    createEmbryo(data: Partial<Embryo>): Observable<Embryo> {
        return this.http.post<Embryo>(`${this.baseUrl}/embryos`, data);
    }

    transferEmbryo(id: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/embryos/${id}/transfer`, {});
    }

    freezeEmbryo(id: string, cryoLocationId: string): Observable<Embryo> {
        return this.http.post<Embryo>(`${this.baseUrl}/embryos/${id}/freeze`, { cryoLocationId });
    }
}
