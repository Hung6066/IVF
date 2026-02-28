import { Component, OnInit, OnDestroy, signal, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { BackupService } from '../../../core/services/backup.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import {
  BackupInfo,
  BackupLogLine,
  BackupOperation,
  BackupSchedule,
  BackupValidationResult,
  CloudBackupObject,
  CloudConfig,
  CloudProvider,
  CloudReplicationConfig,
  CloudReplicationSetupResult,
  CloudStatusResult,
  CloudUploadResult,
  ComplianceReport,
  DataBackupFile,
  DataBackupStatus,
  DataBackupStrategy,
  DbCloudReplicationStatus,
  ExternalReplicationGuide,
  MinioCloudReplicationStatus,
  MinioSyncResult,
  ReplicationActivationResult,
  ReplicationSetupGuide,
  ReplicationStatus,
  WalArchiveListResponse,
  WalStatusResponse,
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

  // Data backup state
  dataStatus = signal<DataBackupStatus | null>(null);
  dataBackupRunning = false;
  dataRestoreRunning = false;
  dataIncludeDatabase = true;
  dataIncludeMinio = true;
  dataUploadToCloud = false;
  selectedDbBackup = '';
  selectedMinioBackup = '';
  validatingFile = signal<string | null>(null);
  validationResult = signal<BackupValidationResult | null>(null);

  // Strategy state
  strategies = signal<DataBackupStrategy[]>([]);
  showStrategyForm = false;
  editingStrategyId: string | null = null;
  strategySaving = false;
  strategyForm = {
    name: '',
    description: '',
    includeDatabase: true,
    includeMinio: true,
    cronExpression: '0 2 * * *',
    uploadToCloud: false,
    retentionDays: 30,
    maxBackupCount: 10,
  };

  // 3-2-1 Compliance state
  compliance = signal<ComplianceReport | null>(null);
  complianceLoading = false;

  // WAL state
  walStatus = signal<WalStatusResponse | null>(null);
  walLoading = false;
  walEnabling = false;
  walSwitching = false;
  baseBackupRunning = false;
  baseBackups = signal<DataBackupFile[]>([]);

  // WAL archives state
  walArchives = signal<WalArchiveListResponse | null>(null);

  // PITR state
  pitrRestoreRunning = false;
  pitrDryRun = true;
  pitrTargetTime = '';
  pitrSelectedBackup = '';
  pitrOperationId = '';
  pitrLogs = signal<BackupLogLine[]>([]);
  showPitrPanel = false;

  // Replication state
  replicationStatus = signal<ReplicationStatus | null>(null);
  replicationGuide = signal<ReplicationSetupGuide | null>(null);
  replicationLoading = false;
  replicationActivating = false;
  showReplicationGuide = false;
  newSlotName = '';

  // Cloud replication state
  cloudReplConfig = signal<CloudReplicationConfig | null>(null);
  dbReplStatus = signal<DbCloudReplicationStatus | null>(null);
  minioReplStatus = signal<MinioCloudReplicationStatus | null>(null);
  cloudReplGuide = signal<ExternalReplicationGuide | null>(null);
  cloudReplSetupResult = signal<CloudReplicationSetupResult | null>(null);
  minioSyncResult = signal<MinioSyncResult | null>(null);
  cloudReplLoading = false;
  cloudReplDbTesting = false;
  cloudReplDbSetting = false;
  cloudReplMinioTesting = false;
  cloudReplMinioSetting = false;
  cloudReplMinioSyncing = false;
  showCloudReplGuide = false;
  cloudReplDbForm = {
    enabled: false,
    remoteHost: '',
    remotePort: 5432,
    remoteUser: 'replicator',
    remotePassword: '',
    sslMode: 'require',
    slotName: 'cloud_standby_slot',
    allowedIps: '',
  };
  cloudReplMinioForm = {
    enabled: false,
    endpoint: '',
    accessKey: '',
    secretKey: '',
    bucket: 'ivf-replica',
    useSsl: true,
    region: 'us-east-1',
    syncMode: 'incremental',
    syncCron: '0 */2 * * *',
  };

  private subscriptions: Subscription[] = [];
  private refreshInterval: any;

  constructor(
    private backupService: BackupService,
    private notify: GlobalNotificationService,
  ) {}

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
    if (tab === 'overview') {
      this.loadDataStatus();
      this.loadCompliance();
    }
    if (tab === 'pki') this.loadArchives();
    if (tab === 'archives') this.loadArchives();
    if (tab === 'history') this.loadOperations();
    if (tab === 'schedule') this.loadSchedule();
    if (tab === 'cloud') this.loadCloudData();
    if (tab === 'data') this.loadDataStatus();
    if (tab === 'db-overview') {
      this.loadDataStatus();
      this.loadWalStatus();
      this.loadReplicationStatus();
    }
    if (tab === 'minio-overview') this.loadDataStatus();
    if (tab === 'strategies') this.loadStrategies();
    if (tab === 'compliance') this.loadCompliance();
    if (tab === 'wal') this.loadWalStatus();
    if (tab === 'replication') this.loadReplicationStatus();
    if (tab === 'cloud-replication') this.loadCloudReplication();
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
          this.notify.success('Dọn dẹp', `Đã xóa ${res.deletedCount} bản sao lưu cũ`);
        } else {
          this.notify.info('Dọn dẹp', 'Không có bản sao lưu cũ cần xóa');
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
        s3AccessKey: this.cloudConfigS3AccessKey,
        s3SecretKey: this.cloudConfigS3SecretKey,
        s3ServiceUrl: this.cloudConfigS3ServiceUrl,
        s3ForcePathStyle: this.cloudConfigS3ForcePathStyle,
        azureConnectionString: this.cloudConfigAzureConnStr,
        azureContainerName: this.cloudConfigAzureContainer,
        gcsProjectId: this.cloudConfigGcsProjectId,
        gcsBucketName: this.cloudConfigGcsBucket,
        gcsCredentialsPath: this.cloudConfigGcsCredentialsPath,
      })
      .subscribe({
        next: () => {
          this.cloudConfigSaving = false;
          this.loadCloudData();
        },
        error: (err) => {
          this.cloudConfigSaving = false;
          console.error('Failed to save cloud config', err);
          this.notify.error(
            'Cấu hình',
            'Lưu cấu hình thất bại: ' + (err.error?.error || err.message),
          );
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
        this.notify.error('Cloud', 'Upload thất bại: ' + (err.error?.error || err.message));
      },
    });
  }

  downloadFromCloud(objectKey: string) {
    this.cloudDownloading.set(objectKey);
    this.backupService.downloadFromCloud(objectKey).subscribe({
      next: (res) => {
        this.cloudDownloading.set(null);
        this.loadArchives();
        this.notify.success('Cloud', `Tải về thành công: ${res.fileName}`);
      },
      error: (err) => {
        this.cloudDownloading.set(null);
        console.error('Cloud download failed', err);
        this.notify.error('Cloud', 'Tải về thất bại: ' + (err.error?.error || err.message));
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

  // ─── Data Backup ──────────────────────────────────────

  loadDataStatus() {
    this.backupService.getDataBackupStatus().subscribe({
      next: (status) => this.dataStatus.set(status),
      error: (err) => console.error('Failed to load data backup status', err),
    });
  }

  startDataBackup() {
    this.dataBackupRunning = true;
    this.backupService
      .startDataBackup({
        includeDatabase: this.dataIncludeDatabase,
        includeMinio: this.dataIncludeMinio,
        uploadToCloud: this.dataUploadToCloud,
      })
      .subscribe({
        next: () => {
          this.dataBackupRunning = false;
          this.loadDataStatus();
        },
        error: (err) => {
          this.dataBackupRunning = false;
          console.error('Data backup failed', err);
          this.notify.error(
            'Sao lưu',
            'Sao lưu dữ liệu thất bại: ' + (err.error?.error || err.message),
          );
        },
      });
  }

  quickFullBackup() {
    this.dataIncludeDatabase = true;
    this.dataIncludeMinio = true;
    this.startDataBackup();
  }

  quickDbBackup() {
    this.dataIncludeDatabase = true;
    this.dataIncludeMinio = false;
    this.startDataBackup();
  }

  quickMinioBackup() {
    this.dataIncludeDatabase = false;
    this.dataIncludeMinio = true;
    this.startDataBackup();
  }

  startDataRestore() {
    if (!this.selectedDbBackup && !this.selectedMinioBackup) return;

    this.confirmMessage =
      'Khôi phục dữ liệu từ bản sao lưu? Thao tác này sẽ ghi đè dữ liệu hiện tại.';
    this.confirmAction = () => this.doDataRestore();
    this.showConfirmDialog = true;
  }

  private doDataRestore() {
    this.dataRestoreRunning = true;
    this.backupService
      .startDataRestore({
        databaseBackupFile: this.selectedDbBackup || undefined,
        minioBackupFile: this.selectedMinioBackup || undefined,
      })
      .subscribe({
        next: () => {
          this.dataRestoreRunning = false;
          this.loadDataStatus();
          this.notify.success('Khôi phục', 'Khôi phục dữ liệu thành công!');
        },
        error: (err) => {
          this.dataRestoreRunning = false;
          console.error('Data restore failed', err);
          this.notify.error(
            'Khôi phục',
            'Khôi phục dữ liệu thất bại: ' + (err.error?.error || err.message),
          );
        },
      });
  }

  deleteDataBackup(fileName: string) {
    this.confirmMessage = `Xóa bản sao lưu "${fileName}"? Thao tác này không thể hoàn tác.`;
    this.confirmAction = () => {
      this.backupService.deleteDataBackup(fileName).subscribe({
        next: () => this.loadDataStatus(),
        error: (err) => console.error('Delete data backup failed', err),
      });
    };
    this.showConfirmDialog = true;
  }

  validateBackup(fileName: string) {
    this.validatingFile.set(fileName);
    this.validationResult.set(null);
    this.backupService.validateBackup(fileName).subscribe({
      next: (result) => {
        this.validatingFile.set(null);
        this.validationResult.set(result);
      },
      error: (err) => {
        this.validatingFile.set(null);
        this.validationResult.set({
          type: fileName.startsWith('ivf_db_') ? 'database' : 'minio',
          isValid: false,
          error: err.error?.error || err.message,
        });
      },
    });
  }

  dismissValidation() {
    this.validationResult.set(null);
  }

  // ─── Strategies ───────────────────────────────────────

  loadStrategies() {
    this.backupService.listStrategies().subscribe({
      next: (data) => this.strategies.set(data),
      error: (err) => console.error('Failed to load strategies', err),
    });
  }

  openNewStrategyForm() {
    this.editingStrategyId = null;
    this.strategyForm = {
      name: '',
      description: '',
      includeDatabase: true,
      includeMinio: true,
      cronExpression: '0 2 * * *',
      uploadToCloud: false,
      retentionDays: 30,
      maxBackupCount: 10,
    };
    this.showStrategyForm = true;
  }

  editStrategy(s: DataBackupStrategy) {
    this.editingStrategyId = s.id;
    this.strategyForm = {
      name: s.name,
      description: s.description ?? '',
      includeDatabase: s.includeDatabase,
      includeMinio: s.includeMinio,
      cronExpression: s.cronExpression,
      uploadToCloud: s.uploadToCloud,
      retentionDays: s.retentionDays,
      maxBackupCount: s.maxBackupCount,
    };
    this.showStrategyForm = true;
  }

  cancelStrategyForm() {
    this.showStrategyForm = false;
    this.editingStrategyId = null;
  }

  saveStrategy() {
    if (!this.strategyForm.name.trim()) return;
    this.strategySaving = true;

    if (this.editingStrategyId) {
      this.backupService
        .updateStrategy(this.editingStrategyId, {
          name: this.strategyForm.name,
          description: this.strategyForm.description || undefined,
          includeDatabase: this.strategyForm.includeDatabase,
          includeMinio: this.strategyForm.includeMinio,
          cronExpression: this.strategyForm.cronExpression,
          uploadToCloud: this.strategyForm.uploadToCloud,
          retentionDays: this.strategyForm.retentionDays,
          maxBackupCount: this.strategyForm.maxBackupCount,
        })
        .subscribe({
          next: () => {
            this.strategySaving = false;
            this.showStrategyForm = false;
            this.loadStrategies();
          },
          error: (err) => {
            this.strategySaving = false;
            this.notify.error('Chiến lược', 'Lưu thất bại: ' + (err.error?.error || err.message));
          },
        });
    } else {
      this.backupService
        .createStrategy({
          name: this.strategyForm.name,
          description: this.strategyForm.description || undefined,
          includeDatabase: this.strategyForm.includeDatabase,
          includeMinio: this.strategyForm.includeMinio,
          cronExpression: this.strategyForm.cronExpression,
          uploadToCloud: this.strategyForm.uploadToCloud,
          retentionDays: this.strategyForm.retentionDays,
          maxBackupCount: this.strategyForm.maxBackupCount,
        })
        .subscribe({
          next: () => {
            this.strategySaving = false;
            this.showStrategyForm = false;
            this.loadStrategies();
          },
          error: (err) => {
            this.strategySaving = false;
            this.notify.error('Chiến lược', 'Tạo thất bại: ' + (err.error?.error || err.message));
          },
        });
    }
  }

  toggleStrategy(s: DataBackupStrategy) {
    this.backupService.updateStrategy(s.id, { enabled: !s.enabled }).subscribe({
      next: () => this.loadStrategies(),
      error: (err) => console.error('Toggle strategy failed', err),
    });
  }

  deleteStrategy(s: DataBackupStrategy) {
    this.confirmMessage = `Xóa chiến lược "${s.name}"? Thao tác này không thể hoàn tác.`;
    this.confirmAction = () => {
      this.backupService.deleteStrategy(s.id).subscribe({
        next: () => this.loadStrategies(),
        error: (err) => console.error('Delete strategy failed', err),
      });
    };
    this.showConfirmDialog = true;
  }

  runStrategy(s: DataBackupStrategy) {
    this.backupService.runStrategy(s.id).subscribe({
      next: (res) => {
        this.notify.success('Chiến lược', `Đã bắt đầu chạy "${s.name}"`);
        this.loadStrategies();
        this.watchOperation(res.operationId);
      },
      error: (err) => {
        console.error('Run strategy failed', err);
        this.notify.error('Chiến lược', 'Chạy thất bại: ' + (err.error?.error || err.message));
      },
    });
  }

  // ─── 3-2-1 Compliance ────────────────────────────────

  loadCompliance() {
    this.complianceLoading = true;
    this.backupService.getCompliance().subscribe({
      next: (report) => {
        this.compliance.set(report);
        this.complianceLoading = false;
      },
      error: (err) => {
        this.complianceLoading = false;
        console.error('Failed to load compliance', err);
      },
    });
  }

  getComplianceColor(score: number, max: number): string {
    const pct = (score / max) * 100;
    if (pct >= 100) return '#2e7d32';
    if (pct >= 60) return '#f57f17';
    return '#c62828';
  }

  // ─── WAL ─────────────────────────────────────────────

  loadWalStatus() {
    this.walLoading = true;
    this.backupService.getWalStatus().subscribe({
      next: (data) => {
        this.walStatus.set(data);
        this.walLoading = false;
      },
      error: (err) => {
        this.walLoading = false;
        console.error('Failed to load WAL status', err);
      },
    });
    this.backupService.listBaseBackups().subscribe({
      next: (data) => this.baseBackups.set(data),
      error: (err) => console.error('Failed to load base backups', err),
    });
    this.backupService.listWalArchives().subscribe({
      next: (data) => this.walArchives.set(data),
      error: (err) => console.error('Failed to load WAL archives', err),
    });
  }

  enableWalArchiving() {
    this.walEnabling = true;
    this.backupService.enableWalArchiving().subscribe({
      next: (res) => {
        this.walEnabling = false;
        this.notify.success('WAL', res.message);
        this.loadWalStatus();
      },
      error: (err) => {
        this.walEnabling = false;
        this.notify.error('WAL', 'Lỗi: ' + (err.error?.error || err.message));
      },
    });
  }

  switchWal() {
    this.walSwitching = true;
    this.backupService.switchWal().subscribe({
      next: (res) => {
        this.walSwitching = false;
        this.notify.success('WAL', res.message);
        this.loadWalStatus();
      },
      error: (err) => {
        this.walSwitching = false;
        this.notify.error('WAL', 'Lỗi: ' + (err.error?.error || err.message));
      },
    });
  }

  createBaseBackup() {
    this.baseBackupRunning = true;
    this.backupService.createBaseBackup().subscribe({
      next: (res) => {
        this.baseBackupRunning = false;
        this.notify.success(
          'Base Backup',
          `Tạo thành công: ${res.fileName} (${this.formatSize(res.sizeBytes)})`,
        );
        this.loadWalStatus();
      },
      error: (err) => {
        this.baseBackupRunning = false;
        this.notify.error('Base Backup', 'Thất bại: ' + (err.error?.error || err.message));
      },
    });
  }

  // ─── PITR Restore ───────────────────────────────────

  togglePitrPanel() {
    this.showPitrPanel = !this.showPitrPanel;
    if (this.showPitrPanel && this.baseBackups().length === 0) {
      this.backupService.listBaseBackups().subscribe({
        next: (data) => this.baseBackups.set(data),
      });
    }
  }

  startPitrRestore() {
    if (!this.pitrSelectedBackup) {
      this.notify.warning('PITR', 'Vui lòng chọn base backup');
      return;
    }
    const action = this.pitrDryRun ? 'DRY-RUN' : 'THỰC THI';
    const timeMsg = this.pitrTargetTime
      ? `tới thời điểm ${this.pitrTargetTime}`
      : 'tới điểm mới nhất (latest)';
    this.confirmMessage = `${action} PITR restore từ "${this.pitrSelectedBackup}" ${timeMsg}?`;
    this.confirmAction = () => this.executePitrRestore();
    this.showConfirmDialog = true;
  }

  private executePitrRestore() {
    this.pitrRestoreRunning = true;
    this.pitrLogs.set([]);
    this.backupService
      .startPitrRestore({
        baseBackupFile: this.pitrSelectedBackup,
        targetTime: this.pitrTargetTime || undefined,
        dryRun: this.pitrDryRun,
      })
      .subscribe({
        next: (res) => {
          this.pitrOperationId = res.operationId;
          // Connect to SignalR for live logs
          this.backupService.connectHub(res.operationId);
          const logSub = this.backupService.logLine$.subscribe((line) => {
            if (line.operationId === res.operationId) {
              this.pitrLogs.update((logs) => [
                ...logs,
                { timestamp: line.timestamp, level: line.level, message: line.message },
              ]);
              this.scrollToBottom();
            }
          });
          const statusSub = this.backupService.statusChanged$.subscribe((op) => {
            if (op.id === res.operationId && op.status !== 'Running') {
              this.pitrRestoreRunning = false;
              logSub.unsubscribe();
              statusSub.unsubscribe();
              this.backupService.disconnectHub();
              if (op.status === 'Completed') {
                this.notify.success('PITR', 'PITR restore hoàn tất thành công!');
              } else {
                this.notify.error(
                  'PITR',
                  'PITR restore thất bại: ' + (op.errorMessage || 'Unknown error'),
                );
              }
            }
          });
          this.subscriptions.push(logSub, statusSub);
        },
        error: (err) => {
          this.pitrRestoreRunning = false;
          this.notify.error('PITR', 'Lỗi khởi chạy PITR: ' + (err.error?.error || err.message));
        },
      });
  }

  // ─── Replication ─────────────────────────────────────

  loadReplicationStatus() {
    this.replicationLoading = true;
    this.backupService.getReplicationStatus().subscribe({
      next: (data) => {
        this.replicationStatus.set(data);
        this.replicationLoading = false;
      },
      error: (err) => {
        this.replicationLoading = false;
        console.error('Failed to load replication status', err);
      },
    });
  }

  loadReplicationGuide() {
    this.showReplicationGuide = true;
    if (!this.replicationGuide()) {
      this.backupService.getReplicationGuide().subscribe({
        next: (guide) => this.replicationGuide.set(guide),
        error: (err) => console.error('Failed to load guide', err),
      });
    }
  }

  activateReplication() {
    this.confirmMessage =
      'Kích hoạt WAL archiving và tạo replication slot? Sau đó cần restart PostgreSQL container.';
    this.confirmAction = () => {
      this.replicationActivating = true;
      this.backupService.activateReplication().subscribe({
        next: (res) => {
          this.replicationActivating = false;
          const stepsMsg = res.steps.join(', ');
          this.notify.success('Replication', `${stepsMsg}. ${res.nextAction}`);
          this.loadReplicationStatus();
          this.loadWalStatus();
        },
        error: (err) => {
          this.replicationActivating = false;
          this.notify.error(
            'Replication',
            'Kích hoạt thất bại: ' + (err.error?.error || err.message),
          );
        },
      });
    };
    this.showConfirmDialog = true;
  }

  createReplicationSlot() {
    if (!this.newSlotName.trim()) return;
    this.backupService.createReplicationSlot(this.newSlotName.trim()).subscribe({
      next: (res) => {
        this.notify.success('Replication', res.message);
        this.newSlotName = '';
        this.loadReplicationStatus();
      },
      error: (err) => this.notify.error('Replication', 'Lỗi: ' + (err.error?.error || err.message)),
    });
  }

  dropReplicationSlot(slotName: string) {
    this.confirmMessage = `Xóa replication slot "${slotName}"?`;
    this.confirmAction = () => {
      this.backupService.dropReplicationSlot(slotName).subscribe({
        next: () => this.loadReplicationStatus(),
        error: (err) =>
          this.notify.error('Replication', 'Lỗi: ' + (err.error?.error || err.message)),
      });
    };
    this.showConfirmDialog = true;
  }

  // ─── Cloud Replication ───────────────────────────────

  loadCloudReplication() {
    this.cloudReplLoading = true;
    this.backupService.getCloudReplicationConfig().subscribe({
      next: (config) => {
        this.cloudReplConfig.set(config);
        this.cloudReplDbForm = {
          enabled: config.dbReplicationEnabled,
          remoteHost: config.remoteDbHost || '',
          remotePort: config.remoteDbPort,
          remoteUser: config.remoteDbUser || 'replicator',
          remotePassword: '',
          sslMode: config.remoteDbSslMode || 'require',
          slotName: config.remoteDbSlotName || 'cloud_standby_slot',
          allowedIps: config.remoteDbAllowedIps || '',
        };
        this.cloudReplMinioForm = {
          enabled: config.minioReplicationEnabled,
          endpoint: config.remoteMinioEndpoint || '',
          accessKey: config.remoteMinioAccessKey || '',
          secretKey: '',
          bucket: config.remoteMinioBucket || 'ivf-replica',
          useSsl: config.remoteMinioUseSsl,
          region: config.remoteMinioRegion || 'us-east-1',
          syncMode: config.remoteMinioSyncMode || 'incremental',
          syncCron: config.remoteMinioSyncCron || '0 */2 * * *',
        };
        this.cloudReplLoading = false;
        this.loadDbReplStatus();
        this.loadMinioReplStatus();
      },
      error: (err) => {
        this.cloudReplLoading = false;
        console.error('Failed to load cloud replication config', err);
      },
    });
  }

  loadDbReplStatus() {
    this.backupService.getDbReplicationStatus().subscribe({
      next: (status) => this.dbReplStatus.set(status),
      error: (err) => console.error('Failed to load DB repl status', err),
    });
  }

  loadMinioReplStatus() {
    this.backupService.getMinioReplicationStatus().subscribe({
      next: (status) => this.minioReplStatus.set(status),
      error: (err) => console.error('Failed to load MinIO repl status', err),
    });
  }

  saveDbReplConfig() {
    this.cloudReplDbSetting = true;
    this.backupService
      .updateDbReplicationConfig({
        enabled: this.cloudReplDbForm.enabled,
        remoteHost: this.cloudReplDbForm.remoteHost || undefined,
        remotePort: this.cloudReplDbForm.remotePort,
        remoteUser: this.cloudReplDbForm.remoteUser || undefined,
        remotePassword: this.cloudReplDbForm.remotePassword || undefined,
        sslMode: this.cloudReplDbForm.sslMode,
        slotName: this.cloudReplDbForm.slotName || undefined,
        allowedIps: this.cloudReplDbForm.allowedIps || undefined,
      })
      .subscribe({
        next: (config) => {
          this.cloudReplConfig.set(config);
          this.cloudReplDbSetting = false;
          this.notify.success('Cloud Replication', 'Cấu hình DB replication đã lưu');
        },
        error: (err) => {
          this.cloudReplDbSetting = false;
          this.notify.error('Cloud Replication', 'Lỗi: ' + (err.error?.error || err.message));
        },
      });
  }

  testDbReplication() {
    this.cloudReplDbTesting = true;
    this.backupService.testDbReplicationConnection().subscribe({
      next: (res) => {
        this.cloudReplDbTesting = false;
        if (res.success) this.notify.success('Cloud Repl', res.message);
        else this.notify.warning('Cloud Repl', res.message);
      },
      error: (err) => {
        this.cloudReplDbTesting = false;
        this.notify.error('Cloud Repl', 'Lỗi: ' + (err.error?.error || err.message));
      },
    });
  }

  setupDbReplication() {
    this.confirmMessage =
      'Thiết lập PostgreSQL replication cho remote standby? Sẽ tạo user, slot và cấu hình pg_hba.conf.';
    this.confirmAction = () => {
      this.cloudReplDbSetting = true;
      this.backupService.setupDbReplication().subscribe({
        next: (result) => {
          this.cloudReplDbSetting = false;
          this.cloudReplSetupResult.set(result);
          if (result.success) {
            this.notify.success('Cloud Repl', 'Thiết lập DB replication thành công');
            this.loadDbReplStatus();
          } else {
            this.notify.warning('Cloud Repl', result.steps.join(', '));
          }
        },
        error: (err) => {
          this.cloudReplDbSetting = false;
          this.notify.error('Cloud Repl', 'Lỗi: ' + (err.error?.error || err.message));
        },
      });
    };
    this.showConfirmDialog = true;
  }

  saveMinioReplConfig() {
    this.cloudReplMinioSetting = true;
    this.backupService
      .updateMinioReplicationConfig({
        enabled: this.cloudReplMinioForm.enabled,
        endpoint: this.cloudReplMinioForm.endpoint || undefined,
        accessKey: this.cloudReplMinioForm.accessKey || undefined,
        secretKey: this.cloudReplMinioForm.secretKey || undefined,
        bucket: this.cloudReplMinioForm.bucket,
        useSsl: this.cloudReplMinioForm.useSsl,
        region: this.cloudReplMinioForm.region,
        syncMode: this.cloudReplMinioForm.syncMode,
        syncCron: this.cloudReplMinioForm.syncCron,
      })
      .subscribe({
        next: (config) => {
          this.cloudReplConfig.set(config);
          this.cloudReplMinioSetting = false;
          this.notify.success('Cloud Replication', 'Cấu hình MinIO replication đã lưu');
        },
        error: (err) => {
          this.cloudReplMinioSetting = false;
          this.notify.error('Cloud Replication', 'Lỗi: ' + (err.error?.error || err.message));
        },
      });
  }

  testMinioReplication() {
    this.cloudReplMinioTesting = true;
    this.backupService.testMinioReplicationConnection().subscribe({
      next: (res) => {
        this.cloudReplMinioTesting = false;
        if (res.success) this.notify.success('Cloud Repl', res.message);
        else this.notify.warning('Cloud Repl', res.message);
      },
      error: (err) => {
        this.cloudReplMinioTesting = false;
        this.notify.error('Cloud Repl', 'Lỗi: ' + (err.error?.error || err.message));
      },
    });
  }

  setupMinioReplication() {
    this.cloudReplMinioSetting = true;
    this.backupService.setupMinioReplication().subscribe({
      next: (res) => {
        this.cloudReplMinioSetting = false;
        if (res.success) {
          this.notify.success('Cloud Repl', res.message);
          this.loadMinioReplStatus();
        } else {
          this.notify.warning('Cloud Repl', res.message);
        }
      },
      error: (err) => {
        this.cloudReplMinioSetting = false;
        this.notify.error('Cloud Repl', 'Lỗi: ' + (err.error?.error || err.message));
      },
    });
  }

  syncMinioNow() {
    this.cloudReplMinioSyncing = true;
    this.backupService.syncMinioNow().subscribe({
      next: (result) => {
        this.cloudReplMinioSyncing = false;
        this.minioSyncResult.set(result);
        if (result.success) {
          this.notify.success(
            'Cloud Repl',
            `Đồng bộ ${result.totalFiles} files (${this.formatSize(result.totalBytes)})`,
          );
          this.loadMinioReplStatus();
        } else {
          this.notify.warning('Cloud Repl', result.message);
        }
      },
      error: (err) => {
        this.cloudReplMinioSyncing = false;
        this.notify.error('Cloud Repl', 'Lỗi: ' + (err.error?.error || err.message));
      },
    });
  }

  loadCloudReplGuide() {
    this.showCloudReplGuide = true;
    if (!this.cloudReplGuide()) {
      this.backupService.getCloudReplicationGuide().subscribe({
        next: (guide) => this.cloudReplGuide.set(guide),
        error: (err) => console.error('Failed to load cloud repl guide', err),
      });
    }
  }

  formatUptime(seconds: number): string {
    if (seconds < 60) return `${seconds}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m`;
    if (seconds < 86400)
      return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`;
    return `${Math.floor(seconds / 86400)}d ${Math.floor((seconds % 86400) / 3600)}h`;
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
    this.activeTab = 'pki';
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
