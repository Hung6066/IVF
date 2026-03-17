import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LynisService } from '../../../core/services/lynis.service';
import { LynisReport, LynisReportSummary } from '../../../core/models/lynis.model';

@Component({
  selector: 'app-lynis-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lynis-dashboard.component.html',
  styleUrls: ['./lynis-dashboard.component.scss'],
})
export class LynisDashboardComponent implements OnInit {
  private lynisService = inject(LynisService);

  activeTab = signal<'overview' | 'reports' | 'detail'>('overview');

  hosts = signal<string[]>([]);
  selectedHost = signal<string>('');
  reports = signal<LynisReportSummary[]>([]);
  selectedReport = signal<LynisReport | null>(null);

  loading = signal(false);
  loadingReport = signal(false);
  error = signal('');

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
    this.loadLatest(host);
    this.loadReports(host);
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

  getScoreLabel(idx: number): string {
    if (idx >= 80) return 'Tốt';
    if (idx >= 60) return 'Trung bình';
    if (idx >= 40) return 'Cần cải thiện';
    return 'Nguy hiểm';
  }

  trackByDate(_: number, r: LynisReportSummary): string {
    return r.key;
  }
}
