import { Component, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SigningAdminService } from '../../../core/services/signing-admin.service';
import { UserSignatureService } from '../../../core/services/user-signature.service';
import { SignaturePadComponent } from '../../../shared/signature-pad/signature-pad.component';
import {
  SigningDashboard,
  SigningConfig,
  ServiceHealthDetail,
  TestSignResult,
  UserSignatureListItem,
  CertProvisionResult,
  UserTestSignResult,
  EjbcaCertificate,
  EjbcaCertSearchRequest,
  EJBCA_CERT_STATUSES,
  EJBCA_REVOKE_REASONS,
} from '../../../core/models/api.models';

@Component({
  selector: 'app-digital-signing',
  standalone: true,
  imports: [CommonModule, FormsModule, SignaturePadComponent],
  templateUrl: './digital-signing.component.html',
  styleUrls: ['./digital-signing.component.scss'],
})
export class DigitalSigningComponent implements OnInit, OnDestroy {
  @ViewChild('signaturePad') signaturePad!: SignaturePadComponent;

  dashboard = signal<SigningDashboard | null>(null);
  config = signal<SigningConfig | null>(null);
  signServerHealth = signal<ServiceHealthDetail | null>(null);
  ejbcaHealth = signal<ServiceHealthDetail | null>(null);
  testResult = signal<TestSignResult | null>(null);
  workers = signal<any>(null);
  ejbcaCAs = signal<any>(null);

  // SignServer management
  selectedWorker = signal<any>(null);
  workerLoading = signal(false);
  workerActionResult = signal<{
    success: boolean;
    message?: string;
    error?: string;
    output?: string;
  } | null>(null);
  expandedWorkerId = signal<number | null>(null);

  // EJBCA management
  ejbcaCertificates = signal<EjbcaCertificate[]>([]);
  ejbcaCertProfiles = signal<any>(null);
  ejbcaEndEntityProfiles = signal<any>(null);
  ejbcaSearching = signal(false);
  ejbcaRevoking = signal(false);
  ejbcaCertSearch: EjbcaCertSearchRequest = {};
  ejbcaMoreResults = signal(false);
  ejbcaRevokeConfirm = signal<{ serial: string; issuerDn: string } | null>(null);
  ejbcaRevokeReason = 'UNSPECIFIED';
  ejbcaRevokeResult = signal<{ success: boolean; message?: string; error?: string } | null>(null);
  expandedCertSerial = signal<string | null>(null);
  ejbcaTotalCount = signal<number>(0);
  readonly certStatuses = EJBCA_CERT_STATUSES;
  readonly revokeReasons = EJBCA_REVOKE_REASONS;

  // User signatures
  userSignatures = signal<UserSignatureListItem[]>([]);
  selectedUserId = signal<string | null>(null);
  provisionResult = signal<CertProvisionResult | null>(null);
  userTestResult = signal<UserTestSignResult | null>(null);
  showSignaturePad = signal(false);
  signaturePadUserId = signal<string | null>(null);
  uploadingSignature = signal(false);
  provisioningCert = signal(false);
  renewingCert = signal(false);
  testingUserSign = signal(false);

  signatureImageUrls = new Map<string, string>();

  loading = signal(false);
  testingSign = signal(false);
  refreshingHealth = signal(false);
  activeTab = 'overview';
  autoRefreshEnabled = false;
  private autoRefreshInterval: any = null;

  constructor(
    private signingService: SigningAdminService,
    public signatureService: UserSignatureService,
  ) {}

  ngOnInit() {
    this.loadDashboard();
  }

  ngOnDestroy() {
    this.stopAutoRefresh();
    // Revoke all object URLs to prevent memory leaks
    this.signatureImageUrls.forEach((url) => URL.revokeObjectURL(url));
    this.signatureImageUrls.clear();
  }

