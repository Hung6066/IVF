// Consultation models matching backend DTOs

export interface ConsultationDto {
  id: string;
  patientId: string;
  patientName: string;
  patientCode: string;
  doctorId: string;
  doctorName: string;
  cycleId?: string;
  consultationType: string;
  consultationDate: string;
  status: string;
  chiefComplaint?: string;
  medicalHistory?: string;
  pastHistory?: string;
  surgicalHistory?: string;
  familyHistory?: string;
  obstetricHistory?: string;
  menstrualHistory?: string;
  physicalExamination?: string;
  diagnosis?: string;
  treatmentPlan?: string;
  recommendedMethod?: string;
  notes?: string;
  waiveConsultationFee: boolean;
  createdAt: string;
}

export interface CreateConsultationRequest {
  patientId: string;
  doctorId: string;
  consultationDate: string;
  consultationType: string;
  cycleId?: string;
  chiefComplaint?: string;
  notes?: string;
  waiveConsultationFee: boolean;
}

export interface RecordClinicalDataRequest {
  chiefComplaint?: string;
  medicalHistory?: string;
  pastHistory?: string;
  surgicalHistory?: string;
  familyHistory?: string;
  obstetricHistory?: string;
  menstrualHistory?: string;
  physicalExamination?: string;
}

export interface RecordDiagnosisRequest {
  diagnosis?: string;
  treatmentPlan?: string;
  recommendedMethod?: string;
}

export interface ConsultationSearchResult {
  items: ConsultationDto[];
  total: number;
}
