import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PatientDocument,
  PatientDocumentListResponse,
  PatientDocumentSummary,
  DocumentDownloadUrl,
  DocumentType,
  StorageHealth,
} from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/documents`;

  // ─── Upload document ───
  uploadDocument(
    file: File,
    patientId: string,
    title: string,
    documentType: string,
    options?: {
      description?: string;
      cycleId?: string;
      formResponseId?: string;
      confidentiality?: string;
      tags?: string;
    },
  ): Observable<PatientDocument> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('patientId', patientId);
    formData.append('title', title);
    formData.append('documentType', documentType);

    if (options?.description) formData.append('description', options.description);
    if (options?.cycleId) formData.append('cycleId', options.cycleId);
    if (options?.formResponseId) formData.append('formResponseId', options.formResponseId);
    if (options?.confidentiality) formData.append('confidentiality', options.confidentiality);
    if (options?.tags) formData.append('tags', options.tags);

    return this.http.post<PatientDocument>(`${this.baseUrl}/upload`, formData);
  }

  // ─── Get document by ID ───
  getDocument(id: string): Observable<PatientDocument> {
    return this.http.get<PatientDocument>(`${this.baseUrl}/${id}`);
  }

  // ─── Get documents for a patient ───
  getPatientDocuments(
    patientId: string,
    options?: {
      type?: string;
      status?: string;
      search?: string;
      page?: number;
      pageSize?: number;
    },
  ): Observable<PatientDocumentListResponse> {
    let params = new HttpParams();
    if (options?.type) params = params.set('type', options.type);
    if (options?.status) params = params.set('status', options.status);
    if (options?.search) params = params.set('search', options.search);
    if (options?.page) params = params.set('page', options.page);
    if (options?.pageSize) params = params.set('pageSize', options.pageSize);

    return this.http.get<PatientDocumentListResponse>(`${this.baseUrl}/patient/${patientId}`, {
      params,
    });
  }

  // ─── Get patient document summary ───
  getPatientSummary(patientId: string): Observable<PatientDocumentSummary> {
    return this.http.get<PatientDocumentSummary>(`${this.baseUrl}/patient/${patientId}/summary`);
  }

  // ─── Get presigned download URL ───
  getDownloadUrl(id: string, signed = false, expiry = 3600): Observable<DocumentDownloadUrl> {
    let params = new HttpParams().set('signed', signed).set('expiry', expiry);
    return this.http.get<DocumentDownloadUrl>(`${this.baseUrl}/${id}/download-url`, { params });
  }

  // ─── Download file directly ───
  downloadFile(id: string, signed = false): Observable<Blob> {
    let params = new HttpParams().set('signed', signed);
    return this.http.get(`${this.baseUrl}/${id}/download`, {
      params,
      responseType: 'blob',
    });
  }

  // ─── Update document metadata ───
  updateDocument(
    id: string,
    data: {
      title?: string;
      description?: string;
      tags?: string;
      status?: string;
    },
  ): Observable<PatientDocument> {
    return this.http.put<PatientDocument>(`${this.baseUrl}/${id}`, data);
  }

  // ─── Upload new version ───
  uploadVersion(documentId: string, file: File): Observable<PatientDocument> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<PatientDocument>(`${this.baseUrl}/${documentId}/versions`, formData);
  }

  // ─── Get version history ───
  getVersionHistory(documentId: string): Observable<PatientDocument[]> {
    return this.http.get<PatientDocument[]>(`${this.baseUrl}/${documentId}/versions`);
  }

  // ─── Sign document ───
  signDocument(id: string, signerName: string): Observable<PatientDocument> {
    return this.http.post<PatientDocument>(`${this.baseUrl}/${id}/sign`, { signerName });
  }

  // ─── Delete document ───
  deleteDocument(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ─── Get document types ───
  getDocumentTypes(): Observable<DocumentType[]> {
    return this.http.get<DocumentType[]>(`${this.baseUrl}/types`);
  }

  // ─── Storage health ───
  getStorageHealth(): Observable<StorageHealth> {
    return this.http.get<StorageHealth>(`${this.baseUrl}/storage/health`);
  }

  // ─── Utility: format file size ───
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  // ─── Utility: get icon for document type ───
  getDocumentTypeIcon(type: string): string {
    const icons: { [key: string]: string } = {
      MedicalRecord: 'description',
      AdmissionNote: 'login',
      DischargeNote: 'logout',
      ProgressNote: 'trending_up',
      LabResult: 'science',
      ImagingReport: 'image',
      PathologyReport: 'biotech',
      SemenAnalysisReport: 'analytics',
      EmbryologyReport: 'child_care',
      OocyteReport: 'egg',
      TransferReport: 'swap_horiz',
      CryopreservationReport: 'ac_unit',
      ConsentForm: 'fact_check',
      IdentityDocument: 'badge',
      InsuranceDocument: 'health_and_safety',
      Prescription: 'medication',
      TreatmentPlan: 'medical_services',
      SignedPdf: 'verified',
      Other: 'attach_file',
    };
    return icons[type] || 'attach_file';
  }
}