  loadDashboard() {
    this.loading.set(true);
    this.signingService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load signing dashboard:', err);
        this.loading.set(false);
      },
    });
  }

  loadConfig() {
    this.signingService.getConfig().subscribe({
      next: (data) => this.config.set(data),
      error: (err) => console.error('Failed to load config:', err),
    });
  }

  refreshSignServerHealth() {
    this.refreshingHealth.set(true);
    this.signingService.getSignServerHealth().subscribe({
      next: (data) => {
        this.signServerHealth.set(data);
        this.refreshingHealth.set(false);
      },
      error: (err) => {
        console.error('SignServer health check failed:', err);
        this.refreshingHealth.set(false);
      },
    });
  }

  refreshEjbcaHealth() {
    this.refreshingHealth.set(true);
    this.signingService.getEjbcaHealth().subscribe({
      next: (data) => {
        this.ejbcaHealth.set(data);
        this.refreshingHealth.set(false);
      },
      error: (err) => {
        console.error('EJBCA health check failed:', err);
        this.refreshingHealth.set(false);
      },
    });
  }

  loadWorkers() {
    this.signingService.getSignServerWorkers().subscribe({
      next: (data) => this.workers.set(data),
      error: (err) => console.error('Failed to load workers:', err),
    });
  }

  loadWorkerDetail(workerId: number) {
    this.workerLoading.set(true);
    this.selectedWorker.set(null);
    this.signingService.getSignServerWorker(workerId).subscribe({
      next: (data) => {
        this.selectedWorker.set(data);
        this.workerLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load worker detail:', err);
        this.workerLoading.set(false);
      },
    });
  }

  toggleWorkerDetail(workerId: number) {
    if (this.expandedWorkerId() === workerId) {
      this.expandedWorkerId.set(null);
      this.selectedWorker.set(null);
    } else {
      this.expandedWorkerId.set(workerId);
      this.loadWorkerDetail(workerId);
    }
  }

  reloadWorker(workerId: number) {
    this.workerActionResult.set(null);
    this.signingService.reloadSignServerWorker(workerId).subscribe({
      next: (result) => {
        this.workerActionResult.set(result);
        this.loadWorkers(); // Refresh list
      },
      error: (err) => this.workerActionResult.set({ success: false, error: err.message }),
    });
  }

  testWorkerKey(workerId: number) {
    this.workerActionResult.set(null);
    this.workerLoading.set(true);
    this.signingService.testSignServerWorkerKey(workerId).subscribe({
      next: (result) => {
        this.workerActionResult.set(result);
        this.workerLoading.set(false);
      },
      error: (err) => {
        this.workerActionResult.set({ success: false, error: err.message });
        this.workerLoading.set(false);
      },
    });
  }

  getWorkerProperties(worker: any): { key: string; value: string }[] {
    if (!worker?.properties) return [];
    return Object.entries(worker.properties)
      .filter(([key]) => key !== 'KEYSTOREPASSWORD')
      .map(([key, value]) => ({ key, value: value as string }))
      .sort((a, b) => a.key.localeCompare(b.key));
  }

  loadEjbcaCAs() {
    this.signingService.getEjbcaCAs().subscribe({
      next: (data) => this.ejbcaCAs.set(data),
      error: (err) => console.error('Failed to load CAs:', err),
    });
  }

  // ─── EJBCA Certificate Management ───────────────────────

  searchEjbcaCertificates() {
    this.ejbcaSearching.set(true);
    this.ejbcaCertificates.set([]);
    this.signingService.searchEjbcaCertificates(this.ejbcaCertSearch).subscribe({
      next: (data) => {
        this.ejbcaCertificates.set(data.certificates || []);
        this.ejbcaMoreResults.set(data.more_results || false);
        this.ejbcaTotalCount.set(data.total_count || 0);
        this.expandedCertSerial.set(null);
        this.ejbcaSearching.set(false);
      },
      error: (err) => {
        console.error('EJBCA certificate search failed:', err);
        this.ejbcaSearching.set(false);
      },
    });
  }

  loadEjbcaProfiles() {
    this.signingService.getEjbcaCertificateProfiles().subscribe({
      next: (data) => this.ejbcaCertProfiles.set(data),
      error: () => {},
    });
    this.signingService.getEjbcaEndEntityProfiles().subscribe({
      next: (data) => this.ejbcaEndEntityProfiles.set(data),
      error: () => {},
    });
  }

  openRevokeConfirm(serial: string, issuerDn: string) {
    this.ejbcaRevokeConfirm.set({ serial, issuerDn });
    this.ejbcaRevokeReason = 'UNSPECIFIED';
    this.ejbcaRevokeResult.set(null);
  }

  cancelRevoke() {
    this.ejbcaRevokeConfirm.set(null);
    this.ejbcaRevokeResult.set(null);
  }

  confirmRevokeCertificate() {
    const target = this.ejbcaRevokeConfirm();
    if (!target) return;

    this.ejbcaRevoking.set(true);
    this.signingService
      .revokeEjbcaCertificate(target.serial, target.issuerDn, this.ejbcaRevokeReason)
      .subscribe({
        next: (result) => {
          this.ejbcaRevokeResult.set(result);
          this.ejbcaRevoking.set(false);
          if (result.success) {
            this.searchEjbcaCertificates(); // Refresh list
          }
        },
        error: (err) => {
          this.ejbcaRevokeResult.set({ success: false, error: err.message });
          this.ejbcaRevoking.set(false);
        },
      });
  }

  downloadCaCert(caName: string) {
    this.signingService.downloadCaCertificate(caName).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${caName}_ca.pem`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => console.error('Failed to download CA cert:', err),
    });
  }

  formatCertSubject(dn: string): string {
    if (!dn) return '-';
    const cn = dn.match(/CN=([^,]+)/i);
    return cn ? cn[1] : dn;
  }

  toggleCertDetail(serialNumber: string) {
    this.expandedCertSerial.set(this.expandedCertSerial() === serialNumber ? null : serialNumber);
  }

  // ─── User Signatures ────────────────────────────────────

  loadUserSignatures() {
    this.signatureService.listSignatures().subscribe({
      next: (data) => {
        this.userSignatures.set(data.items);
        this.loadSignatureImages(data.items);
      },
      error: (err) => console.error('Failed to load user signatures:', err),
    });
  }

  private loadSignatureImages(signatures: UserSignatureListItem[]) {
    // Revoke old URLs
    this.signatureImageUrls.forEach((url) => URL.revokeObjectURL(url));
    this.signatureImageUrls.clear();

    for (const sig of signatures) {
      if (sig.hasSignatureImage) {
        this.signatureService.getUserSignatureImageBlob(sig.userId).subscribe({
          next: (blob) => {
            this.signatureImageUrls.set(sig.userId, URL.createObjectURL(blob));
          },
          error: () => {
            /* signature image not available */
          },
        });
      }
    }
  }

  openSignaturePad(userId: string) {
    this.signaturePadUserId.set(userId);
    this.showSignaturePad.set(true);
  }

  closeSignaturePad() {
    this.showSignaturePad.set(false);
    this.signaturePadUserId.set(null);
  }

  saveSignature() {
    const userId = this.signaturePadUserId();
    if (!userId || !this.signaturePad) return;

    const base64 = this.signaturePad.getSignatureBase64();
    if (!base64) return;

    this.uploadingSignature.set(true);
    this.signatureService.uploadUserSignature(userId, base64).subscribe({
      next: () => {
        this.uploadingSignature.set(false);
        this.closeSignaturePad();
        this.loadUserSignatures();
      },
      error: (err) => {
        console.error('Failed to upload signature:', err);
        this.uploadingSignature.set(false);
      },
    });
  }

  provisionCertificate(userId: string) {
    this.provisioningCert.set(true);
    this.provisionResult.set(null);
    this.signatureService.provisionCertificate(userId).subscribe({
      next: (result) => {
        this.provisionResult.set(result);
        this.provisioningCert.set(false);
        this.loadUserSignatures(); // Refresh list
      },
      error: (err) => {
        this.provisionResult.set({
          success: false,
          message: 'Lỗi kết nối',
          error: err.message,
        });
        this.provisioningCert.set(false);
      },
    });
  }

  renewCertificate(userId: string) {
    this.renewingCert.set(true);
    this.provisionResult.set(null);
    this.signatureService.renewCertificate(userId).subscribe({
      next: (result) => {
        this.provisionResult.set(result);
        this.renewingCert.set(false);
        this.loadUserSignatures();
      },
      error: (err) => {
        this.provisionResult.set({
          success: false,
          message: 'Lỗi kết nối khi gia hạn',
          error: err.message,
        });
        this.renewingCert.set(false);
      },
    });
  }

  testUserSign(userId: string) {
    this.testingUserSign.set(true);
    this.userTestResult.set(null);
    this.signatureService.testUserSigning(userId).subscribe({
      next: (result) => {
        this.userTestResult.set(result);
        this.testingUserSign.set(false);
      },
      error: (err) => {
        this.userTestResult.set({ success: false, error: err.message });
        this.testingUserSign.set(false);
      },
    });
  }

  getCertStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      None: 'Chưa cấp',
      Pending: 'Đang xử lý',
      Active: 'Hoạt động',
      Expired: 'Hết hạn',
      Revoked: 'Đã thu hồi',
      Error: 'Lỗi',
    };
    return labels[status] || status;
  }

  getCertStatusClass(status: string): string {
    const classes: Record<string, string> = {
      None: 'cert-none',
      Pending: 'cert-pending',
      Active: 'cert-active',
      Expired: 'cert-expired',
      Revoked: 'cert-revoked',
      Error: 'cert-error',
    };
    return classes[status] || '';
  }

  // ─── Common Methods ─────────────────────────────────────

  testSigning() {
    this.testingSign.set(true);
    this.testResult.set(null);
    this.signingService.testSign().subscribe({
      next: (result) => {
        this.testResult.set(result);
        this.testingSign.set(false);
      },
      error: (err) => {
        this.testResult.set({
          success: false,
          error: err.message || 'Network error',
          errorType: 'HttpError',
          timestamp: new Date().toISOString(),
        });
        this.testingSign.set(false);
      },
    });
  }

  switchTab(tab: string) {
    this.activeTab = tab;
    switch (tab) {
      case 'overview':
        this.loadDashboard();
        break;
      case 'signserver':
        this.refreshSignServerHealth();
        this.loadWorkers();
        break;
      case 'ejbca':
        this.refreshEjbcaHealth();
        this.loadEjbcaCAs();
        this.loadEjbcaProfiles();
        break;
      case 'config':
        this.loadConfig();
        break;
      case 'signatures':
        this.loadUserSignatures();
        break;
    }
  }

  toggleAutoRefresh() {
    this.autoRefreshEnabled = !this.autoRefreshEnabled;
    if (this.autoRefreshEnabled) {
      this.autoRefreshInterval = setInterval(() => {
        this.loadDashboard();
      }, 15000);
    } else {
      this.stopAutoRefresh();
    }
  }

  private stopAutoRefresh() {
    if (this.autoRefreshInterval) {
      clearInterval(this.autoRefreshInterval);
      this.autoRefreshInterval = null;
    }
    this.autoRefreshEnabled = false;
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'healthy':
        return 'status-healthy';
      case 'unhealthy':
        return 'status-unhealthy';
      case 'unreachable':
        return 'status-unreachable';
      case 'disabled':
        return 'status-disabled';
      default:
        return 'status-unknown';
    }
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'healthy':
        return 'Hoạt động';
      case 'unhealthy':
        return 'Lỗi';
      case 'unreachable':
        return 'Không kết nối';
      case 'disabled':
        return 'Tắt';
      default:
        return 'Không rõ';
    }
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'healthy':
        return '✅';
      case 'unhealthy':
        return '⚠️';
      case 'unreachable':
        return '❌';
      case 'disabled':
        return '⏸️';
      default:
        return '❓';
    }
  }

  formatDateTime(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    return (bytes / 1024).toFixed(1) + ' KB';
  }

  openExternalUrl(url: string) {
    window.open(url, '_blank', 'noopener,noreferrer');
  }
}
