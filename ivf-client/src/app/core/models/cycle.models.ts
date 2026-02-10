export interface TreatmentCycle {
    id: string;
    cycleCode: string;
    coupleId: string;
    method: 'QHTN' | 'IUI' | 'ICSI' | 'IVM';
    currentPhase: CyclePhase;
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

export interface StimulationDrug {
    drugName: string;
    duration: number;
    posology?: string;
    sortOrder: number;
}

export interface StimulationData {
    id: string;
    cycleId: string;
    lastMenstruation?: string;
    startDate?: string;
    startDay?: number;
    drugs: StimulationDrug[];
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

export interface CultureData {
    id: string;
    cycleId: string;
    totalFreezedEmbryo: number;
    totalThawedEmbryo: number;
    totalTransferedEmbryo: number;
    remainFreezedEmbryo: number;
}

export interface TransferData {
    id: string;
    cycleId: string;
    transferDate?: string;
    thawingDate?: string;
    dayOfTransfered: number;
    labNote?: string;
}

export interface LutealPhaseDrug {
    drugName: string;
    category: 'Luteal' | 'Endometrium';
    sortOrder: number;
}

export interface LutealPhaseData {
    id: string;
    cycleId: string;
    drugs: LutealPhaseDrug[];
}

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

export interface BirthOutcome {
    gender: string;
    weight?: number;
    isLiveBirth: boolean;
    sortOrder: number;
}

export interface BirthData {
    id: string;
    cycleId: string;
    deliveryDate?: string;
    gestationalWeeks: number;
    deliveryMethod?: string;
    liveBirths: number;
    stillbirths: number;
    outcomes: BirthOutcome[];
    complications?: string;
}

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
