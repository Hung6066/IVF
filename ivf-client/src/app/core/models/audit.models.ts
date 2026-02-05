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
