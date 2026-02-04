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

    // ==================== APPOINTMENTS ====================
    getTodayAppointments(): Observable<Appointment[]> {
        return this.http.get<Appointment[]>(`${this.baseUrl}/appointments/today`);
    }

    getAppointments(start?: Date, end?: Date): Observable<Appointment[]> {
        let params = new HttpParams();
        if (start) params = params.set('start', start.toISOString());
        if (end) params = params.set('end', end.toISOString());
        return this.http.get<Appointment[]>(`${this.baseUrl}/appointments`, { params });
    }

    getUpcomingAppointments(doctorId?: string, days = 7): Observable<Appointment[]> {
        let params = new HttpParams().set('days', days);
        if (doctorId) params = params.set('doctorId', doctorId);
        return this.http.get<Appointment[]>(`${this.baseUrl}/appointments/upcoming`, { params });
    }

    getAppointment(id: string): Observable<Appointment> {
        return this.http.get<Appointment>(`${this.baseUrl}/appointments/${id}`);
    }

    getPatientAppointments(patientId: string): Observable<Appointment[]> {
        return this.http.get<Appointment[]>(`${this.baseUrl}/appointments/patient/${patientId}`);
    }

    getDoctorAppointments(doctorId: string, date?: Date): Observable<Appointment[]> {
        let params = new HttpParams();
        if (date) params = params.set('date', date.toISOString());
        return this.http.get<Appointment[]>(`${this.baseUrl}/appointments/doctor/${doctorId}`, { params });
    }

    createAppointment(data: CreateAppointmentRequest): Observable<Appointment> {
        return this.http.post<Appointment>(`${this.baseUrl}/appointments`, data);
    }

    confirmAppointment(id: string): Observable<Appointment> {
        return this.http.post<Appointment>(`${this.baseUrl}/appointments/${id}/confirm`, {});
    }

    checkInAppointment(id: string): Observable<Appointment> {
        return this.http.post<Appointment>(`${this.baseUrl}/appointments/${id}/checkin`, {});
    }

    completeAppointment(id: string): Observable<Appointment> {
        return this.http.post<Appointment>(`${this.baseUrl}/appointments/${id}/complete`, {});
    }

    cancelAppointment(id: string, reason?: string): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/appointments/${id}/cancel`, { reason });
    }

    rescheduleAppointment(id: string, newDateTime: Date): Observable<Appointment> {
        return this.http.post<Appointment>(`${this.baseUrl}/appointments/${id}/reschedule`, { newDateTime: newDateTime.toISOString() });
    }

    // ==================== NOTIFICATIONS ====================
    getNotifications(unreadOnly = false): Observable<Notification[]> {
        return this.http.get<Notification[]>(`${this.baseUrl}/notifications`, {
            params: new HttpParams().set('unreadOnly', unreadOnly)
        });
    }

    getUnreadCount(): Observable<{ count: number }> {
        return this.http.get<{ count: number }>(`${this.baseUrl}/notifications/unread-count`);
    }

    markNotificationAsRead(id: string): Observable<Notification> {
        return this.http.post<Notification>(`${this.baseUrl}/notifications/${id}/read`, {});
    }

    markAllNotificationsAsRead(): Observable<void> {
        return this.http.post<void>(`${this.baseUrl}/notifications/read-all`, {});
    }

    createNotification(data: CreateNotificationRequest): Observable<Notification> {
        return this.http.post<Notification>(`${this.baseUrl}/notifications`, data);
    }

    broadcastNotification(data: BroadcastNotificationRequest): Observable<{ sent: number }> {
        return this.http.post<{ sent: number }>(`${this.baseUrl}/notifications/broadcast`, data);
    }

    // ==================== AUDIT LOGS ====================
    getRecentAuditLogs(take = 100): Observable<AuditLog[]> {
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/recent`, {
            params: new HttpParams().set('take', take)
        });
    }

    getEntityAuditLogs(entityType: string, entityId: string): Observable<AuditLog[]> {
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/entity/${entityType}/${entityId}`);
    }

    getUserAuditLogs(userId: string, take = 100): Observable<AuditLog[]> {
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/user/${userId}`, {
            params: new HttpParams().set('take', take)
        });
    }

    searchAuditLogs(params: AuditSearchParams): Observable<AuditLog[]> {
        let httpParams = new HttpParams();
        if (params.entityType) httpParams = httpParams.set('entityType', params.entityType);
        if (params.action) httpParams = httpParams.set('action', params.action);
        if (params.userId) httpParams = httpParams.set('userId', params.userId);
        if (params.from) httpParams = httpParams.set('from', params.from.toISOString());
        if (params.to) httpParams = httpParams.set('to', params.to.toISOString());
        httpParams = httpParams.set('page', params.page || 1).set('pageSize', params.pageSize || 50);
        return this.http.get<AuditLog[]>(`${this.baseUrl}/audit/search`, { params: httpParams });
    }
}

// ==================== INTERFACES ====================
export interface Appointment {
    id: string;
    patientId: string;
    cycleId?: string;
    doctorId?: string;
    scheduledAt: string;
    durationMinutes: number;
    type: AppointmentType;
    status: AppointmentStatus;
    notes?: string;
    roomNumber?: string;
    patient?: Patient;
    doctor?: any;
    createdAt: string;
}

export type AppointmentType = 'Consultation' | 'Ultrasound' | 'Injection' | 'EggRetrieval' | 'EmbryoTransfer' | 'LabTest' | 'SemenCollection' | 'FollowUp' | 'Other';
export type AppointmentStatus = 'Scheduled' | 'Confirmed' | 'CheckedIn' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow' | 'Rescheduled';

export interface CreateAppointmentRequest {
    patientId: string;
    scheduledAt: string;
    type: AppointmentType;
    cycleId?: string;
    doctorId?: string;
    durationMinutes?: number;
    notes?: string;
    roomNumber?: string;
}

export interface Notification {
    id: string;
    userId: string;
    title: string;
    message: string;
    type: NotificationType;
    isRead: boolean;
    readAt?: string;
    entityType?: string;
    entityId?: string;
    createdAt: string;
}

export type NotificationType = 'Info' | 'Success' | 'Warning' | 'Error' | 'AppointmentReminder' | 'QueueCalled' | 'CycleUpdate' | 'PaymentDue';

export interface CreateNotificationRequest {
    userId: string;
    title: string;
    message: string;
    type: NotificationType;
    entityType?: string;
    entityId?: string;
}

export interface BroadcastNotificationRequest {
    userIds: string[];
    title: string;
    message: string;
    type: NotificationType;
}

export interface AuditLog {
    id: string;
    userId?: string;
    username?: string;
    entityType: string;
    entityId: string;
    action: 'Create' | 'Update' | 'Delete';
    oldValues?: string;
    newValues?: string;
    changedColumns?: string;
    ipAddress?: string;
    userAgent?: string;
    createdAt: string;
}

export interface AuditSearchParams {
    entityType?: string;
    action?: string;
    userId?: string;
    from?: Date;
    to?: Date;
    page?: number;
    pageSize?: number;
}

