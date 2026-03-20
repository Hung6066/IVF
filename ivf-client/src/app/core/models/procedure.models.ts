export interface ProcedureDto {
  id: string;
  patientId: string;
  patientName: string;
  patientCode: string;
  cycleId?: string;
  performedByDoctorId: string;
  performedByDoctorName: string;
  assistantDoctorId?: string;
  assistantDoctorName?: string;
  procedureType: string;
  procedureCode?: string;
  procedureName: string;
  scheduledAt: string;
  startedAt?: string;
  completedAt?: string;
  durationMinutes?: number;
  anesthesiaType?: string;
  anesthesiaNotes?: string;
  roomNumber?: string;
  preOpNotes?: string;
  intraOpFindings?: string;
  postOpNotes?: string;
  complications?: string;
  status: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateProcedureRequest {
  patientId: string;
  performedByDoctorId: string;
  procedureType: string;
  procedureName: string;
  scheduledAt: string;
  cycleId?: string;
  assistantDoctorId?: string;
  procedureCode?: string;
  anesthesiaType?: string;
  roomNumber?: string;
  preOpNotes?: string;
}

export interface CompleteProcedureRequest {
  intraOpFindings?: string;
  postOpNotes?: string;
  complications?: string;
  durationMinutes?: number;
}

export interface PostponeProcedureRequest {
  newScheduledAt: string;
  reason?: string;
}

export interface CancelProcedureRequest {
  reason?: string;
}

export interface ProcedureSearchResult {
  items: ProcedureDto[];
  total: number;
  page: number;
  pageSize: number;
}
