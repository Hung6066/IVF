import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReportsService, KPIData } from '../reports.service';

@Component({
  selector: 'app-financial-report',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './financial-report.component.html',
  styleUrls: ['./financial-report.component.scss'],
})
export class FinancialReportComponent implements OnInit {
  private reportsService = inject(ReportsService);

  loading = signal(true);
  kpis = signal<KPIData | null>(null);

  ngOnInit() {
    this.reportsService.getKPIs().subscribe({
      next: (k) => {
        this.kpis.set(k);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
