import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Notification, CreateNotificationRequest, BroadcastNotificationRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class NotificationService {
    private http = inject(HttpClient);
    private readonly baseUrl = environment.apiUrl;

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
}
