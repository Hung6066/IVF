import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ComplianceService } from '../../../core/services/compliance.service';
import {
  AuditDashboard,
  AuditScan,
  AuditFramework,
  AuditControl,
  AuditAlert,
} from '../../../core/models/compliance.model';

@Component({
  selector: 'app-compliance-audit',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './compliance-audit.component.html',
  styleUrl: './compliance-audit.component.scss',
})
export class ComplianceAuditComponent implements OnInit {
  private complianceService = inject(ComplianceService);

  dashboard = signal<AuditDashboard | null>(null);
  currentScan = signal<AuditScan | null>(null);
  scanHistory = signal<AuditScan[]>([]);
  loading = signal(true);
  scanning = signal(false);
  error = signal<string | null>(null);

  activeTab = signal<'overview' | 'frameworks' | 'controls' | 'alerts' | 'history'>('overview');
  selectedFramework = signal<AuditFramework | null>(null);
  controlFilter = signal<'all' | 'passed' | 'failed' | 'warning'>('all');

  // Computed
  filteredControls = computed(() => {
    const fw = this.selectedFramework();
    if (!fw) return [];
    const filter = this.controlFilter();
    if (filter === 'all') return fw.controls;
    const statusMap: Record<string, string> = {
      passed: 'Passed',
      failed: 'Failed',
      warning: 'Warning',
    };
    return fw.controls.filter((c) => c.status === statusMap[filter]);
  });

  criticalAlerts = computed(() => {
    const d = this.dashboard();
    return d?.alerts.filter((a) => a.severity === 'Critical') ?? [];
  });

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.loading.set(true);
    this.error.set(null);
    this.complianceService.getAuditDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Không thể tải dữ liệu audit');
        this.loading.set(false);
      },
    });
  }

  runScan(): void {
    this.scanning.set(true);
    this.complianceService.runAuditScan().subscribe({
      next: (scan) => {
        this.currentScan.set(scan);
        this.scanning.set(false);
        this.loadDashboard();
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Scan thất bại');
        this.scanning.set(false);
      },
    });
  }

  loadHistory(): void {
    this.complianceService.getAuditHistory().subscribe({
      next: (data) => this.scanHistory.set(data.scans),
      error: () => {},
    });
  }

  setTab(tab: 'overview' | 'frameworks' | 'controls' | 'alerts' | 'history'): void {
    this.activeTab.set(tab);
    if (tab === 'history' && this.scanHistory().length === 0) {
      this.loadHistory();
    }
  }

  selectFramework(fw: AuditFramework | null): void {
    this.selectedFramework.set(fw);
    if (fw) {
      this.activeTab.set('controls');
    }
  }

  getScoreColor(score: number): string {
    if (score >= 90) return '#10b981';
    if (score >= 75) return '#f59e0b';
    if (score >= 60) return '#f97316';
    return '#ef4444';
  }

  getGradeClass(grade: string): string {
    if (grade.startsWith('A')) return 'grade-a';
    if (grade.startsWith('B')) return 'grade-b';
    if (grade.startsWith('C')) return 'grade-c';
    return 'grade-f';
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'Passed':
        return '✅';
      case 'Failed':
        return '❌';
      case 'Warning':
        return '⚠️';
      default:
        return '⏳';
    }
  }

  getSeverityClass(severity: string): string {
    return `severity-${severity.toLowerCase()}`;
  }

  getHealthIcon(online: boolean): string {
    return online ? '🟢' : '🔴';
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleString('vi-VN');
  }

  trackByFramework(_: number, fw: { id: string }): string {
    return fw.id;
  }

  trackByControl(_: number, ctl: AuditControl): string {
    return ctl.id;
  }

  trackByAlert(index: number): number {
    return index;
  }

  trackByScan(_: number, scan: AuditScan): string {
    return scan.scanId;
  }

  findFramework(id: string): AuditFramework | null {
    const scan = this.dashboard()?.lastScan;
    return scan?.frameworks.find((f) => f.id === id) ?? null;
  }
}
