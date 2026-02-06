import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PatientPhotoDto {
    id: string;
    patientId: string;
    contentType: string;
    fileName: string;
    uploadedAt: string;
    sizeBytes: number;
}

export interface PatientFingerprintDto {
    id: string;
    patientId: string;
    fingerType: string;
    sdkType: string;
    quality: number;
    capturedAt: string;
}

export interface RegisterFingerprintRequest {
    fingerprintDataBase64: string;
    fingerType: FingerprintType;
    sdkType: FingerprintSdkType;
    quality: number;
}

export enum FingerprintType {
    LeftThumb = 1,
    LeftIndex = 2,
    LeftMiddle = 3,
    LeftRing = 4,
    LeftPinky = 5,
    RightThumb = 6,
    RightIndex = 7,
    RightMiddle = 8,
    RightRing = 9,
    RightPinky = 10
}

export enum FingerprintSdkType {
    DigitalPersona = 1,
    SecuGen = 2
}

@Injectable({ providedIn: 'root' })
export class PatientBiometricsService {
    private http = inject(HttpClient);
    private baseUrl = `${environment.apiUrl}/patients`;

    // ==================== Photo ====================

    uploadPhoto(patientId: string, file: File): Observable<PatientPhotoDto> {
        const formData = new FormData();
        formData.append('file', file);
        return this.http.post<PatientPhotoDto>(`${this.baseUrl}/${patientId}/photo`, formData);
    }

    getPhotoUrl(patientId: string): string {
        return `${this.baseUrl}/${patientId}/photo`;
    }

    /**
     * Fetch photo as Blob with authentication headers.
     * Use this method to load images that require JWT authentication.
     */
    getPhotoBlob(patientId: string): Observable<Blob> {
        return this.http.get(`${this.baseUrl}/${patientId}/photo`, { responseType: 'blob' });
    }

    deletePhoto(patientId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/${patientId}/photo`);
    }

    // ==================== Fingerprints ====================

    registerFingerprint(patientId: string, request: RegisterFingerprintRequest): Observable<PatientFingerprintDto> {
        return this.http.post<PatientFingerprintDto>(`${this.baseUrl}/${patientId}/fingerprints`, request);
    }

    getFingerprints(patientId: string): Observable<PatientFingerprintDto[]> {
        return this.http.get<PatientFingerprintDto[]>(`${this.baseUrl}/${patientId}/fingerprints`);
    }

    deleteFingerprint(fingerprintId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/fingerprints/${fingerprintId}`);
    }

    // ==================== Helpers ====================

    getFingerTypeName(type: string | FingerprintType): string {
        const names: Record<string, string> = {
            'LeftThumb': 'Ngón cái trái', 'LeftIndex': 'Ngón trỏ trái',
            'LeftMiddle': 'Ngón giữa trái', 'LeftRing': 'Ngón áp út trái',
            'LeftPinky': 'Ngón út trái', 'RightThumb': 'Ngón cái phải',
            'RightIndex': 'Ngón trỏ phải', 'RightMiddle': 'Ngón giữa phải',
            'RightRing': 'Ngón áp út phải', 'RightPinky': 'Ngón út phải'
        };
        return names[type.toString()] || type.toString();
    }

    getSdkTypeName(type: string | FingerprintSdkType): string {
        const names: Record<string, string> = {
            'DigitalPersona': 'DigitalPersona',
            'SecuGen': 'SecuGen'
        };
        return names[type.toString()] || type.toString();
    }
}
