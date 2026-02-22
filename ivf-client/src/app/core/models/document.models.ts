export interface PatientDocument {
  id: string;
  patientId: string;
  patientCode?: string;
  patientName?: string;
  title: string;
  description?: string;
  documentType: string;
  status: string;
  confidentiality: string;
  bucketName: string;
  objectKey: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  checksum?: string;
  isSigned: boolean;
  signedByName?: string;
  signedAt?: string;
  signedObjectKey?: string;
  version: number;
  previousVersionId?: string;
  tags?: string;
  metadataJson?: string;
  cycleId?: string;
  formResponseId?: string;
  uploadedByUserId?: string;
  createdAt: string;
  updatedAt?: string;
  downloadUrl?: string;
}

export interface PatientDocumentListResponse {
  items: PatientDocument[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PatientDocumentSummary {
  patientId: string;
  patientCode: string;
  patientName: string;
  totalStorageBytes: number;
  totalDocuments: number;
  documentCountsByType: { [key: string]: number };
}

export interface DocumentDownloadUrl {
  url: string;
  fileName: string;
  contentType: string;
  expiresAt: string;
}

export interface DocumentType {
  value: string;
  label: string;
}

export interface StorageHealth {
  healthy: boolean;
  totalObjects?: number;
  totalSizeBytes?: number;
  totalSizeMB?: number;
  error?: string;
}
