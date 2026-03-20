export interface FetProtocolDto {
  id: string;
  cycleId: string;
  prepType: string;
  startDate?: string;
  cycleDay: number;
  estrogenDrug?: string;
  estrogenDose?: string;
  estrogenStartDate?: string;
  progesteroneDrug?: string;
  progesteroneDose?: string;
  progesteroneStartDate?: string;
  endometriumThickness?: number;
  endometriumPattern?: string;
  endometriumCheckDate?: string;
  embryosToThaw: number;
  embryosSurvived: number;
  thawDate?: string;
  embryoGrade?: string;
  embryoAge: number;
  plannedTransferDate?: string;
  notes?: string;
  status: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateFetProtocolRequest {
  cycleId: string;
  prepType: string;
  startDate?: string;
  cycleDay?: number;
  notes?: string;
}

export interface UpdateHormoneTherapyRequest {
  estrogenDrug?: string;
  estrogenDose?: string;
  estrogenStartDate?: string;
  progesteroneDrug?: string;
  progesteroneDose?: string;
  progesteroneStartDate?: string;
}

export interface RecordEndometriumCheckRequest {
  thickness: number;
  pattern?: string;
  checkDate: string;
}

export interface RecordThawingRequest {
  embryosToThaw: number;
  embryosSurvived: number;
  thawDate: string;
  embryoGrade?: string;
  embryoAge: number;
}

export interface ScheduleTransferRequest {
  transferDate: string;
}

export interface FetSearchResult {
  items: FetProtocolDto[];
  total: number;
  page: number;
  pageSize: number;
}
