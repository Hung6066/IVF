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
    actionUrl?: string;
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
    actionUrl?: string;
}

export interface BroadcastNotificationRequest {
    userIds?: string[];
    role?: string;
    title: string;
    message: string;
    type: NotificationType;
}
