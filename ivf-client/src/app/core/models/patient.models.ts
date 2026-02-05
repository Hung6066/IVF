export interface Patient {
    id: string;
    patientCode: string;
    fullName: string;
    dateOfBirth: string;
    gender: 'Male' | 'Female';
    identityNumber?: string;
    phone?: string;
    email?: string;
    address?: string;
    patientType: 'Infertility' | 'EggDonor' | 'SpermDonor';
    createdAt: string;
}

export interface PatientListResponse {
    items: Patient[];
    total: number;
    page: number;
    pageSize: number;
}
