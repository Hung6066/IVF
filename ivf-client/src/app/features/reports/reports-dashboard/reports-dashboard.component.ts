import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ReportsService, KPIData, MonthlyResult, DoctorPerformance, CryoStats } from '../reports.service';

@Component({
  selector: 'app-reports-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './reports-dashboard.component.html',
  styleUrls: ['./reports-dashboard.component.scss']
})
export class ReportsDashboardComponent implements OnInit {
  private service = inject(ReportsService);

  startDate = '';
  endDate = '';

  kpis = signal<KPIData | null>(null);
  monthlyData = signal<MonthlyResult[]>([]);
  topDoctors = signal<DoctorPerformance[]>([]);
  cryo = signal<CryoStats | null>(null);

  ngOnInit(): void {
    const today = new Date();
    this.endDate = today.toISOString().split('T')[0];
    this.startDate = new Date(today.setMonth(today.getMonth() - 1)).toISOString().split('T')[0];
    this.loadReports();
  }

  loadReports(): void {
    this.service.getKPIs().subscribe(data => this.kpis.set(data));
    this.service.getMonthlyResults().subscribe(data => this.monthlyData.set(data));
    this.service.getTopDoctors().subscribe(data => this.topDoctors.set(data));
    this.service.getCryoStats().subscribe(data => this.cryo.set(data));
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value);
  }
}
