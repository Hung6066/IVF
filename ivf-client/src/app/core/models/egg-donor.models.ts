export interface EggDonorDto {
  id: string;
  donorCode: string;
  patientId: string;
  patientName: string;
  status: string;
  bloodType?: string;
  height?: number;
  weight?: number;
  ethnicity?: string;
  amhLevel?: number;
  antralFollicleCount?: number;
  totalDonations: number;
  successfulPregnancies: number;
  lastDonationDate?: string;
  screeningDate?: string;
  createdAt: string;
}

export interface OocyteSampleDto {
  id: string;
  donorId: string;
  sampleCode: string;
  collectionDate: string;
  totalOocytes?: number;
  matureOocytes?: number;
  immatureOocytes?: number;
  degeneratedOocytes?: number;
  vitrifiedCount?: number;
  isAvailable: boolean;
  freezeDate?: string;
  thawDate?: string;
  survivedAfterThaw?: number;
  notes?: string;
  createdAt: string;
}

export interface CreateEggDonorRequest {
  patientId: string;
}

export interface UpdateEggDonorProfileRequest {
  bloodType?: string;
  height?: number;
  weight?: number;
  eyeColor?: string;
  hairColor?: string;
  ethnicity?: string;
  education?: string;
  occupation?: string;
  amhLevel?: number;
  antralFollicleCount?: number;
  menstrualHistory?: string;
}

export interface CreateOocyteSampleRequest {
  donorId: string;
  collectionDate: string;
}

export interface RecordOocyteQualityRequest {
  totalOocytes?: number;
  matureOocytes?: number;
  immatureOocytes?: number;
  degeneratedOocytes?: number;
  notes?: string;
}

export interface VitrifyOocytesRequest {
  count: number;
  freezeDate: string;
  cryoLocationId?: string;
}

export interface EggDonorSearchResult {
  items: EggDonorDto[];
  total: number;
  page: number;
  pageSize: number;
}
