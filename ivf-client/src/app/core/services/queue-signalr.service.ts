import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

// Note: Install @microsoft/signalr package for production use
// npm install @microsoft/signalr

export interface QueueTicket {
    id: string;
    ticketNumber: string;
    patientName?: string;
    departmentCode: string;
    status: string;
    issuedAt: string;
    calledAt?: string;
}

@Injectable({ providedIn: 'root' })
export class QueueSignalRService {
    private connection: any = null;
    private hubUrl = environment.apiUrl.replace('/api', '/hubs/queue');

    // Observable signals for queue updates
    currentTicket = signal<QueueTicket | null>(null);
    waitingTickets = signal<QueueTicket[]>([]);
    connected = signal(false);

    async connect(departmentCode: string): Promise<void> {
        // Dynamic import to avoid SSR issues
        const signalR = await import('@microsoft/signalr');

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl, {
                accessTokenFactory: () => localStorage.getItem('token') || ''
            })
            .withAutomaticReconnect()
            .build();

        // Register event handlers
        this.connection.on('TicketIssued', (ticket: QueueTicket) => {
            this.waitingTickets.update(list => [...list, ticket]);
        });

        this.connection.on('TicketCalled', (ticket: QueueTicket) => {
            this.currentTicket.set(ticket);
            this.waitingTickets.update(list => list.filter(t => t.id !== ticket.id));
        });

        this.connection.on('TicketCompleted', (ticketId: string) => {
            if (this.currentTicket()?.id === ticketId) {
                this.currentTicket.set(null);
            }
            this.waitingTickets.update(list => list.filter(t => t.id !== ticketId));
        });

        this.connection.on('TicketSkipped', (ticketId: string) => {
            if (this.currentTicket()?.id === ticketId) {
                this.currentTicket.set(null);
            }
            this.waitingTickets.update(list => list.filter(t => t.id !== ticketId));
        });

        // Start connection
        try {
            await this.connection.start();
            await this.connection.invoke('JoinDepartment', departmentCode);
            this.connected.set(true);
            console.log('SignalR connected to queue hub');
        } catch (err) {
            console.error('SignalR connection error:', err);
            this.connected.set(false);
        }
    }

    async disconnect(departmentCode: string): Promise<void> {
        if (this.connection) {
            try {
                await this.connection.invoke('LeaveDepartment', departmentCode);
                await this.connection.stop();
            } catch (err) {
                console.error('SignalR disconnect error:', err);
            }
            this.connection = null;
            this.connected.set(false);
        }
    }

    isConnected(): boolean {
        return this.connected();
    }
}
