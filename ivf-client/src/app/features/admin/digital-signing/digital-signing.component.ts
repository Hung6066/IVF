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

  // User signatures
  userSignatures = signal<UserSignatureListItem[]>([]);
  selectedUserId = signal<string | null>(null);
  provisionResult = signal<CertProvisionResult | null>(null);
  userTestResult = signal<UserTestSignResult | null>(null);
  showSignaturePad = signal(false);
  signaturePadUserId = signal<string | null>(null);
  uploadingSignature = signal(false);
  provisioningCert = signal(false);
  testingUserSign = signal(false);

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

  loadEjbcaCAs() {
    this.signingService.getEjbcaCAs().subscribe({
      next: (data) => this.ejbcaCAs.set(data),
      error: (err) => console.error('Failed to load CAs:', err),
    });
  }

  // ─── User Signatures ────────────────────────────────────

  loadUserSignatures() {
    this.signatureService.listSignatures().subscribe({
      next: (data) => this.userSignatures.set(data.items),
      error: (err) => console.error('Failed to load user signatures:', err),
    });
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
