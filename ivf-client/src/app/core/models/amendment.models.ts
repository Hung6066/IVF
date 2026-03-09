// ─── Document Amendment Models ───────────────────────────────

export interface AmendmentDto {
  id: string;
  formResponseId: string;
  version: number;
  status: AmendmentStatus;
  reason: string;
  reviewNotes?: string;
  requestedByUserId: string;
  requestedByName?: string;
  reviewedByUserId?: string;
  reviewedByName?: string;
  createdAt: string;
  reviewedAt?: string;
  fieldChanges: FieldChangeDto[];
}

export interface FieldChangeDto {
  id: string;
  formFieldId: string;
  fieldKey: string;
  fieldLabel: string;
  changeType: FieldChangeType;
  oldTextValue?: string;
  newTextValue?: string;
  oldNumericValue?: number;
  newNumericValue?: number;
  oldDateValue?: string;
  newDateValue?: string;
  oldBooleanValue?: boolean;
  newBooleanValue?: boolean;
  oldJsonValue?: string;
  newJsonValue?: string;
}

export type AmendmentStatus = 'Pending' | 'Approved' | 'Rejected';
export type FieldChangeType = 'Modified' | 'Added' | 'Removed';

export interface CreateAmendmentRequest {
  reason: string;
  fieldChanges: FieldChangeRequest[];
}

export interface FieldChangeRequest {
  formFieldId: string;
  newTextValue?: string;
  newNumericValue?: number;
  newDateValue?: string;
  newBooleanValue?: boolean;
  newJsonValue?: string;
}

export interface ApproveRejectRequest {
  notes?: string;
}

export interface AmendmentApproveResult {
  amendment: AmendmentDto;
  message: string;
}

export interface PendingAmendmentsResponse {
  items: AmendmentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}
