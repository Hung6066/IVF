import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    Patient, PatientListResponse, Couple, TreatmentCycle,
    QueueTicket, Ultrasound, Embryo, Invoice,
    DashboardStats, CycleSuccessRates, MonthlyRevenue,
    TreatmentIndication, StimulationData, CultureData, TransferData,
    LutealPhaseData, PregnancyData, BirthData, AdverseEventData
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

    searchDoctors(query?: string): Observable<any[]> {
        let params = new HttpParams();
        if (query) params = params.set('q', query);
        return this.http.get<any[]>(`${this.baseUrl}/doctors`, { params });
    }

    createDoctor(data: { userId: string, specialty: string, licenseNumber?: string, roomNumber?: string, maxPatientsPerDay?: number }): Observable<any> {
        return this.http.post(`${this.baseUrl}/doctors`, data);
    }

    // ==================== USERS / ADMIN ====================
    getUsers(search?: string, role?: string, isActive?: boolean, page = 1, pageSize = 20): Observable<any> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (search) params = params.set('q', search);
        if (role) params = params.set('role', role);
        if (isActive !== undefined) params = params.set('isActive', isActive);
        return this.http.get(`${this.baseUrl}/users`, { params });
    }

    getRoles(): Observable<string[]> {
        return this.http.get<string[]>(`${this.baseUrl}/users/roles`);
    }

    createUser(data: any): Observable<any> {
        return this.http.post(`${this.baseUrl}/users`, data);
    }

    updateUser(id: string, data: any): Observable<void> {
        return this.http.put<void>(`${this.baseUrl}/users/${id}`, data);
    }

    deleteUser(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/users/${id}`);
    }

    // ==================== PERMISSIONS ====================
    getAllPermissions(): Observable<{ name: string, value: number }[]> {
        return this.http.get<{ name: string, value: number }[]>(`${this.baseUrl}/users/permissions`);
    }

    getUserPermissions(userId: string): Observable<string[]> {
        return this.http.get<string[]>(`${this.baseUrl}/users/${userId}/permissions`);
    }

    assignPermissions(userId: string, permissions: string[], grantedBy?: string): Observable<any> {
        return this.http.post(`${this.baseUrl}/users/${userId}/permissions`, { permissions, grantedBy });
    }

    revokePermission(userId: string, permission: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/users/${userId}/permissions/${permission}`);
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

    // ==================== CYCLE PHASE DATA ====================
    getCycleIndication(cycleId: string): Observable<TreatmentIndication> {
        return this.http.get<TreatmentIndication>(`${this.baseUrl}/cycles/${cycleId}/indication`);
    }

    updateCycleIndication(cycleId: string, data: Partial<TreatmentIndication>): Observable<TreatmentIndication> {
        return this.http.put<TreatmentIndication>(`${this.baseUrl}/cycles/${cycleId}/indication`, data);
    }

    getCycleStimulation(cycleId: string): Observable<StimulationData> {
        return this.http.get<StimulationData>(`${this.baseUrl}/cycles/${cycleId}/stimulation`);
    }

    updateCycleStimulation(cycleId: string, data: Partial<StimulationData>): Observable<StimulationData> {
        return this.http.put<StimulationData>(`${this.baseUrl}/cycles/${cycleId}/stimulation`, data);
    }

    getCycleCulture(cycleId: string): Observable<CultureData> {
        return this.http.get<CultureData>(`${this.baseUrl}/cycles/${cycleId}/culture`);
    }

    updateCycleCulture(cycleId: string, data: Partial<CultureData>): Observable<CultureData> {
        return this.http.put<CultureData>(`${this.baseUrl}/cycles/${cycleId}/culture`, data);
    }

    getCycleTransfer(cycleId: string): Observable<TransferData> {
        return this.http.get<TransferData>(`${this.baseUrl}/cycles/${cycleId}/transfer`);
    }

    updateCycleTransfer(cycleId: string, data: Partial<TransferData>): Observable<TransferData> {
        return this.http.put<TransferData>(`${this.baseUrl}/cycles/${cycleId}/transfer`, data);
    }

    getCycleLutealPhase(cycleId: string): Observable<LutealPhaseData> {
        return this.http.get<LutealPhaseData>(`${this.baseUrl}/cycles/${cycleId}/luteal-phase`);
    }

    updateCycleLutealPhase(cycleId: string, data: Partial<LutealPhaseData>): Observable<LutealPhaseData> {
        return this.http.put<LutealPhaseData>(`${this.baseUrl}/cycles/${cycleId}/luteal-phase`, data);
    }

    getCyclePregnancy(cycleId: string): Observable<PregnancyData> {
        return this.http.get<PregnancyData>(`${this.baseUrl}/cycles/${cycleId}/pregnancy`);
    }

    updateCyclePregnancy(cycleId: string, data: Partial<PregnancyData>): Observable<PregnancyData> {
        return this.http.put<PregnancyData>(`${this.baseUrl}/cycles/${cycleId}/pregnancy`, data);
    }

    getCycleBirth(cycleId: string): Observable<BirthData> {
        return this.http.get<BirthData>(`${this.baseUrl}/cycles/${cycleId}/birth`);
    }

    updateCycleBirth(cycleId: string, data: Partial<BirthData>): Observable<BirthData> {
        return this.http.put<BirthData>(`${this.baseUrl}/cycles/${cycleId}/birth`, data);
    }

    getCycleAdverseEvents(cycleId: string): Observable<AdverseEventData[]> {
        return this.http.get<AdverseEventData[]>(`${this.baseUrl}/cycles/${cycleId}/adverse-events`);
    }

    createCycleAdverseEvent(cycleId: string, data: Partial<AdverseEventData>): Observable<AdverseEventData> {
        return this.http.post<AdverseEventData>(`${this.baseUrl}/cycles/${cycleId}/adverse-events`, data);
    }

    // ==================== QUEUE ====================
    getQueueByDept(deptCode: string): Observable<any[]> {
        return this.http.get<any[]>(`${this.baseUrl}/queue/${deptCode}`);
    }

    issueTicket(patientId: string, dept: string, type: 'Normal' | 'VIP' | 'Emergency' = 'Normal', notes?: string, cycleId?: string, serviceIds?: string[]): Observable<any> {
        return this.http.post(`${this.baseUrl}/queue/issue`, {
            patientId,
            departmentCode: dept,
            priority: type, // Changed from queueType to priority
            cycleId,
            serviceIds // Added serviceIds
        });
    }

    callTicket(ticketId: string): Observable<any> {
        return this.http.post(`${this.baseUrl}/queue/${ticketId}/call`, {});
    }

    completeTicket(ticketId: string): Observable<any> {
        return this.http.post(`${this.baseUrl}/queue/${ticketId}/complete`, {});
    }

    skipTicket(ticketId: string): Observable<any> {
        return this.http.post(`${this.baseUrl}/queue/${ticketId}/skip`, {});
    }

    getPatientPendingTicket(patientId: string): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/queue/patient/${patientId}/pending`);
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

    // ==================== SERVICE CATALOG ====================
    getServices(query?: string, category?: string, page = 1, pageSize = 50): Observable<any> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (query) params = params.set('q', query);
        if (category) params = params.set('category', category);
        return this.http.get<any>(`${this.baseUrl}/services`, { params });
    }

    getServiceCategories(): Observable<{ name: string; value: number }[]> {
        return this.http.get<{ name: string; value: number }[]>(`${this.baseUrl}/services/categories`);
    }

    createService(data: { code: string; name: string; category: string; unitPrice: number; unit?: string; description?: string }): Observable<any> {
        return this.http.post(`${this.baseUrl}/services`, data);
    }

    updateService(id: string, data: { name: string; category: string; unitPrice: number; unit?: string; description?: string }): Observable<any> {
        return this.http.put(`${this.baseUrl}/services/${id}`, data);
    }

    toggleService(id: string): Observable<{ isActive: boolean }> {
        return this.http.patch<{ isActive: boolean }>(`${this.baseUrl}/services/${id}/toggle`, {});
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
    actionUrl?: string; // Added actionUrl
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
    actionUrl?: string; // Added actionUrl
}

export interface BroadcastNotificationRequest {
    userIds?: string[];
    role?: string;
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

// ==================== SERVICE CATALOG ====================
export interface ServiceItem {
    id: string;
    code: string;
    name: string;
    category: string;
    unitPrice: number;
    unit: string;
    description?: string;
    isActive: boolean;
}

export interface ServiceListResponse {
    items: ServiceItem[];
    total: number;
    page: number;
    pageSize: number;
}
