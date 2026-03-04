import { Component, OnInit, OnDestroy, inject, signal, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ComplianceService } from '../../../core/services/compliance.service';
import {
  EvidenceAccessControl,
  EvidenceTraining,
  EvidenceIncidents,
  EvidenceBackup,
  EvidenceAssets,
  EvidenceSummary,
  EvidenceCategory,
  EvidenceLogLine,
  EvidenceFile,
  EvidenceProgress,
} from '../../../core/models/compliance.model';

@Component({
  selector: 'app-evidence-collection',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './evidence-collection.component.html',
  styleUrls: ['./evidence-collection.component.scss'],
})
export class EvidenceCollectionComponent implements OnInit, OnDestroy {
  private complianceService = inject(ComplianceService);
  private subs: Subscription[] = [];

  @ViewChild('logContainer') logContainer?: ElementRef<HTMLDivElement>;

  activeTab = signal<EvidenceCategory | 'overview' | 'runner'>('overview');
  loading = signal(false);
  exporting = signal<string | null>(null);

  accessControl = signal<EvidenceAccessControl | null>(null);
  training = signal<EvidenceTraining | null>(null);
  incidents = signal<EvidenceIncidents | null>(null);
  backup = signal<EvidenceBackup | null>(null);
  assets = signal<EvidenceAssets | null>(null);
  summary = signal<EvidenceSummary | null>(null);

  // Script runner state
  scriptRunning = signal(false);
  scriptStatus = signal<string>('idle');
  operationId = signal<string | null>(null);
  logLines = signal<EvidenceLogLine[]>([]);
  progress = signal<EvidenceProgress | null>(null);
  evidenceFiles = signal<EvidenceFile[]>([]);
  filesLoading = signal(false);

  // Script config
  selectedCategories: Record<string, boolean> = {
    access_control: true,
    incident_response: true,
    training: true,
    change_management: true,
    encryption: true,
    backup: true,
    vendor: true,
    policy_versions: true,
  };
  skipApi = false;

  allScriptCategories = [
    { key: 'access_control', label: 'Kiểm soát truy cập', icon: '🔐' },
    { key: 'incident_response', label: 'Phản ứng sự cố', icon: '🚨' },
    { key: 'training', label: 'Đào tạo', icon: '📚' },
    { key: 'change_management', label: 'Quản lý thay đổi', icon: '🔄' },
    { key: 'encryption', label: 'Mã hóa & Chứng chỉ', icon: '🔒' },
    { key: 'backup', label: 'Sao lưu', icon: '💾' },
    { key: 'vendor', label: 'Nhà cung cấp', icon: '📦' },
    { key: 'policy_versions', label: 'Phiên bản chính sách', icon: '📜' },
  ];

  categories: { key: EvidenceCategory; icon: string; label: string; desc: string }[] = [
    {
      key: 'access-control',
      icon: '🔐',
      label: 'Kiểm soát truy cập',
      desc: 'User lists, roles, MFA, sessions',
    },
    { key: 'training', icon: '📚', label: 'Đào tạo', desc: 'Completion records, test scores' },
    {
      key: 'incidents',
      icon: '🚨',
      label: 'Sự cố & Breach',
      desc: 'Incident tickets, response times',
    },
    {
      key: 'backup',
      icon: '💾',
      label: 'Sao lưu & Lưu trữ',
      desc: 'Retention policies, backup status',
    },
    { key: 'assets', icon: '🗃️', label: 'Tài sản dữ liệu', desc: 'Asset inventory, risk scores' },
    {
      key: 'summary',
      icon: '📊',
      label: 'Tổng hợp Compliance',
      desc: 'Framework scores, controls status',
    },
  ];

  ngOnInit() {
    this.loadSummary();
    this.subscribeToHub();
  }

  ngOnDestroy() {
    this.subs.forEach((s) => s.unsubscribe());
    this.complianceService.disconnectEvidenceHub();
  }

