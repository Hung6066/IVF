export interface QueueTicket {
    id: string;
    ticketNumber: string;
    patientId: string;
    patientName?: string;
    departmentCode: string;
    status: TicketStatus;
    issuedAt: string;
    calledAt?: string;
    completedAt?: string;
}

export type TicketStatus = 'Waiting' | 'Called' | 'InService' | 'Completed' | 'Skipped' | 'Cancelled';
