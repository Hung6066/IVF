export type ConsentType =
  | 'OPU'
  | 'IUI'
  | 'Anesthesia'
  | 'EggDonation'
  | 'SpermDonation'
  | 'FET'
  | 'General';

export interface ConsentFormDto {
  id: string;
  patientId: string;
  patientName: string;
  consentType: ConsentType;
  title: string;
  content: string;
  isSigned: boolean;
  signedAt?: string;
  signedByName?: string;
  witnessName?: string;
  scanDocumentUrl?: string;
  isRevoked: boolean;
  revokedAt?: string;
  revocationReason?: string;
  cycleId?: string;
  createdAt: string;
}

export interface CreateConsentFormRequest {
  patientId: string;
  cycleId?: string;
  consentType: ConsentType;
  title: string;
  content: string;
}

export interface SignConsentFormRequest {
  signedByUserId: string;
  witnessName?: string;
}
