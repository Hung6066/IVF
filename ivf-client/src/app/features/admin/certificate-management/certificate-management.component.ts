import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { BackupService } from '../../../core/services/backup.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import {
  CaDashboard,
  CaListItem,
  CaDetail,
  CertListItem,
  CertBundle,
  CertDeployResult,
  CertRenewalBatchResult,
  CreateCaRequest,
  IssueCertRequest,
  DeployCertRequest,
  DeployLogItem,
  DeployLogLine,
  CrlListItem,
  CertAuditItem,
  OcspResponse,
  CreateIntermediateCaRequest,
  RevocationReason,
} from '../../../core/models/backup.models';

type Tab =
  | 'dashboard'
  | 'cas'
  | 'certs'
  | 'expiring'
  | 'deploy'
  | 'logs'
  | 'crl'
  | 'ocsp'
  | 'audit';

@Component({
  selector: 'app-certificate-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './certificate-management.component.html',
  styleUrls: ['./certificate-management.component.scss'],
})
export class CertificateManagementComponent implements OnInit, OnDestroy {
  activeTab = signal<Tab>('dashboard');

  // Dashboard
  dashboard = signal<CaDashboard | null>(null);

  // CAs
  cas = signal<CaListItem[]>([]);
  selectedCa = signal<CaDetail | null>(null);
  showCreateCa = signal(false);
  newCa: CreateCaRequest = {
    name: '',
    commonName: '',
    organization: 'IVF System',
    country: 'VN',
    keySize: 4096,
    validityDays: 3650,
  };

  // Certificates
  certs = signal<CertListItem[]>([]);
  filterCaId = '';
  showIssueCert = signal(false);
  newCert: IssueCertRequest = {
    caId: '',
    commonName: '',
    subjectAltNames: '',
    type: 0,
    purpose: '',
    validityDays: 365,
    keySize: 2048,
    renewBeforeDays: 30,
  };

  // Cert bundle viewer
  viewingBundle = signal<CertBundle | null>(null);

  // Deploy
  deployResult = signal<CertDeployResult | null>(null);
  deployReq: DeployCertRequest = { container: 'ivf-db', certPath: '', keyPath: '', caPath: '' };
  deployingCertId = signal<string>('');
  quickDeployPgPrimaryId = '';
  quickDeployPgReplicaId = '';
  quickDeployMinioPrimaryId = '';
  quickDeployMinioReplicaId = '';

  // Expiring
  expiringCerts = signal<CertListItem[]>([]);
  renewalResult = signal<CertRenewalBatchResult | null>(null);

  // Deploy Logs (real-time + history)
  deployLogs = signal<DeployLogItem[]>([]);
  liveLogLines = signal<DeployLogLine[]>([]);
  activeOperationId = signal<string>('');
  viewingLog = signal<DeployLogItem | null>(null);
  private deploySubs: Subscription[] = [];

  // Loading
  loading = signal(false);

  // CRL
  crls = signal<CrlListItem[]>([]);
  selectedCrlCaId = signal<string>('');

  // OCSP
  ocspResult = signal<OcspResponse | null>(null);
  ocspCaId = '';
  ocspSerial = '';

  // Audit Trail
  auditEvents = signal<CertAuditItem[]>([]);
  auditFilterCertId = '';
  auditFilterCaId = '';
  auditFilterEventType = '';

  // Intermediate CA
  showCreateIntermediateCa = signal(false);
  newIntermediateCa: CreateIntermediateCaRequest = {
    parentCaId: '',
    name: '',
    commonName: '',
    organization: 'IVF System',
    country: 'VN',
    keySize: 4096,
    validityDays: 1825,
  };

  // Revocation reasons
  revocationReasons: { value: RevocationReason; label: string }[] = [
    { value: 'Unspecified', label: 'Unspecified' },
    { value: 'KeyCompromise', label: 'Key Compromise' },
    { value: 'CaCompromise', label: 'CA Compromise' },
    { value: 'AffiliationChanged', label: 'Affiliation Changed' },
    { value: 'Superseded', label: 'Superseded' },
    { value: 'CessationOfOperation', label: 'Cessation of Operation' },
    { value: 'CertificateHold', label: 'Certificate Hold' },
    { value: 'PrivilegeWithdrawn', label: 'Privilege Withdrawn' },
  ];
  selectedRevocationReason: RevocationReason = 'Unspecified';

