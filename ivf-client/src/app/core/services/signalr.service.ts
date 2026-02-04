import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';

export interface SignalRNotification {
    id: string;
    userId: string;
    title: string;
    message: string;
    type: string;
    isRead: boolean;
    entityType?: string;
    entityId?: string;
    createdAt: string;
}

@Injectable({
    providedIn: 'root'
})
export class SignalRService {
    private notificationHubConnection?: signalR.HubConnection;
    private queueHubConnection?: signalR.HubConnection;

    private notificationSubject = new BehaviorSubject<SignalRNotification | null>(null);
    private queueUpdateSubject = new BehaviorSubject<any>(null);

    public notification$ = this.notificationSubject.asObservable();
    public queueUpdate$ = this.queueUpdateSubject.asObservable();

    private readonly baseUrl = 'http://localhost:5000';

    constructor() { }

    async startNotificationConnection(token: string): Promise<void> {
        this.notificationHubConnection = new signalR.HubConnectionBuilder()
            .withUrl(`${this.baseUrl}/hubs/notifications`, {
                accessTokenFactory: () => token,
                skipNegotiation: false,
                transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.notificationHubConnection.on('ReceiveNotification', (notification: SignalRNotification) => {
            console.log('📩 Received notification:', notification);
            this.notificationSubject.next(notification);
        });

        this.notificationHubConnection.onreconnecting(() => {
            console.log('🔄 Reconnecting to NotificationHub...');
        });

        this.notificationHubConnection.onreconnected(() => {
            console.log('✅ Reconnected to NotificationHub');
        });

        this.notificationHubConnection.onclose(() => {
            console.log('❌ NotificationHub connection closed');
        });

        try {
            await this.notificationHubConnection.start();
            console.log('✅ Connected to NotificationHub');
        } catch (err) {
            console.error('❌ NotificationHub connection error:', err);
            setTimeout(() => this.startNotificationConnection(token), 5000);
        }
    }

    async startQueueConnection(token: string): Promise<void> {
        this.queueHubConnection = new signalR.HubConnectionBuilder()
            .withUrl(`${this.baseUrl}/hubs/queue`, {
                accessTokenFactory: () => token,
                skipNegotiation: false,
                transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.queueHubConnection.on('QueueUpdated', (update: any) => {
            console.log('🎫 Queue updated:', update);
            this.queueUpdateSubject.next(update);
        });

        this.queueHubConnection.on('TicketCalled', (ticket: any) => {
            console.log('🔔 Ticket called:', ticket);
            this.queueUpdateSubject.next({ type: 'called', ticket });
        });

        this.queueHubConnection.onreconnecting(() => {
            console.log('🔄 Reconnecting to QueueHub...');
        });

        this.queueHubConnection.onreconnected(() => {
            console.log('✅ Reconnected to QueueHub');
        });

        this.queueHubConnection.onclose(() => {
            console.log('❌ QueueHub connection closed');
        });

        try {
            await this.queueHubConnection.start();
            console.log('✅ Connected to QueueHub');
        } catch (err) {
            console.error('❌ QueueHub connection error:', err);
            setTimeout(() => this.startQueueConnection(token), 5000);
        }
    }

    async stopConnections(): Promise<void> {
        if (this.notificationHubConnection) {
            await this.notificationHubConnection.stop();
        }
        if (this.queueHubConnection) {
            await this.queueHubConnection.stop();
        }
    }

    getNotificationConnectionState(): signalR.HubConnectionState | undefined {
        return this.notificationHubConnection?.state;
    }

    getQueueConnectionState(): signalR.HubConnectionState | undefined {
        return this.queueHubConnection?.state;
    }
}
