import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class UserService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

    // ==================== USERS ====================
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

    // ==================== DOCTORS (Users with Doctor role) ====================
    searchDoctors(query?: string): Observable<any[]> {
        let params = new HttpParams();
        if (query) params = params.set('q', query);
        return this.http.get<any[]>(`${this.baseUrl}/doctors`, { params });
    }

    createDoctor(data: { userId: string, specialty: string, licenseNumber?: string, roomNumber?: string, maxPatientsPerDay?: number }): Observable<any> {
        return this.http.post(`${this.baseUrl}/doctors`, data);
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
}
