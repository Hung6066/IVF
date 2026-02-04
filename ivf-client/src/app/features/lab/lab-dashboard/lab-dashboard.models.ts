export interface EmbryoCard {
    id: string;
    cycleCode: string;
    patientName: string;
    embryoNumber: number;
    grade: string;
    day: string;
    status: string;
    location?: string;
    notes?: string;
}

export interface ScheduleItem {
    id: string;
    time: string;
    patientName: string;
    cycleCode: string;
    procedure: string;
    type: 'retrieval' | 'transfer' | 'report';
    status: 'pending' | 'done';
}

export interface CryoLocation {
    tank: string;
    canister: number;
    cane: number;
    goblet: number;
    available: number;
    used: number;
}

export interface QueueItem {
    id: string;
    number: string;
    patientName: string;
    patientCode: string;
    issueTime: string;
    status: 'Waiting' | 'Called' | 'InService' | string;
}

export interface LabStats {
    eggRetrievalCount: number;
    cultureCount: number;
    transferCount: number;
    freezeCount: number;
    totalFrozenEmbryos: number;
    totalFrozenEggs: number;
    totalFrozenSperm: number;
}
