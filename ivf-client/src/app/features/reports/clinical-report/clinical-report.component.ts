import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReportsService, MonthlyResult, DoctorPerformance } from '../reports.service';

@Component({
  selector: 'app-clinical-report',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './clinical-report.component.html',
  styleUrls: ['./clinical-report.component.scss'],
})
export class ClinicalReportComponent implements OnInit {
  private reportsService = inject(ReportsService);

  loading = signal(true);
  monthlyResults = signal<MonthlyResult[]>([]);
  topDoctors = signal<DoctorPerformance[]>([]);
  successRate = signal(0);
  totalCycles = signal(0);

  ngOnInit() {
    this.reportsService.getKPIs().subscribe({
      next: (kpi) => {
        this.successRate.set(kpi.successRate);
        this.totalCycles.set(kpi.totalCycles);
      },
    });
    this.reportsService.getMonthlyResults().subscribe({ next: (r) => this.monthlyResults.set(r) });
    this.reportsService.getTopDoctors().subscribe({
      next: (d) => {
        this.topDoctors.set(d);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  getBarWidth(value: number, max: number): string {
    return Math.round((value / max) * 100) + '%';
  }

  get maxMonthlyTotal(): number {
    const vals = this.monthlyResults().map((r) => r.success + r.failed);
    return Math.max(...vals, 1);
  }
}
