// Auth Models
export interface LoginRequest {
    username: string;
    password: string;
}

export interface AuthResponse {
    accessToken: string;
    refreshToken: string;
    expiresIn: number;
    user: User;
}

export interface User {
    id: string;
    username: string;
    fullName: string;
    role: string;
    department?: string;
}

// Patient Models
export interface Patient {
    id: string;
    patientCode: string;
    fullName: string;
    dateOfBirth: string;
    gender: 'Male' | 'Female';
    identityNumber?: string;
    phone?: string;
    email?: string;
    address?: string;
    patientType: 'Infertility' | 'EggDonor' | 'SpermDonor';
    createdAt: string;
}

export interface PatientListResponse {
    items: Patient[];
    total: number;
    page: number;
    pageSize: number;
}

// Couple Models
export interface Couple {
    id: string;
    wife: Patient;
    husband: Patient;
    marriageDate?: string;
    infertilityYears?: number;
    spermDonorId?: string;
}

// Cycle Models
export interface TreatmentCycle {
    id: string;
    cycleCode: string;
    coupleId: string;
    method: 'QHTN' | 'IUI' | 'ICSI' | 'IVM';
    phase: CyclePhase;
    outcome: CycleOutcome;
    startDate: string;
    endDate?: string;
    notes?: string;
}

export type CyclePhase =
    | 'Consultation' | 'OvarianStimulation' | 'TriggerShot'
    | 'EggRetrieval' | 'EmbryoCulture' | 'EmbryoTransfer'
    | 'LutealSupport' | 'PregnancyTest' | 'Completed';

export type CycleOutcome = 'Ongoing' | 'Pregnant' | 'NotPregnant' | 'Cancelled' | 'FrozenAll';

// Queue Models
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

// Ultrasound Models
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

// Invoice Models
export interface Invoice {
    id: string;
    invoiceNumber: string;
    patientId: string;
    status: InvoiceStatus;
    invoiceDate: string;
    subTotal: number;
    discountAmount: number;
    taxAmount: number;
    totalAmount: number;
    paidAmount: number;
    items?: InvoiceItem[];
}

export interface InvoiceItem {
    id: string;
    serviceCode: string;
    description: string;
    quantity: number;
    unitPrice: number;
    amount: number;
}

export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Refunded' | 'Cancelled';

// Report Models
export interface DashboardStats {
    totalPatients: number;
    activeCycles: number;
    todayQueueCount: number;
    monthlyRevenue: number;
}

export interface CycleSuccessRates {
    year: number;
    totalCycles: number;
    pregnancies: number;
    notPregnant: number;
    cancelled: number;
    frozenAll: number;
    successRate: number;
}

export interface MonthlyRevenue {
    month: number;
    revenue: number;
}
