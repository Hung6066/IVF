// Lab Order models matching backend DTOs

export interface LabOrderDto {
  id: string;
  patientId: string;
  patientName: string;
  patientCode: string;
  cycleId?: string;
  orderedByUserId: string;
  orderedByName: string;
  orderedAt: string;
  orderType: string;
  status: string;
  resultDeliveredTo?: string;
  completedAt?: string;
  deliveredAt?: string;
  notes?: string;
  createdAt: string;
  tests: LabTestDto[];
}

export interface LabTestDto {
  id: string;
  testCode: string;
  testName: string;
  resultValue?: string;
  resultUnit?: string;
  referenceRange?: string;
  isAbnormal: boolean;
  completedAt?: string;
  notes?: string;
}

export interface LabTestInput {
  testCode: string;
  testName: string;
  referenceRange?: string;
}

export interface LabTestResultInput {
  testId: string;
  resultValue: string;
  resultUnit?: string;
  isAbnormal: boolean;
  notes?: string;
}

export interface CreateLabOrderRequest {
  patientId: string;
  orderedByUserId: string;
  orderType: string;
  cycleId?: string;
  notes?: string;
  tests: LabTestInput[];
}

export interface EnterLabResultRequest {
  performedByUserId: string;
  results: LabTestResultInput[];
}

export interface DeliverLabResultRequest {
  deliveredByUserId: string;
  deliveredTo: string;
}

export interface LabOrderStatistics {
  orderedCount: number;
  inProgressCount: number;
  completedCount: number;
  deliveredCount: number;
}

export interface LabOrderSearchResult {
  items: LabOrderDto[];
  total: number;
}
