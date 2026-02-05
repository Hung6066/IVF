import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Appointment, CreateAppointmentRequest } from '../models/appointment.models';

@Injectable({ providedIn: 'root' })
export class AppointmentService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

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
}
