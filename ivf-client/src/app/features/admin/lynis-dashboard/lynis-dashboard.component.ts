import { Component, inject, OnDestroy, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { interval, Subscription } from 'rxjs';
import { switchMap, takeWhile } from 'rxjs/operators';
import { LynisService } from '../../../core/services/lynis.service';
import { LynisReport, LynisReportSummary } from '../../../core/models/lynis.model';

@Component({
  selector: 'app-lynis-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lynis-dashboard.component.html',
  styleUrls: ['./lynis-dashboard.component.scss'],
})
export class LynisDashboardComponent implements OnInit, OnDestroy {
  private lynisService = inject(LynisService);
  private scanPollSub: Subscription | null = null;

  activeTab = signal<'overview' | 'reports' | 'detail'>('overview');

  hosts = signal<string[]>([]);
  selectedHost = signal<string>('');
  reports = signal<LynisReportSummary[]>([]);
  selectedReport = signal<LynisReport | null>(null);

  loading = signal(false);
  loadingReport = signal(false);
  error = signal('');
  scanning = signal(false);
  scanError = signal('');

  // computed: score màu (SCSS class modifiers)
  scoreColor = computed(() => {
    const idx = this.selectedReport()?.hardening_index ?? 0;
    if (idx >= 80) return 'score-good';
    if (idx >= 60) return 'score-medium';
    if (idx >= 40) return 'score-warning';
    return 'score-danger';
  });

  scoreBg = computed(() => {
    const idx = this.selectedReport()?.hardening_index ?? 0;
    if (idx >= 80) return 'stat-green';
    if (idx >= 60) return 'stat-yellow';
    if (idx >= 40) return 'stat-orange';
    return 'stat-red';
  });

  ngOnInit(): void {
    this.loadHosts();
  }

  ngOnDestroy(): void {
    this.scanPollSub?.unsubscribe();
  }

  loadHosts(): void {
    this.loading.set(true);
    this.error.set('');
    this.lynisService.getHosts().subscribe({
      next: (res) => {
        this.hosts.set(res.hosts);
        if (res.hosts.length > 0) {
          this.selectedHost.set(res.hosts[0]);
          this.loadLatest(res.hosts[0]);
          this.loadReports(res.hosts[0]);
          this.checkScanStatus(res.hosts[0]);
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Không thể tải danh sách hosts. Kiểm tra kết nối MinIO.');
        this.loading.set(false);
      },
    });
  }

  loadLatest(host: string): void {
    this.loadingReport.set(true);
    this.lynisService.getLatestReport(host).subscribe({
      next: (r) => {
        this.selectedReport.set(r);
        this.loadingReport.set(false);
      },
      error: () => {
        this.selectedReport.set(null);
        this.loadingReport.set(false);
      },
    });
  }

  loadReports(host: string): void {
    this.lynisService.getReports(host).subscribe({
      next: (res) => this.reports.set(res.reports),
      error: () => this.reports.set([]),
    });
  }

  onHostChange(host: string): void {
    this.selectedHost.set(host);
    this.scanPollSub?.unsubscribe();
    this.scanning.set(false);
    this.scanError.set('');
    this.loadLatest(host);
    this.loadReports(host);
    this.checkScanStatus(host);
  }

  openReport(report: LynisReportSummary): void {
    this.loadingReport.set(true);
    this.activeTab.set('detail');
    this.lynisService.getReport(report.hostname, report.date).subscribe({
      next: (r) => {
        this.selectedReport.set(r);
        this.loadingReport.set(false);
      },
      error: () => this.loadingReport.set(false),
    });
  }

  setTab(tab: 'overview' | 'reports' | 'detail'): void {
    this.activeTab.set(tab);
  }

  triggerScan(): void {
    const host = this.selectedHost();
    if (!host || this.scanning()) return;

    this.scanning.set(true);
    this.scanError.set('');

    this.lynisService.triggerScan(host).subscribe({
      next: () => this.startScanPolling(host),
      error: () => {
        this.scanning.set(false);
        this.scanError.set('Không thể gửi yêu cầu quét. Thử lại sau.');
      },
    });
  }

  private checkScanStatus(host: string): void {
    this.lynisService.getScanStatus(host).subscribe({
      next: (s) => {
        if (s.status === 'scanning') {
          this.scanning.set(true);
          this.startScanPolling(host);
        }
      },
      error: () => {},
    });
  }

  private startScanPolling(host: string): void {
    this.scanPollSub?.unsubscribe();
    // Poll every 10 seconds; stop when status becomes idle
    this.scanPollSub = interval(10_000)
      .pipe(
        switchMap(() => this.lynisService.getScanStatus(host)),
        takeWhile((s) => s.status === 'scanning', true),
      )
      .subscribe({
        next: (s) => {
          if (s.status === 'idle') {
            this.scanning.set(false);
            this.loadLatest(host);
            this.loadReports(host);
          }
        },
        error: () => {
          this.scanning.set(false);
          this.scanError.set('Lỗi kiểm tra trạng thái quét.');
        },
      });
  }

  getScoreLabel(idx: number): string {
    if (idx >= 80) return 'Tốt';
    if (idx >= 60) return 'Trung bình';
    if (idx >= 40) return 'Cần cải thiện';
    return 'Nguy hiểm';
  }

  trackByDate(_index: number, r: LynisReportSummary): string {
    return r.date;
  }
}

