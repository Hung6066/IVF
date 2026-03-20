// Prescription models matching backend DTOs

export interface PrescriptionDto {
  id: string;
  patientId: string;
  patientName: string;
  patientCode: string;
  cycleId?: string;
  doctorId: string;
  doctorName: string;
  prescriptionDate: string;
  status: string;
  enteredAt?: string;
  printedAt?: string;
  dispensedAt?: string;
  notes?: string;
  waiveConsultationFee: boolean;
  createdAt: string;
  items: PrescriptionItemDto[];
}

export interface PrescriptionItemDto {
  id: string;
  drugCode?: string;
  drugName: string;
  dosage?: string;
  frequency?: string;
  duration?: string;
  quantity: number;
}

export interface PrescriptionItemInput {
  drugName: string;
  quantity: number;
  drugCode?: string;
  dosage?: string;
  frequency?: string;
  duration?: string;
}

export interface CreatePrescriptionRequest {
  patientId: string;
  doctorId: string;
  prescriptionDate: string;
  cycleId?: string;
  notes?: string;
  templateId?: string;
  waiveConsultationFee: boolean;
  items: PrescriptionItemInput[];
}

export interface PrescriptionStatistics {
  todayCount: number;
  pendingCount: number;
}

export interface PrescriptionSearchResult {
  items: PrescriptionDto[];
  total: number;
}
