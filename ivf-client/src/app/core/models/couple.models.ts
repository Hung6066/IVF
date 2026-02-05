import { Patient } from './patient.models';

export interface Couple {
    id: string;
    wife: Patient;
    husband: Patient;
    marriageDate?: string;
    infertilityYears?: number;
    spermDonorId?: string;
}
