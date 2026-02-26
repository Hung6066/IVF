import { Component, OnInit, OnDestroy, signal, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { BackupService } from '../../../core/services/backup.service';
import {
  BackupInfo,
  BackupLogLine,
  BackupOperation,
  BackupSchedule,
  CloudBackupObject,
  CloudConfig,
  CloudProvider,
  CloudStatusResult,
  CloudUploadResult,
} from '../../../core/models/backup.models';

@Component({
  selector: 'app-backup-restore',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './backup-restore.component.html',
  styleUrls: ['./backup-restore.component.scss'],
})
export class BackupRestoreComponent implements OnInit, OnDestroy {
  @ViewChild('logContainer') logContainer!: ElementRef<HTMLDivElement>;

  // State
  archives = signal<BackupInfo[]>([]);
  operations = signal<BackupOperation[]>([]);
  activeOperation = signal<BackupOperation | null>(null);
  logLines = signal<BackupLogLine[]>([]);
  schedule = signal<BackupSchedule | null>(null);
  loading = signal(false);
  activeTab = 'overview';
  autoScroll = true;

  // Restore form
  selectedArchive = '';
  restoreKeysOnly = false;
  restoreDryRun = false;

  // Schedule form
  scheduleEnabled = true;
  scheduleCron = '0 2 * * *';
  scheduleKeysOnly = false;
  scheduleRetentionDays = 30;
  scheduleMaxCount = 50;
  scheduleSaving = false;
  cleanupRunning = false;

  // Cloud state
  cloudStatus = signal<CloudStatusResult | null>(null);
  cloudBackups = signal<CloudBackupObject[]>([]);
  cloudUploading = signal<string | null>(null);
  cloudDownloading = signal<string | null>(null);
  lastUploadResult = signal<CloudUploadResult | null>(null);
  scheduleCloudSync = false;

  // Cloud config state
  cloudConfig = signal<CloudConfig | null>(null);
  cloudConfigProvider: CloudProvider = 'MinIO';
  cloudConfigCompression = true;
  cloudConfigS3Region = 'us-east-1';
  cloudConfigS3Bucket = 'ivf-backups';
  cloudConfigS3AccessKey = '';
  cloudConfigS3SecretKey = '';
  cloudConfigS3ServiceUrl = '';
  cloudConfigS3ForcePathStyle = true;
  cloudConfigAzureConnStr = '';
  cloudConfigAzureContainer = 'ivf-backups';
  cloudConfigGcsProjectId = '';
  cloudConfigGcsBucket = 'ivf-backups';
  cloudConfigGcsCredentialsPath = '';
  cloudConfigSaving = false;
  cloudConfigTesting = false;
  cloudTestResult = signal<{ connected: boolean; provider: string } | null>(null);

  // Confirmation
  showConfirmDialog = false;
  confirmAction: (() => void) | null = null;
  confirmMessage = '';

  private subscriptions: Subscription[] = [];
  private refreshInterval: any;

  constructor(private backupService: BackupService) {}

  ngOnInit() {
    this.loadArchives();
    this.loadOperations();
    this.loadSchedule();
    this.refreshInterval = setInterval(() => {
      this.loadOperations();
      this.loadArchives();
    }, 15000);
  }

  ngOnDestroy() {
    this.subscriptions.forEach((s) => s.unsubscribe());
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    this.backupService.disconnectHub();
  }

  switchTab(tab: string) {
    this.activeTab = tab;
    if (tab === 'archives') this.loadArchives();
    if (tab === 'history') this.loadOperations();
    if (tab === 'schedule') this.loadSchedule();
    if (tab === 'cloud') this.loadCloudData();
  }

  // ─── Data loading ─────────────────────────────────────

  loadArchives() {
    this.backupService.listArchives().subscribe({
      next: (data) => this.archives.set(data),
      error: (err) => console.error('Failed to load archives', err),
    });
  }

  loadOperations() {
    this.backupService.listOperations().subscribe({
      next: (data) => this.operations.set(data),
      error: (err) => console.error('Failed to load operations', err),
    });
  }

  // ─── Schedule ─────────────────────────────────────────

  loadSchedule() {
    this.backupService.getSchedule().subscribe({
      next: (data) => {
        this.schedule.set(data);
        this.scheduleEnabled = data.enabled;
        this.scheduleCron = data.cronExpression;
        this.scheduleKeysOnly = data.keysOnly;
        this.scheduleRetentionDays = data.retentionDays;
        this.scheduleMaxCount = data.maxBackupCount;
        this.scheduleCloudSync = data.cloudSyncEnabled;
      },
      error: (err) => console.error('Failed to load schedule', err),
    });
  }

  saveSchedule() {
    this.scheduleSaving = true;
    this.backupService
      .updateSchedule({
        enabled: this.scheduleEnabled,
        cronExpression: this.scheduleCron,
        keysOnly: this.scheduleKeysOnly,
        retentionDays: this.scheduleRetentionDays,
        maxBackupCount: this.scheduleMaxCount,
        cloudSyncEnabled: this.scheduleCloudSync,
      })
      .subscribe({
        next: () => {
          this.scheduleSaving = false;
          this.loadSchedule();
        },
        error: (err) => {
          this.scheduleSaving = false;
          console.error('Failed to save schedule', err);
        },
      });
  }

  runCleanup() {
    this.cleanupRunning = true;
    this.backupService.runCleanup().subscribe({
      next: (res) => {
        this.cleanupRunning = false;
        this.loadArchives();
        if (res.deletedCount > 0) {
          alert(`Đã xóa ${res.deletedCount} bản sao lưu cũ`);
        } else {
          alert('Không có bản sao lưu cũ cần xóa');
        }
      },
      error: (err) => {
        this.cleanupRunning = false;
        console.error('Cleanup failed', err);
      },
    });
  }

  describeCron(cron: string): string {
    if (!cron) return '';
    const parts = cron.trim().split(/\s+/);
    if (parts.length !== 5) return 'Biểu thức không hợp lệ';
    const [min, hour, dom, month, dow] = parts;
    const dowNames = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];

    let desc = '';
    if (dom === '*' && month === '*' && dow === '*') {
      desc = `Hàng ngày lúc ${hour}:${min.padStart(2, '0')}`;
    } else if (dom === '*' && month === '*' && dow !== '*') {
      const days = dow
        .split(',')
        .map((d) => dowNames[+d] || d)
        .join(', ');
      desc = `${days} lúc ${hour}:${min.padStart(2, '0')}`;
    } else if (dow === '*' && month === '*') {
      desc = `Ngày ${dom} hàng tháng lúc ${hour}:${min.padStart(2, '0')}`;
    } else {
      desc = `${min} ${hour} ${dom} ${month} ${dow}`;
    }
    return desc + ' (UTC)';
  }

  // ─── Backup ───────────────────────────────────────────

  startBackup(keysOnly = false) {
    this.loading.set(true);
    this.backupService.startBackup(keysOnly).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.watchOperation(res.operationId);
      },
      error: (err) => {
        this.loading.set(false);
        console.error('Backup start failed', err);
      },
    });
  }

  // ─── Restore ──────────────────────────────────────────

  startRestore() {
    if (!this.selectedArchive) return;

    if (this.restoreDryRun) {
      this.doStartRestore();
      return;
    }

    this.confirmMessage = `Khôi phục từ backup "${this.selectedArchive}"? Thao tác này sẽ ghi đè dữ liệu hiện tại của EJBCA và SignServer.`;
    this.confirmAction = () => this.doStartRestore();
    this.showConfirmDialog = true;
  }

  private doStartRestore() {
    this.loading.set(true);
    this.backupService
      .startRestore(this.selectedArchive, this.restoreKeysOnly, this.restoreDryRun)
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          this.watchOperation(res.operationId);
        },
        error: (err) => {
          this.loading.set(false);
          console.error('Restore start failed', err);
        },
      });
  }

  confirmDialogYes() {
    this.showConfirmDialog = false;
    if (this.confirmAction) this.confirmAction();
    this.confirmAction = null;
  }

  confirmDialogNo() {
    this.showConfirmDialog = false;
    this.confirmAction = null;
  }

  // ─── Operation watching ───────────────────────────────

  async watchOperation(operationId: string) {
    this.logLines.set([]);
    this.activeTab = 'live';

    // Load existing logs first
    this.backupService.getOperation(operationId).subscribe({
      next: (op) => {
        this.activeOperation.set(op);
        if (op.logLines) {
          this.logLines.set([...op.logLines]);
          this.scrollToBottom();
        }
      },
    });

    // Connect SignalR for live streaming
    try {
      await this.backupService.connectHub(operationId);
    } catch (err) {
      console.error('SignalR connection failed, falling back to polling', err);
      this.startPolling(operationId);
      return;
    }

    const logSub = this.backupService.logLine$.subscribe((line) => {
      if (line.operationId === operationId) {
        this.logLines.update((prev) => [...prev, line]);
        if (this.autoScroll) this.scrollToBottom();
      }
    });
    this.subscriptions.push(logSub);

    const statusSub = this.backupService.statusChanged$.subscribe((op) => {
      if (op.id === operationId) {
        this.activeOperation.set(op);
        if (op.status !== 'Running') {
          this.loadOperations();
          this.loadArchives();
        }
      }
    });
    this.subscriptions.push(statusSub);
  }

  private startPolling(operationId: string) {
    const pollInterval = setInterval(() => {
      this.backupService.getOperation(operationId).subscribe({
        next: (op) => {
          this.activeOperation.set(op);
          if (op.logLines) {
            this.logLines.set([...op.logLines]);
            if (this.autoScroll) this.scrollToBottom();
          }
          if (op.status !== 'Running') {
            clearInterval(pollInterval);
            this.loadOperations();
            this.loadArchives();
          }
        },
      });
    }, 2000);
  }

  viewOperationLogs(operationId: string) {
    this.backupService.getOperation(operationId).subscribe({
      next: (op) => {
        this.activeOperation.set(op);
        this.logLines.set(op.logLines ?? []);
        this.activeTab = 'live';
        this.scrollToBottom();

        if (op.status === 'Running') {
          this.watchOperation(operationId);
        }
      },
    });
  }

  cancelOperation() {
    const op = this.activeOperation();
    if (!op) return;
    this.backupService.cancelOperation(op.id).subscribe({
      next: () => this.loadOperations(),
    });
  }

  // ─── Cloud ────────────────────────────────────────────

  loadCloudData() {
    this.backupService.getCloudStatus().subscribe({
      next: (status) => this.cloudStatus.set(status),
      error: (err) => console.error('Failed to load cloud status', err),
    });
    this.backupService.listCloudBackups().subscribe({
      next: (data) => this.cloudBackups.set(data),
      error: (err) => console.error('Failed to load cloud backups', err),
    });
    this.loadCloudConfig();
  }

  loadCloudConfig() {
    this.backupService.getCloudConfig().subscribe({
      next: (config) => {
        this.cloudConfig.set(config);
        this.cloudConfigProvider = config.provider;
        this.cloudConfigCompression = config.compressionEnabled;
        this.cloudConfigS3Region = config.s3Region;
        this.cloudConfigS3Bucket = config.s3BucketName;
        this.cloudConfigS3AccessKey = config.s3AccessKey ?? '';
        this.cloudConfigS3SecretKey = config.s3SecretKey ?? '';
        this.cloudConfigS3ServiceUrl = config.s3ServiceUrl ?? '';
        this.cloudConfigS3ForcePathStyle = config.s3ForcePathStyle;
        this.cloudConfigAzureConnStr = config.azureConnectionString ?? '';
        this.cloudConfigAzureContainer = config.azureContainerName;
        this.cloudConfigGcsProjectId = config.gcsProjectId ?? '';
        this.cloudConfigGcsBucket = config.gcsBucketName;
        this.cloudConfigGcsCredentialsPath = config.gcsCredentialsPath ?? '';
      },
      error: (err) => console.error('Failed to load cloud config', err),
    });
  }

  saveCloudConfig() {
    this.cloudConfigSaving = true;
    this.backupService
      .updateCloudConfig({
        provider: this.cloudConfigProvider,
        compressionEnabled: this.cloudConfigCompression,
        s3Region: this.cloudConfigS3Region,
        s3BucketName: this.cloudConfigS3Bucket,
        s3AccessKey: this.cloudConfigS3AccessKey || undefined,
        s3SecretKey: this.cloudConfigS3SecretKey || undefined,
        s3ServiceUrl: this.cloudConfigS3ServiceUrl || undefined,
        s3ForcePathStyle: this.cloudConfigS3ForcePathStyle,
        azureConnectionString: this.cloudConfigAzureConnStr || undefined,
        azureContainerName: this.cloudConfigAzureContainer || undefined,
        gcsProjectId: this.cloudConfigGcsProjectId || undefined,
        gcsBucketName: this.cloudConfigGcsBucket || undefined,
        gcsCredentialsPath: this.cloudConfigGcsCredentialsPath || undefined,
      })
      .subscribe({
        next: () => {
          this.cloudConfigSaving = false;
          this.loadCloudData();
        },
        error: (err) => {
          this.cloudConfigSaving = false;
          console.error('Failed to save cloud config', err);
          alert('Lưu cấu hình thất bại: ' + (err.error?.error || err.message));
        },
      });
  }

  testCloudConnection() {
    this.cloudConfigTesting = true;
    this.cloudTestResult.set(null);
    this.backupService
      .testCloudConfig({
        provider: this.cloudConfigProvider,
        s3Region: this.cloudConfigS3Region,
        s3BucketName: this.cloudConfigS3Bucket,
        s3AccessKey: this.cloudConfigS3AccessKey || undefined,
        s3SecretKey: this.cloudConfigS3SecretKey || undefined,
        s3ServiceUrl: this.cloudConfigS3ServiceUrl || undefined,
        s3ForcePathStyle: this.cloudConfigS3ForcePathStyle,
        azureConnectionString: this.cloudConfigAzureConnStr || undefined,
        azureContainerName: this.cloudConfigAzureContainer || undefined,
        gcsProjectId: this.cloudConfigGcsProjectId || undefined,
        gcsBucketName: this.cloudConfigGcsBucket || undefined,
        gcsCredentialsPath: this.cloudConfigGcsCredentialsPath || undefined,
      })
      .subscribe({
        next: (result) => {
          this.cloudConfigTesting = false;
          this.cloudTestResult.set(result);
        },
        error: (err) => {
          this.cloudConfigTesting = false;
          this.cloudTestResult.set({ connected: false, provider: this.cloudConfigProvider });
          console.error('Cloud test failed', err);
        },
      });
  }

  uploadToCloud(archiveFileName: string) {
    this.cloudUploading.set(archiveFileName);
    this.lastUploadResult.set(null);
    this.backupService.uploadToCloud(archiveFileName).subscribe({
      next: (result) => {
        this.cloudUploading.set(null);
        this.lastUploadResult.set(result);
        this.loadCloudData();
      },
      error: (err) => {
        this.cloudUploading.set(null);
        console.error('Cloud upload failed', err);
        alert('Upload thất bại: ' + (err.error?.error || err.message));
      },
    });
  }

  downloadFromCloud(objectKey: string) {
    this.cloudDownloading.set(objectKey);
    this.backupService.downloadFromCloud(objectKey).subscribe({
      next: (res) => {
        this.cloudDownloading.set(null);
        this.loadArchives();
        alert(`Tải về thành công: ${res.fileName}`);
      },
      error: (err) => {
        this.cloudDownloading.set(null);
        console.error('Cloud download failed', err);
        alert('Tải về thất bại: ' + (err.error?.error || err.message));
      },
    });
  }

  deleteFromCloud(objectKey: string) {
    this.confirmMessage = `Xóa "${objectKey}" khỏi cloud? Thao tác này không thể hoàn tác.`;
    this.confirmAction = () => {
      this.backupService.deleteFromCloud(objectKey).subscribe({
        next: () => this.loadCloudData(),
        error: (err) => console.error('Cloud delete failed', err),
      });
    };
    this.showConfirmDialog = true;
  }

  // ─── Helpers ──────────────────────────────────────────

  formatSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1073741824) return (bytes / 1048576).toFixed(1) + ' MB';
    return (bytes / 1073741824).toFixed(1) + ' GB';
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleString('vi-VN');
  }

  formatDuration(op: BackupOperation): string {
    if (!op.startedAt) return '';
    const start = new Date(op.startedAt).getTime();
    const end = op.completedAt ? new Date(op.completedAt).getTime() : Date.now();
    const secs = Math.round((end - start) / 1000);
    if (secs < 60) return `${secs}s`;
    return `${Math.floor(secs / 60)}m ${secs % 60}s`;
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Running':
        return 'badge-info';
      case 'Completed':
        return 'badge-success';
      case 'Failed':
        return 'badge-danger';
      case 'Cancelled':
        return 'badge-warning';
      default:
        return 'badge-secondary';
    }
  }

  getLogLevelClass(level: string): string {
    switch (level) {
      case 'OK':
        return 'log-ok';
      case 'ERROR':
        return 'log-error';
      case 'WARN':
        return 'log-warn';
      default:
        return 'log-info';
    }
  }

  selectArchiveForRestore(fileName: string) {
    this.selectedArchive = fileName;
    this.activeTab = 'restore';
  }

  private scrollToBottom() {
    setTimeout(() => {
      if (this.logContainer?.nativeElement) {
        const el = this.logContainer.nativeElement;
        el.scrollTop = el.scrollHeight;
      }
    }, 50);
  }

  get runningOps(): BackupOperation[] {
    return this.operations().filter((o) => o.status === 'Running');
  }

  get recentOps(): BackupOperation[] {
    return this.operations().slice(0, 20);
  }
}
