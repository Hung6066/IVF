import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DocumentService } from '../../../core/services/document.service';
import { PatientService } from '../../../core/services/patient.service';
import {
  PatientDocument,
  PatientDocumentSummary,
  DocumentType,
} from '../../../core/models/document.models';

@Component({
  selector: 'app-patient-documents',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './patient-documents.component.html',
  styleUrls: ['./patient-documents.component.scss'],
})
export class PatientDocumentsComponent implements OnInit {
  // ─── State ───
  documents = signal<PatientDocument[]>([]);
  summary = signal<PatientDocumentSummary | null>(null);
  documentTypes = signal<DocumentType[]>([]);
  patient = signal<any>(null);
  totalDocuments = signal(0);
  loading = signal(false);
  uploading = signal(false);
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  // ─── Filters ───
  selectedType = signal<string>('');
  searchTerm = signal('');
  currentPage = signal(1);
  pageSize = 20;

  // ─── Upload dialog ───
  showUploadDialog = signal(false);
  uploadTitle = signal('');
  uploadDescription = signal('');
  uploadDocType = signal('MedicalRecord');
  uploadConfidentiality = signal('Normal');
  uploadTags = signal('');
  selectedFile = signal<File | null>(null);

  // ─── Detail panel ───
  selectedDocument = signal<PatientDocument | null>(null);
  showDetailPanel = signal(false);
  versionHistory = signal<PatientDocument[]>([]);

  // ─── Computed ───
  storageUsed = computed(() => {
    const s = this.summary();
    return s ? this.documentService.formatFileSize(s.totalStorageBytes) : '0 B';
  });

  patientId = '';

  private route = inject(ActivatedRoute);
  documentService = inject(DocumentService);
  private patientService = inject(PatientService);

  ngOnInit(): void {
    this.route.params.subscribe((params) => {
      this.patientId = params['id'];
      this.loadData();
    });
  }

  loadData(): void {
    this.loading.set(true);
    this.loadDocuments();
    this.loadSummary();
    this.loadDocumentTypes();
    this.loadPatient();
  }

  loadPatient(): void {
    this.patientService.getPatient(this.patientId).subscribe({
      next: (p) => this.patient.set(p),
      error: () => {},
    });
  }

  loadDocuments(): void {
    this.loading.set(true);
    this.documentService
      .getPatientDocuments(this.patientId, {
        type: this.selectedType() || undefined,
        search: this.searchTerm() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.documents.set(res.items);
          this.totalDocuments.set(res.total);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set('Không thể tải danh sách tài liệu');
          this.loading.set(false);
        },
      });
  }

  loadSummary(): void {
    this.documentService.getPatientSummary(this.patientId).subscribe({
      next: (s) => this.summary.set(s),
      error: () => {},
    });
  }

  loadDocumentTypes(): void {
    this.documentService.getDocumentTypes().subscribe({
      next: (types) => this.documentTypes.set(types),
      error: () => {},
    });
  }

  // ─── Filter actions ───
  onTypeChange(type: string): void {
    this.selectedType.set(type);
    this.currentPage.set(1);
    this.loadDocuments();
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.currentPage.set(1);
    this.loadDocuments();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadDocuments();
  }

  get totalPages(): number {
    return Math.ceil(this.totalDocuments() / this.pageSize);
  }

  // ─── Upload ───
  openUploadDialog(): void {
    this.showUploadDialog.set(true);
    this.uploadTitle.set('');
    this.uploadDescription.set('');
    this.uploadDocType.set('MedicalRecord');
    this.uploadConfidentiality.set('Normal');
    this.uploadTags.set('');
    this.selectedFile.set(null);
  }

  closeUploadDialog(): void {
    this.showUploadDialog.set(false);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      this.selectedFile.set(file);
      if (!this.uploadTitle()) {
        this.uploadTitle.set(file.name.replace(/\.[^/.]+$/, ''));
      }
    }
  }

  uploadDocument(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.uploading.set(true);
    this.documentService
      .uploadDocument(file, this.patientId, this.uploadTitle() || file.name, this.uploadDocType(), {
        description: this.uploadDescription() || undefined,
        confidentiality: this.uploadConfidentiality(),
        tags: this.uploadTags() || undefined,
      })
      .subscribe({
        next: (doc) => {
          this.uploading.set(false);
          this.showUploadDialog.set(false);
          this.showSuccess('Tải lên thành công: ' + doc.title);
          this.loadData();
        },
        error: (err) => {
          this.uploading.set(false);
          this.error.set(err.error?.error || 'Tải lên thất bại');
        },
      });
  }

  // ─── Document actions ───
  viewDocument(doc: PatientDocument): void {
    this.selectedDocument.set(doc);
    this.showDetailPanel.set(true);
    this.loadVersionHistory(doc.id);
  }

  loadVersionHistory(docId: string): void {
    this.documentService.getVersionHistory(docId).subscribe({
      next: (versions) => this.versionHistory.set(versions),
      error: () => this.versionHistory.set([]),
    });
  }

  closeDetailPanel(): void {
    this.showDetailPanel.set(false);
    this.selectedDocument.set(null);
  }

  downloadDocument(doc: PatientDocument, signed = false): void {
    this.documentService.downloadFile(doc.id, signed).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download =
          signed && doc.isSigned ? `signed_${doc.originalFileName}` : doc.originalFileName;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.error.set('Không thể tải file'),
    });
  }

  signDocument(doc: PatientDocument): void {
    const signerName = prompt('Nhập tên người ký:');
    if (!signerName) return;

    this.documentService.signDocument(doc.id, signerName).subscribe({
      next: (updated) => {
        this.showSuccess('Ký số thành công');
        this.selectedDocument.set(updated);
        this.loadDocuments();
      },
      error: (err) => this.error.set(err.error?.error || 'Ký số thất bại'),
    });
  }

  deleteDocument(doc: PatientDocument): void {
    if (!confirm(`Xác nhận xóa tài liệu "${doc.title}"?`)) return;

    this.documentService.deleteDocument(doc.id).subscribe({
      next: () => {
        this.showSuccess('Đã xóa tài liệu');
        this.closeDetailPanel();
        this.loadData();
      },
      error: () => this.error.set('Xóa thất bại'),
    });
  }

  // ─── Helpers ───
  getDocTypeIcon(type: string): string {
    return this.documentService.getDocumentTypeIcon(type);
  }

  getDocTypeLabel(type: string): string {
    const found = this.documentTypes().find((t) => t.value === type);
    return found?.label || type;
  }

  formatSize(bytes: number): string {
    return this.documentService.formatFileSize(bytes);
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  getStatusClass(status: string): string {
    return status.toLowerCase();
  }

  getStatusLabel(status: string): string {
    const labels: { [key: string]: string } = {
      Draft: 'Nháp',
      Active: 'Đang dùng',
      Superseded: 'Đã thay thế',
      Archived: 'Lưu trữ',
      EnteredInError: 'Nhập sai',
    };
    return labels[status] || status;
  }

  getConfidentialityLabel(level: string): string {
    const labels: { [key: string]: string } = {
      Normal: 'Bình thường',
      Restricted: 'Hạn chế',
      VeryRestricted: 'Rất hạn chế',
    };
    return labels[level] || level;
  }

  private showSuccess(message: string): void {
    this.successMessage.set(message);
    this.error.set(null);
    setTimeout(() => this.successMessage.set(null), 3000);
  }
}