  // purpose presets
  purposePresets = [
    { value: 'pg-primary', label: 'PostgreSQL Primary Server' },
    { value: 'pg-replica', label: 'PostgreSQL Replica Server' },
    { value: 'pg-client', label: 'PostgreSQL Client (verify-full)' },
    { value: 'minio-primary', label: 'MinIO Primary TLS' },
    { value: 'minio-replica', label: 'MinIO Replica TLS' },
    { value: 'api-client', label: 'API mTLS Client' },
    { value: 'signserver-tls', label: 'SignServer TLS' },
    { value: 'custom', label: 'Custom' },
  ];

  constructor(
    private backupService: BackupService,
    private notify: GlobalNotificationService,
  ) {}

  ngOnInit() {
    this.loadDashboard();
    this.loadCAs();
  }

  ngOnDestroy() {
    this.deploySubs.forEach((s) => s.unsubscribe());
    this.backupService.disconnectHub();
  }

  switchTab(tab: Tab) {
    this.activeTab.set(tab);
    switch (tab) {
      case 'dashboard':
        this.loadDashboard();
        break;
      case 'cas':
        this.loadCAs();
        break;
      case 'certs':
        this.loadCertificates();
        break;
      case 'expiring':
        this.loadExpiring();
        break;
      case 'deploy':
        this.filterCaId = '';
        this.loadCertificates();
        break;
      case 'logs':
        this.loadDeployLogs();
        break;
      case 'crl':
        break;
      case 'ocsp':
        break;
      case 'audit':
        this.loadAuditEvents();
        break;
    }
  }

  // ─── Dashboard ────────────────────────────────────────

  loadDashboard() {
    this.loading.set(true);
    this.backupService.getCaDashboard().subscribe({
      next: (d) => {
        this.dashboard.set(d);
        this.loading.set(false);
      },
      error: (err) => {
        this.notify.error('Error', 'Failed to load dashboard');
        this.loading.set(false);
      },
    });
  }

  // ─── CA Management ───────────────────────────────────

  loadCAs() {
    this.loading.set(true);
    this.backupService.listCAs().subscribe({
      next: (list) => {
        this.cas.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.notify.error('Error', 'Failed to load CAs');
        this.loading.set(false);
      },
    });
  }

  createCA() {
    if (!this.newCa.name || !this.newCa.commonName) return;
    this.loading.set(true);
    this.backupService.createRootCA(this.newCa).subscribe({
      next: (res) => {
        this.notify.success('Success', `CA "${res.name}" created`);
        this.showCreateCa.set(false);
        this.newCa = {
          name: '',
          commonName: '',
          organization: 'IVF System',
          country: 'VN',
          keySize: 4096,
          validityDays: 3650,
        };
        this.loadCAs();
      },
      error: (err) => {
        this.notify.error('Error', err.error?.detail || 'Failed to create CA');
        this.loading.set(false);
      },
    });
  }

  viewCaDetail(id: string) {
    this.backupService.getCA(id).subscribe({
      next: (ca) => this.selectedCa.set(ca),
      error: () => this.notify.error('Error', 'Failed to load CA details'),
    });
  }

