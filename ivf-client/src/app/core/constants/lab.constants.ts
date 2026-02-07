// Lab Schedule Constants
export const ScheduleTypes = {
    RETRIEVAL: 'retrieval' as const,
    TRANSFER: 'transfer' as const,
    REPORT: 'report' as const
};

export const ScheduleStatuses = {
    PENDING: 'pending' as const,
    DONE: 'done' as const
};

// Type for schedule type
export type ScheduleType = typeof ScheduleTypes[keyof typeof ScheduleTypes];

// Type for schedule status  
export type ScheduleStatus = typeof ScheduleStatuses[keyof typeof ScheduleStatuses];