  private subscribeToHub() {
    this.subs.push(
      this.complianceService.logLine$.subscribe((line) => {
        this.logLines.update((lines) => [...lines, line]);
        setTimeout(() => this.scrollLogToBottom(), 50);
      }),
      this.complianceService.progressChanged$.subscribe((p) => {
        this.progress.set(p);
      }),
      this.complianceService.statusChanged$.subscribe((status) => {
        this.scriptStatus.set(status.status);
        if (
          status.status === 'Completed' ||
          status.status === 'Failed' ||
          status.status === 'Cancelled'
        ) {
          this.scriptRunning.set(false);
          this.loadEvidenceFiles();
        }
      }),
    );
  }

  setTab(tab: EvidenceCategory | 'overview' | 'runner') {
    this.activeTab.set(tab);
    if (tab === 'overview') {
      if (!this.summary()) this.loadSummary();
    } else if (tab === 'runner') {
      this.loadEvidenceFiles();
    } else {
      this.loadCategory(tab);
    }
  }

  loadSummary() {
    this.loading.set(true);
    this.complianceService.getEvidenceSummary().subscribe({
      next: (data) => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadCategory(cat: EvidenceCategory) {
    this.loading.set(true);
    switch (cat) {
      case 'access-control':
        if (this.accessControl()) {
          this.loading.set(false);
          return;
        }
        this.complianceService.getEvidenceAccessControl().subscribe({
          next: (d) => {
            this.accessControl.set(d);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
        break;
      case 'training':
        if (this.training()) {
          this.loading.set(false);
          return;
        }
        this.complianceService.getEvidenceTraining().subscribe({
          next: (d) => {
            this.training.set(d);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
        break;
      case 'incidents':
        if (this.incidents()) {
          this.loading.set(false);
          return;
        }
        this.complianceService.getEvidenceIncidents().subscribe({
          next: (d) => {
            this.incidents.set(d);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
        break;
      case 'backup':
        if (this.backup()) {
          this.loading.set(false);
          return;
        }
        this.complianceService.getEvidenceBackup().subscribe({
          next: (d) => {
            this.backup.set(d);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
        break;
      case 'assets':
        if (this.assets()) {
          this.loading.set(false);
          return;
        }
        this.complianceService.getEvidenceAssets().subscribe({
          next: (d) => {
            this.assets.set(d);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
        break;
      case 'summary':
        if (this.summary()) {
          this.loading.set(false);
          return;
        }
        this.loadSummary();
        break;
    }
  }

  reloadCategory(cat: EvidenceCategory) {
    // Force reload
    switch (cat) {
      case 'access-control':
        this.accessControl.set(null);
        break;
      case 'training':
        this.training.set(null);
        break;
      case 'incidents':
        this.incidents.set(null);
        break;
      case 'backup':
        this.backup.set(null);
        break;
      case 'assets':
        this.assets.set(null);
        break;
      case 'summary':
        this.summary.set(null);
        break;
    }
    this.loadCategory(cat);
  }

  exportCsv(cat: EvidenceCategory) {
    this.exporting.set(cat);
    // Load data first if needed, then export
    const doExport = () => {
      let rows: string[][] = [];
      let filename = '';

      switch (cat) {
        case 'access-control': {
          const d = this.accessControl();
          if (!d) return;
          filename = `evidence_access_control_${this.dateStr()}.csv`;
          rows = [['ID', 'Username', 'FullName', 'Role', 'Department', 'IsActive', 'CreatedAt']];
          d.users.forEach((u) =>
            rows.push([
              u.id,
              u.username,
              u.fullName,
              u.role,
              u.department || '',
              String(u.isActive),
              u.createdAt,
            ]),
          );
          break;
        }
        case 'training': {
          const d = this.training();
          if (!d) return;
          filename = `evidence_training_${this.dateStr()}.csv`;
          rows = [
            [
              'ID',
              'UserID',
              'Type',
              'Name',
              'AssignedAt',
              'DueDate',
              'Completed',
              'CompletedAt',
              'Score',
              'Passed',
              'Overdue',
            ],
          ];
          d.records.forEach((r) =>
            rows.push([
              r.id,
              r.userId,
              r.trainingType,
              r.trainingName,
              r.assignedAt,
              r.dueDate,
              String(r.isCompleted),
              r.completedAt || '',
              String(r.scorePercent),
              String(r.isPassed),
              String(r.isOverdue),
            ]),
          );
          break;
        }
        case 'incidents': {
          const d = this.incidents();
          if (!d) return;
          filename = `evidence_incidents_${this.dateStr()}.csv`;
          rows = [
            [
              'ID',
              'Type',
              'Severity',
              'Status',
              'DetectedAt',
              'ResolvedAt',
              'Description',
              'Username',
            ],
          ];
          d.incidents.records.forEach((r) =>
            rows.push([
              r.id,
              r.incidentType,
              r.severity,
              r.status,
              r.detectedAt,
              r.resolvedAt || '',
              r.description || '',
              r.username || '',
            ]),
          );
          break;
        }
        case 'assets': {
          const d = this.assets();
          if (!d) return;
          filename = `evidence_assets_${this.dateStr()}.csv`;
          rows = [
            [
              'ID',
              'Name',
              'Type',
              'Status',
              'Owner',
              'Department',
              'PHI',
              'PII',
              'RiskScore',
              'LastAudit',
              'NextAudit',
              'Overdue',
            ],
          ];
          d.records.forEach((r) =>
            rows.push([
              r.id,
              r.assetName,
              r.assetType,
              r.status,
              r.owner,
              r.department || '',
              String(r.containsPhi),
              String(r.containsPii),
              String(r.riskScore),
              r.lastAuditedAt || '',
              r.nextAuditDueAt || '',
              String(r.overdue),
            ]),
          );
          break;
        }
        case 'summary': {
          const d = this.summary();
          if (!d) return;
          filename = `evidence_compliance_summary_${this.dateStr()}.csv`;
          rows = [
            ['Framework', 'Score', 'MaxScore', 'Percentage', 'ControlID', 'ControlName', 'Status'],
          ];
          d.compliance.frameworks.forEach((f) => {
            f.controls.forEach((c) =>
              rows.push([
                f.name,
                String(f.score),
                String(f.maxScore),
                String(f.percentage),
                c.controlId,
                c.name,
                c.status,
              ]),
            );
          });
          break;
        }
        default:
          return;
      }

      this.downloadCsv(rows, filename);
      this.exporting.set(null);
    };

    // Ensure data is loaded
    const hasData = () => {
      switch (cat) {
        case 'access-control':
          return !!this.accessControl();
        case 'training':
          return !!this.training();
        case 'incidents':
          return !!this.incidents();
        case 'assets':
          return !!this.assets();
        case 'summary':
          return !!this.summary();
        default:
          return false;
      }
    };

    if (hasData()) {
      doExport();
    } else {
      this.loadCategory(cat);
      // Wait for data to load
      const interval = setInterval(() => {
        if (hasData() || !this.loading()) {
          clearInterval(interval);
          doExport();
        }
      }, 200);
    }
  }

  exportAllJson() {
    this.exporting.set('all');
    const allData: any = {};
    let loaded = 0;
    const total = 6;

    const checkDone = () => {
      loaded++;
      if (loaded >= total) {
        const blob = new Blob([JSON.stringify(allData, null, 2)], { type: 'application/json' });
        this.downloadBlob(blob, `evidence_full_export_${this.dateStr()}.json`);
        this.exporting.set(null);
      }
    };

    this.complianceService.getEvidenceAccessControl().subscribe({
      next: (d) => {
        allData.accessControl = d;
        checkDone();
      },
      error: () => checkDone(),
    });
    this.complianceService.getEvidenceTraining().subscribe({
      next: (d) => {
        allData.training = d;
        checkDone();
      },
      error: () => checkDone(),
    });
    this.complianceService.getEvidenceIncidents().subscribe({
      next: (d) => {
        allData.incidents = d;
        checkDone();
      },
      error: () => checkDone(),
    });
    this.complianceService.getEvidenceBackup().subscribe({
      next: (d) => {
        allData.backup = d;
        checkDone();
      },
      error: () => checkDone(),
    });
    this.complianceService.getEvidenceAssets().subscribe({
      next: (d) => {
        allData.assets = d;
        checkDone();
      },
      error: () => checkDone(),
    });
    this.complianceService.getEvidenceSummary().subscribe({
      next: (d) => {
        allData.summary = d;
        checkDone();
      },
      error: () => checkDone(),
    });
  }

  // ─── Script Runner ───

  toggleAllCategories(checked: boolean) {
    for (const key of Object.keys(this.selectedCategories)) {
      this.selectedCategories[key] = checked;
    }
  }

  get selectedCategoryCount(): number {
    return Object.values(this.selectedCategories).filter((v) => v).length;
  }

  async startCollection() {
    const categories = Object.entries(this.selectedCategories)
      .filter(([, v]) => v)
      .map(([k]) => k);

    if (categories.length === 0) return;

    this.scriptRunning.set(true);
    this.scriptStatus.set('Starting');
    this.logLines.set([]);
    this.progress.set(null);

    this.complianceService
      .startEvidenceCollection({
        categories,
        skipApi: this.skipApi,
      })
      .subscribe({
        next: async (result) => {
          this.operationId.set(result.operationId);
          this.scriptStatus.set(result.status);
          await this.complianceService.connectEvidenceHub(result.operationId);
        },
        error: (err) => {
          this.scriptRunning.set(false);
          this.scriptStatus.set('Failed');
          this.logLines.update((lines) => [
            ...lines,
            {
              operationId: '',
              timestamp: new Date().toISOString(),
              level: 'ERROR',
              message: `Không thể bắt đầu: ${err.error?.message || err.message || 'Unknown error'}`,
            },
          ]);
        },
      });
  }

  cancelCollection() {
    const opId = this.operationId();
    if (!opId) return;

    this.complianceService.cancelEvidenceCollection(opId).subscribe({
      next: () => this.scriptStatus.set('Cancelling'),
      error: () => {},
    });
  }

  clearLog() {
    this.logLines.set([]);
  }

  loadEvidenceFiles() {
    this.filesLoading.set(true);
    this.complianceService.getEvidenceFiles().subscribe({
      next: (data) => {
        this.evidenceFiles.set(data.files);
        this.filesLoading.set(false);
      },
      error: () => this.filesLoading.set(false),
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }

  getLogLevelClass(level: string): string {
    switch (level) {
      case 'OK':
        return 'log-ok';
      case 'WARN':
        return 'log-warn';
      case 'ERROR':
        return 'log-error';
      case 'HEADER':
        return 'log-header';
      default:
        return 'log-info';
    }
  }

  private scrollLogToBottom() {
    if (this.logContainer?.nativeElement) {
      const el = this.logContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }

  getCategoryLabel(key: string): string {
    return this.allScriptCategories.find((c) => c.key === key)?.label ?? key;
  }

  getCategoryIcon(key: string): string {
    return this.allScriptCategories.find((c) => c.key === key)?.icon ?? '📋';
  }

  getGradeClass(grade: string): string {
    if (grade === 'A' || grade === 'A+') return 'grade-a';
    if (grade === 'B' || grade === 'B+') return 'grade-b';
    if (grade === 'C' || grade === 'C+') return 'grade-c';
    return 'grade-d';
  }

  getStatusClass(status: string): string {
    if (status === 'Pass') return 'status-pass';
    if (status === 'Partial') return 'status-partial';
    return 'status-fail';
  }

  getRiskClass(score: number): string {
    if (score >= 70) return 'risk-high';
    if (score >= 40) return 'risk-medium';
    return 'risk-low';
  }

  private dateStr(): string {
    return new Date().toISOString().split('T')[0];
  }

  private downloadCsv(rows: string[][], filename: string) {
    const csvContent = rows
      .map((r) => r.map((c) => `"${(c || '').replace(/"/g, '""')}"`).join(','))
      .join('\n');
    const blob = new Blob(['\ufeff' + csvContent], { type: 'text/csv;charset=utf-8;' });
    this.downloadBlob(blob, filename);
  }

  private downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
