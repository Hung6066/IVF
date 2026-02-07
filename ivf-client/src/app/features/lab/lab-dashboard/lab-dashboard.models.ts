import { ScheduleType, ScheduleStatus } from '../../../core/constants/lab.constants';

export interface EmbryoCard {
    id: string;
    cycleId: string;
    cycleCode: string;
    patientName: string;
    embryoNumber: number;
    grade: string;
    day: string;
    status: string;
    fertilizationDate: string;
    location?: string;
    notes?: string;
}

export interface ScheduleItem {
    id: string;
    time: string;
    patientName: string;
    cycleCode: string;
    procedure: string;
    type: ScheduleType;
    status: ScheduleStatus;
}

export interface CryoLocation {
    tank: string;
    canister: number;
    cane: number;
    goblet: number;
    available: number;
    used: number;
    specimenType?: number;
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

export interface EmbryoReport {
    cycleCode: string;
    patientName: string;
    aspirationDate: string;
    totalEggs: number;
    mII: number;
    twoPN: number;
    d3: number;
    d5D6: string;
    transferred: number;
    frozen: number;
    status: string;
}
