export interface Ultrasound {
    id: string;
    cycleId: string;
    examDate: string;
    dayOfCycle?: number;
    leftOvaryCount?: number;
    rightOvaryCount?: number;
    leftFollicles?: string;
    rightFollicles?: string;
    endometriumThickness?: number;
    findings?: string;
}

// Embryo Models
export interface Embryo {
    id: string;
    cycleId: string;
    embryoNumber: number;
    grade: EmbryoGrade;
    day: EmbryoDay;
    status: EmbryoStatus;
    notes?: string;
}

export type EmbryoGrade = 'AA' | 'AB' | 'BA' | 'BB' | 'AC' | 'CA' | 'BC' | 'CB' | 'CC' | 'CD' | 'DC' | 'DD';
export type EmbryoDay = 'D1' | 'D2' | 'D3' | 'D4' | 'D5' | 'D6';
export type EmbryoStatus = 'Developing' | 'Transferred' | 'Frozen' | 'Thawed' | 'Discarded' | 'Arrested';
