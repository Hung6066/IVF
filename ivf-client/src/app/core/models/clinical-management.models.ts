export interface ConsentFormDto {
  id: string;
  patientId: string;
  patientName: string;
  consentType: string;
  title: string;
  content: string;
  isSigned: boolean;
  signedAt?: string;
  witnessName?: string;
  isRevoked: boolean;
  revokedAt?: string;
  revocationReason?: string;
  cycleId?: string;
  createdAt: string;
}

export interface MedicationAdministrationDto {
  id: string;
  patientId: string;
  patientName: string;
  cycleId?: string;
  medicationName: string;
  dosage: string;
  route: string;
  administeredAt: string;
  administeredByUserId: string;
  isTriggerShot: boolean;
  isSkipped: boolean;
  isRefused: boolean;
  notes?: string;
}

export interface CycleFeeDto {
  id: string;
  cycleId: string;
  patientId: string;
  feeType: string;
  description: string;
  amount: number;
  paidAmount: number;
  balanceDue: number;
  status: string;
  isOneTimePerCycle: boolean;
  invoiceId?: string;
  createdAt: string;
}

export type DrugCategory =
  | 'Gonadotropin'
  | 'GnRH'
  | 'Progesterone'
  | 'Estrogen'
  | 'Trigger'
  | 'Antibiotic'
  | 'Supplement'
  | 'Other';

export interface DrugCatalogDto {
  id: string;
  code: string;
  name: string;
  genericName: string;
  category: DrugCategory;
  unit: string;
  activeIngredient?: string;
  defaultDosage?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
}

export type PrescriptionCycleType = 'IVF' | 'IUI' | 'FET' | 'General';

export interface PrescriptionTemplateItemDto {
  id: string;
  drugName: string;
  drugCode?: string;
  dosage: string;
  frequency: string;
  duration: string;
  sortOrder: number;
}

export interface PrescriptionTemplateDto {
  id: string;
  name: string;
  cycleType: PrescriptionCycleType;
  createdByDoctorId: string;
  doctorName: string;
  description?: string;
  isActive: boolean;
  items: PrescriptionTemplateItemDto[];
  createdAt: string;
}

export type FileStatus = 'InStorage' | 'CheckedOut' | 'InTransit' | 'Lost' | 'Archived';

export interface FileTransferDto {
  id: string;
  fromLocation: string;
  toLocation: string;
  transferredByUserId: string;
  reason?: string;
  transferredAt: string;
}

export interface FileTrackingDto {
  id: string;
  patientId: string;
  patientName: string;
  fileCode: string;
  currentLocation: string;
  status: FileStatus;
  notes?: string;
  transfers: FileTransferDto[];
  createdAt: string;
}

export type MatchStatus = 'Pending' | 'Matched' | 'InProgress' | 'Completed' | 'Cancelled';

export interface EggDonorRecipientDto {
  id: string;
  eggDonorId: string;
  donorCode: string;
  recipientCoupleId: string;
  recipientCoupleName: string;
  status: MatchStatus;
  matchedByUserId: string;
  treatmentCycleId?: string;
  matchedAt: string;
  completedAt?: string;
  notes?: string;
}

export type InventoryRequestType = 'Restock' | 'Usage' | 'PurchaseOrder' | 'Return';
export type InventoryRequestStatus =
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'Fulfilled'
  | 'Cancelled';

export interface InventoryRequestDto {
  id: string;
  requestType: InventoryRequestType;
  status: InventoryRequestStatus;
  requestedByUserId: string;
  requesterName: string;
  itemName: string;
  quantity: number;
  unit: string;
  notes?: string;
  approvedByUserId?: string;
  approvedAt?: string;
  rejectionReason?: string;
  fulfilledByUserId?: string;
  fulfilledAt?: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
