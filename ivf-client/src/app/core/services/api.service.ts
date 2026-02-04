import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    Patient, PatientListResponse, Couple, TreatmentCycle,
    QueueTicket, Ultrasound, Embryo, Invoice,
    DashboardStats, CycleSuccessRates, MonthlyRevenue
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
    private readonly baseUrl = environment.apiUrl;

    constructor(private http: HttpClient) { }

    // ==================== PATIENTS ====================
    searchPatients(query?: string, page = 1, pageSize = 20): Observable<PatientListResponse> {
        let params = new HttpParams()
            .set('page', page)
            .set('pageSize', pageSize);
        if (query) params = params.set('q', query);
        return this.http.get<PatientListResponse>(`${this.baseUrl}/patients`, { params });
    }

    getPatient(id: string): Observable<Patient> {
        return this.http.get<Patient>(`${this.baseUrl}/patients/${id}`);
    }

    createPatient(patient: Partial<Patient>): Observable<Patient> {
        return this.http.post<Patient>(`${this.baseUrl}/patients`, patient);
    }

    updatePatient(id: string, data: Partial<Patient>): Observable<Patient> {
        return this.http.put<Patient>(`${this.baseUrl}/patients/${id}`, data);
    }

    deletePatient(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/patients/${id}`);
    }

    // ==================== COUPLES ====================
    getCouple(id: string): Observable<Couple> {
        return this.http.get<Couple>(`${this.baseUrl}/couples/${id}`);
    }

    getCoupleByPatient(patientId: string): Observable<Couple> {
        return this.http.get<Couple>(`${this.baseUrl}/couples/patient/${patientId}`);
    }

    createCouple(data: { wifeId: string; husbandId: string; marriageDate?: string; infertilityYears?: number }): Observable<Couple> {
        return this.http.post<Couple>(`${this.baseUrl}/couples`, data);
    }

    getCouples(): Observable<Couple[]> {
        return this.http.get<Couple[]>(`${this.baseUrl}/couples`);
    }

    // ==================== CYCLES ====================
    getCycle(id: string): Observable<TreatmentCycle> {
        return this.http.get<TreatmentCycle>(`${this.baseUrl}/cycles/${id}`);
    }

    getCyclesByCouple(coupleId: string): Observable<TreatmentCycle[]> {
        return this.http.get<TreatmentCycle[]>(`${this.baseUrl}/cycles/couple/${coupleId}`);
    }

    createCycle(data: { coupleId: string; method: string; startDate: string; notes?: string }): Observable<TreatmentCycle> {
        return this.http.post<TreatmentCycle>(`${this.baseUrl}/cycles`, data);
    }

    advanceCyclePhase(id: string, phase: string): Observable<TreatmentCycle> {
        return this.http.put<TreatmentCycle>(`${this.baseUrl}/cycles/${id}/phase`, { phase });
    }

    completeCycle(id: string, outcome: string): Observable<TreatmentCycle> {
        return this.http.post<TreatmentCycle>(`${this.baseUrl}/cycles/${id}/complete`, { outcome });
    }

    // ==================== QUEUE ====================
    getQueue(departmentCode: string): Observable<QueueTicket[]> {
        return this.http.get<QueueTicket[]>(`${this.baseUrl}/queue/${departmentCode}`);
    }

    issueTicket(data: { patientId: string; departmentCode: string }): Observable<QueueTicket> {
        return this.http.post<QueueTicket>(`${this.baseUrl}/queue/issue`, data);
    }

    callTicket(id: string): Observable<QueueTicket> {
        return this.http.post<QueueTicket>(`${this.baseUrl}/queue/${id}/call`, {});
    }

    completeTicket(id: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/queue/${id}/complete`, {});
    }

    skipTicket(id: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/queue/${id}/skip`, {});
    }

    // ==================== ULTRASOUNDS ====================
    getUltrasoundsByCycle(cycleId: string): Observable<Ultrasound[]> {
        return this.http.get<Ultrasound[]>(`${this.baseUrl}/ultrasounds/cycle/${cycleId}`);
    }

    createUltrasound(data: Partial<Ultrasound>): Observable<Ultrasound> {
        return this.http.post<Ultrasound>(`${this.baseUrl}/ultrasounds`, data);
    }

    // ==================== EMBRYOS ====================
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

    // ==================== INVOICES ====================
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

    // ==================== REPORTS ====================
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
