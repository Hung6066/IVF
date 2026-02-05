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

// Extended Cycle Detail
export interface TreatmentCycleDetail extends TreatmentCycle {
    opuNo?: number;
    transferNo?: number;
    stimulationNo?: number;
    room?: string;
    etIuiDoctor?: string;
    treatBothWifeAndEggDonor?: boolean;
    betaHcg?: number;
    ctrlNote?: string;
    stopReason?: string;
    wifeName?: string;
    husbandName?: string;
    ultrasoundCount?: number;
    embryoCount?: number;
    // Phase data
    indication?: TreatmentIndication;
    stimulation?: StimulationData;
    culture?: CultureData;
    transfer?: TransferData;
    lutealPhase?: LutealPhaseData;
    pregnancy?: PregnancyData;
    birth?: BirthData;
    adverseEvents?: AdverseEventData[];
}

// Tab 1: Treatment Indication
export interface TreatmentIndication {
    id: string;
    cycleId: string;
    lastMenstruation?: string;
    treatmentType?: string;
    regimen?: string;
    freezeAll: boolean;
    sis: boolean;
    wifeDiagnosis?: string;
    wifeDiagnosis2?: string;
    husbandDiagnosis?: string;
    husbandDiagnosis2?: string;
    ultrasoundDoctorId?: string;
    indicationDoctorId?: string;
    fshDoctorId?: string;
    midwifeId?: string;
    timelapse: boolean;
    pgtA: boolean;
    pgtSr: boolean;
    pgtM: boolean;
    subType?: string;
    scientificResearch?: string;
    source?: string;
    procedurePlace?: string;
    stopReason?: string;
    treatmentMonth?: string;
    previousTreatmentsAtSite: number;
    previousTreatmentsOther: number;
}

// Tab 2: Stimulation & Trigger
export interface StimulationData {
    id: string;
    cycleId: string;
    lastMenstruation?: string;
    startDate?: string;
    startDay?: number;
    drug1?: string;
    drug1Duration: number;
    drug1Posology?: string;
    drug2?: string;
    drug2Duration: number;
    drug2Posology?: string;
    drug3?: string;
    drug3Duration: number;
    drug3Posology?: string;
    drug4?: string;
    drug4Duration: number;
    drug4Posology?: string;
    size12Follicle?: number;
    size14Follicle?: number;
    endometriumThickness?: number;
    triggerDrug?: string;
    triggerDrug2?: string;
    hcgDate?: string;
    hcgDate2?: string;
    hcgTime?: string;
    hcgTime2?: string;
    lhLab?: number;
    e2Lab?: number;
    p4Lab?: number;
    procedureType?: string;
    aspirationDate?: string;
    procedureDate?: string;
    aspirationNo?: number;
    techniqueWife?: string;
    techniqueHusband?: string;
}

// Tab 3: Culture
export interface CultureData {
    id: string;
    cycleId: string;
    totalFreezedEmbryo: number;
    totalThawedEmbryo: number;
    totalTransferedEmbryo: number;
    remainFreezedEmbryo: number;
}

// Tab 4: Transfer
export interface TransferData {
    id: string;
    cycleId: string;
    transferDate?: string;
    thawingDate?: string;
    dayOfTransfered: number;
    labNote?: string;
}

// Tab 5: Luteal Phase
export interface LutealPhaseData {
    id: string;
    cycleId: string;
    lutealDrug1?: string;
    lutealDrug2?: string;
    endometriumDrug1?: string;
    endometriumDrug2?: string;
}

// Tab 6: Pregnancy
export interface PregnancyData {
    id: string;
    cycleId: string;
    betaHcg?: number;
    betaHcgDate?: string;
    isPregnant: boolean;
    gestationalSacs?: number;
    fetalHeartbeats?: number;
    dueDate?: string;
    notes?: string;
}

// Tab 7: Birth
export interface BirthData {
    id: string;
    cycleId: string;
    deliveryDate?: string;
    gestationalWeeks: number;
    deliveryMethod?: string;
    liveBirths: number;
    stillbirths: number;
    babyGenders?: string;
    birthWeights?: string;
    complications?: string;
}

// Tab 8: Adverse Events
export interface AdverseEventData {
    id: string;
    cycleId: string;
    eventDate?: string;
    eventType?: string;
    severity?: string;
    description?: string;
    treatment?: string;
    outcome?: string;
}

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