  downloadChain(id: string) {
    this.backupService.downloadCaChain(id).subscribe({
      next: (pem) => {
        const blob = new Blob([pem], { type: 'application/x-pem-file' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'ca-chain.pem';
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.notify.error('Error', 'Failed to download CA chain'),
    });
  }

  // ─── Certificate Management ──────────────────────────

  loadCertificates() {
    this.loading.set(true);
    const caId = this.filterCaId || undefined;
    this.backupService.listCertificates(caId).subscribe({
      next: (list) => {
        this.certs.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.notify.error('Error', 'Failed to load certificates');
        this.loading.set(false);
      },
    });
  }

  issueCert() {
    if (!this.newCert.caId || !this.newCert.commonName || !this.newCert.purpose) return;
    this.loading.set(true);
    this.backupService.issueCertificate(this.newCert).subscribe({
      next: () => {
        this.notify.success('Success', 'Certificate issued');
        this.showIssueCert.set(false);
        this.loadCertificates();
      },
      error: (err) => {
        this.notify.error('Error', err.error?.detail || 'Failed to issue certificate');
        this.loading.set(false);
      },
    });
  }

  viewBundle(id: string) {
    this.backupService.getCertBundle(id).subscribe({
      next: (b) => this.viewingBundle.set(b),
      error: () => this.notify.error('Error', 'Failed to load cert bundle'),
    });
  }

  closeBundle() {
    this.viewingBundle.set(null);
  }

  downloadPem(content: string, filename: string) {
    const blob = new Blob([content], { type: 'application/x-pem-file' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  // ─── Renewal & Rotation ──────────────────────────────

  renewCert(id: string) {
    if (!confirm('Renew this certificate? The old cert will be marked as superseded.')) return;
    this.loading.set(true);
    this.backupService.renewCertificate(id).subscribe({
      next: () => {
        this.notify.success('Success', 'Certificate renewed');
        this.loadCertificates();
        this.loadExpiring();
      },
      error: (err) => {
        this.notify.error('Error', err.error?.detail || 'Failed to renew certificate');
        this.loading.set(false);
      },
    });
  }

  toggleAutoRenew(cert: CertListItem) {
    const newState = !cert.autoRenewEnabled;
    this.backupService.setAutoRenew(cert.id, newState).subscribe({
      next: () => {
        this.notify.success('Success', `Auto-renew ${newState ? 'enabled' : 'disabled'}`);
        this.loadCertificates();
      },
      error: () => this.notify.error('Error', 'Failed to update auto-renew'),
    });
  }

  loadExpiring() {
    this.loading.set(true);
    this.backupService.getExpiringCertificates().subscribe({
      next: (list) => {
        this.expiringCerts.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.notify.error('Error', 'Failed to load expiring certs');
        this.loading.set(false);
      },
    });
  }

  triggerAutoRenew() {
    if (!confirm('Run auto-renewal now for all expiring certificates?')) return;
    this.loading.set(true);
    this.backupService.triggerAutoRenewal().subscribe({
      next: (result) => {
        this.renewalResult.set(result);
        this.notify.success(
          'Success',
          `Renewed ${result.renewedCount} of ${result.totalCandidates} certificates`,
        );
        this.loadExpiring();
        this.loading.set(false);
      },
      error: (err) => {
        this.notify.error('Error', 'Auto-renewal failed');
        this.loading.set(false);
      },
    });
  }

  // ─── Revocation (with reason) ──────────────────────────

  revokeCert(id: string, cn: string) {
    if (!confirm(`Revoke certificate "${cn}"? This cannot be undone.`)) return;
    this.backupService.revokeCertificate(id, this.selectedRevocationReason).subscribe({
      next: () => {
        this.notify.success('Success', 'Certificate revoked & CRL updated');
        this.loadCertificates();
      },
      error: () => this.notify.error('Error', 'Failed to revoke certificate'),
    });
  }

  // ─── Deployment ──────────────────────────────────────

  startDeploy(certId: string) {
    this.deployingCertId.set(certId);
    this.deployResult.set(null);
    this.liveLogLines.set([]);
    this.activeTab.set('deploy');
  }

  private async startDeployWithLogs(certId: string, deployFn: () => void) {
    this.liveLogLines.set([]);
    this.deployResult.set(null);
    this.loading.set(true);

    // We don't know the operationId yet; we'll connect when we get the result
    deployFn();
  }

  private handleDeployResult(r: CertDeployResult, successMsg: string) {
    this.deployResult.set(r);
    this.loading.set(false);

    if (r.operationId) {
      this.activeOperationId.set(r.operationId);
      // Connect SignalR to get any remaining logs
      this.connectToDeployLogs(r.operationId);
      // Also load from DB for persistence
      this.backupService.getDeployLog(r.operationId).subscribe({
        next: (log) => {
          this.liveLogLines.set(log.logLines || []);
        },
      });
    }

    if (r.success) this.notify.success('Success', successMsg);
    else this.notify.error('Error', 'Deployment failed');
  }

  private connectToDeployLogs(operationId: string) {
    this.deploySubs.forEach((s) => s.unsubscribe());
    this.deploySubs = [];

    this.deploySubs.push(
      this.backupService.deployLog$.subscribe((logLine) => {
        if (logLine && logLine.operationId === operationId) {
          this.liveLogLines.update((lines) => [
            ...lines,
            {
              timestamp: logLine.timestamp,
              level: logLine.level,
              message: logLine.message,
            },
          ]);
        }
      }),
    );

    this.deploySubs.push(
      this.backupService.deployStatus$.subscribe((status) => {
        if (status && status.operationId === operationId) {
          if (status.status === 'Completed' || status.status === 'Failed') {
            this.loading.set(false);
          }
        }
      }),
    );

    this.backupService.connectDeployHub(operationId).catch(() => {
      // SignalR connection failed — logs still available via REST
    });
  }

  deployToContainer() {
    const id = this.deployingCertId();
    if (!id) return;
    this.loading.set(true);
    this.liveLogLines.set([]);
    this.backupService.deployCertificate(id, this.deployReq).subscribe({
      next: (r) => this.handleDeployResult(r, 'Certificate deployed'),
      error: () => {
        this.notify.error('Error', 'Deploy failed');
        this.loading.set(false);
      },
    });
  }

  deployPgSsl(certId: string) {
    if (!confirm('Deploy this certificate as PostgreSQL Primary SSL and reload config?')) return;
    this.loading.set(true);
    this.liveLogLines.set([]);
    this.backupService.deployPgSsl(certId).subscribe({
      next: (r) => this.handleDeployResult(r, 'PostgreSQL Primary SSL deployed & reloaded'),
      error: () => {
        this.notify.error('Error', 'PG Primary SSL deploy failed');
        this.loading.set(false);
      },
    });
  }

  deployMinioSsl(certId: string) {
    if (!confirm('Deploy this certificate to MinIO Primary and restart?')) return;
    this.loading.set(true);
    this.liveLogLines.set([]);
    this.backupService.deployMinioSsl(certId).subscribe({
      next: (r) => this.handleDeployResult(r, 'MinIO Primary TLS deployed & restarted'),
      error: () => {
        this.notify.error('Error', 'MinIO Primary TLS deploy failed');
        this.loading.set(false);
      },
    });
  }

  deployReplicaPgSsl(certId: string) {
    if (!confirm('Deploy this certificate to Replica PostgreSQL via SSH and reload config?'))
      return;
    this.loading.set(true);
    this.liveLogLines.set([]);
    this.backupService.deployReplicaPgSsl(certId).subscribe({
      next: (r) => this.handleDeployResult(r, 'Replica PostgreSQL SSL deployed & reloaded'),
      error: () => {
        this.notify.error('Error', 'Replica PG SSL deploy failed');
        this.loading.set(false);
      },
    });
  }

  deployReplicaMinioSsl(certId: string) {
    if (!confirm('Deploy this certificate to Replica MinIO via SSH and restart?')) return;
    this.loading.set(true);
    this.liveLogLines.set([]);
    this.backupService.deployReplicaMinioSsl(certId).subscribe({
      next: (r) => this.handleDeployResult(r, 'Replica MinIO TLS deployed & restarted'),
      error: () => {
        this.notify.error('Error', 'Replica MinIO TLS deploy failed');
        this.loading.set(false);
      },
    });
  }

  // ─── Intermediate CA ──────────────────────────────────

  createIntermediateCA() {
    const req = this.newIntermediateCa;
    if (!req.parentCaId || !req.name || !req.commonName) return;
    this.loading.set(true);
    this.backupService.createIntermediateCA(req).subscribe({
      next: (res) => {
        this.notify.success('Success', `Intermediate CA "${res.name}" created`);
        this.showCreateIntermediateCa.set(false);
        this.newIntermediateCa = {
          parentCaId: '',
          name: '',
          commonName: '',
          organization: 'IVF System',
          country: 'VN',
          keySize: 4096,
          validityDays: 1825,
        };
        this.loadCAs();
      },
      error: (err) => {
        this.notify.error('Error', err.error?.detail || 'Failed to create Intermediate CA');
        this.loading.set(false);
      },
    });
  }

  // ─── CRL Management ─────────────────────────────────

  loadCrls(caId: string) {
    this.selectedCrlCaId.set(caId);
    this.loading.set(true);
    this.backupService.listCrls(caId).subscribe({
      next: (list) => {
        this.crls.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.notify.error('Error', 'Failed to load CRLs');
        this.loading.set(false);
      },
    });
  }

  generateCrl(caId: string) {
    if (!confirm('Generate a new CRL for this CA?')) return;
    this.loading.set(true);
    this.backupService.generateCrl(caId).subscribe({
      next: (result) => {
        this.notify.success(
          'Success',
          `CRL #${result.crlNumber} generated with ${result.revokedCount} revoked cert(s)`,
        );
        this.loadCrls(caId);
      },
      error: (err) => {
        this.notify.error('Error', err.error?.detail || 'Failed to generate CRL');
        this.loading.set(false);
      },
    });
  }

  downloadCrl(caId: string) {
    this.backupService.downloadLatestCrl(caId).subscribe({
      next: (pem) => {
        const blob = new Blob([pem], { type: 'application/x-pem-file' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'crl.pem';
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.notify.error('Error', 'No CRL available for download'),
    });
  }

  // ─── OCSP ────────────────────────────────────────────

  checkOcsp() {
    if (!this.ocspCaId || !this.ocspSerial) return;
    this.loading.set(true);
    this.backupService.checkCertStatus(this.ocspCaId, this.ocspSerial).subscribe({
      next: (result) => {
        this.ocspResult.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.notify.error('Error', 'OCSP query failed');
        this.loading.set(false);
      },
    });
  }

  ocspStatusClass(status: string): string {
    const map: Record<string, string> = {
      Good: 'badge-success',
      Revoked: 'badge-danger',
      Unknown: 'badge-warning',
    };
    return map[status] ?? '';
  }

  // ─── Audit Trail ─────────────────────────────────────

  loadAuditEvents() {
    this.loading.set(true);
    this.backupService
      .listCertAuditEvents(
        this.auditFilterCertId || undefined,
        this.auditFilterCaId || undefined,
        this.auditFilterEventType || undefined,
        100,
      )
      .subscribe({
        next: (events) => {
          this.auditEvents.set(events);
          this.loading.set(false);
        },
        error: () => {
          this.notify.error('Error', 'Failed to load audit events');
          this.loading.set(false);
        },
      });
  }

  auditEventTypeLabel(type: string): string {
    const map: Record<string, string> = {
      CaCreated: 'CA Created',
      CaRevoked: 'CA Revoked',
      CertIssued: 'Cert Issued',
      CertRenewed: 'Cert Renewed',
      CertRevoked: 'Cert Revoked',
      CertExpired: 'Cert Expired',
      CertSuperseded: 'Cert Superseded',
      CertDeployed: 'Cert Deployed',
      CertDeployFailed: 'Deploy Failed',
      AutoRenewTriggered: 'Auto-Renew',
      AutoRenewFailed: 'Auto-Renew Failed',
      CrlGenerated: 'CRL Generated',
      OcspQuery: 'OCSP Query',
      IntermediateCaCreated: 'Intermediate CA Created',
      CertRotationStarted: 'Rotation Started',
      CertRotationCompleted: 'Rotation Completed',
    };
    return map[type] ?? type;
  }

  // ─── Deploy Logs ─────────────────────────────────────

  loadDeployLogs() {
    this.loading.set(true);
    this.backupService.listDeployLogs(undefined, 50).subscribe({
      next: (logs) => {
        this.deployLogs.set(logs);
        this.loading.set(false);
      },
      error: () => {
        this.notify.error('Error', 'Failed to load deploy logs');
        this.loading.set(false);
      },
    });
  }

  viewDeployLog(log: DeployLogItem) {
    this.viewingLog.set(log);
  }

  closeLogViewer() {
    this.viewingLog.set(null);
  }

  deployStatusClass(status: string): string {
    const map: Record<string, string> = {
      Running: 'badge-warning',
      Completed: 'badge-success',
      Failed: 'badge-danger',
    };
    return map[status] ?? '';
  }

  logLevelClass(level: string): string {
    const map: Record<string, string> = {
      info: 'log-info',
      warn: 'log-warn',
      error: 'log-error',
      success: 'log-success',
    };
    return map[level] ?? 'log-info';
  }

  // ─── Helpers ─────────────────────────────────────────

  certStatusLabel(status: string): string {
    return status || 'Unknown';
  }

  certStatusClass(status: string): string {
    const map: Record<string, string> = {
      Active: 'badge-success',
      Revoked: 'badge-danger',
      Expired: 'badge-warning',
      Superseded: 'badge-secondary',
    };
    return map[status] ?? '';
  }

  certTypeLabel(type: string): string {
    return type || 'Unknown';
  }

  caStatusLabel(status: string): string {
    return status || 'Unknown';
  }

  caTypeLabel(type: string): string {
    return type || 'Unknown';
  }

  daysUntilExpiry(dateStr: string): number {
    return Math.ceil((new Date(dateStr).getTime() - Date.now()) / 86400000);
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('vi-VN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  truncateFingerprint(fp: string): string {
    if (!fp) return '';
    return fp.length > 16 ? fp.substring(0, 16) + '…' : fp;
  }

  getDuration(start: string, end: string): string {
    const ms = new Date(end).getTime() - new Date(start).getTime();
    if (ms < 1000) return `${ms}ms`;
    const s = Math.floor(ms / 1000);
    if (s < 60) return `${s}s`;
    return `${Math.floor(s / 60)}m ${s % 60}s`;
  }
}
