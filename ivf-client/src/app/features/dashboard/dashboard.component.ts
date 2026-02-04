import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { DashboardStats, CycleSuccessRates } from '../../core/models/api.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dashboard">
      <header class="page-header">
        <h1>Dashboard</h1>
        <p>Tổng quan hoạt động hệ thống IVF</p>
      </header>

      <section class="stats-grid">
        <div class="stat-card patients">
          <div class="stat-icon">👥</div>
          <div class="stat-content">
            <span class="stat-value">{{ stats()?.totalPatients || 0 }}</span>
            <span class="stat-label">Tổng bệnh nhân</span>
          </div>
        </div>

        <div class="stat-card cycles">
          <div class="stat-icon">🔄</div>
          <div class="stat-content">
            <span class="stat-value">{{ stats()?.activeCycles || 0 }}</span>
            <span class="stat-label">Chu kỳ đang điều trị</span>
          </div>
        </div>

        <div class="stat-card queue">
          <div class="stat-icon">🎫</div>
          <div class="stat-content">
            <span class="stat-value">{{ stats()?.todayQueueCount || 0 }}</span>
            <span class="stat-label">Số khám hôm nay</span>
          </div>
        </div>

        <div class="stat-card revenue">
          <div class="stat-icon">💰</div>
          <div class="stat-content">
            <span class="stat-value">{{ formatCurrency(stats()?.monthlyRevenue || 0) }}</span>
            <span class="stat-label">Doanh thu tháng</span>
          </div>
        </div>
      </section>

      <section class="charts-grid">
        <div class="chart-card">
          <h3>Tỷ lệ thành công {{ successRates()?.year }}</h3>
          <div class="success-chart">
            <div class="success-rate">
              <div class="rate-circle" [style.--rate]="(successRates()?.successRate || 0) + '%'">
                <span class="rate-value">{{ successRates()?.successRate || 0 }}%</span>
              </div>
              <span class="rate-label">Tỷ lệ có thai</span>
            </div>
            <div class="success-breakdown">
              <div class="breakdown-item">
                <span class="dot pregnant"></span>
                <span>Có thai: {{ successRates()?.pregnancies || 0 }}</span>
              </div>
              <div class="breakdown-item">
                <span class="dot not-pregnant"></span>
                <span>Không thai: {{ successRates()?.notPregnant || 0 }}</span>
              </div>
              <div class="breakdown-item">
                <span class="dot cancelled"></span>
                <span>Huỷ: {{ successRates()?.cancelled || 0 }}</span>
              </div>
              <div class="breakdown-item">
                <span class="dot frozen"></span>
                <span>Trữ phôi: {{ successRates()?.frozenAll || 0 }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="chart-card">
          <h3>Hướng dẫn nhanh</h3>
          <div class="quick-actions">
            <button class="action-btn" (click)="goToAddPatient()">➕ Thêm bệnh nhân mới</button>
            <button class="action-btn" (click)="goToIssueQueue()">🎫 Phát số thứ tự</button>
            <button class="action-btn" (click)="goToAppointments()">📋 Xem lịch hẹn</button>
            <button class="action-btn" (click)="goToReports()">📊 Báo cáo chi tiết</button>
          </div>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .dashboard { max-width: 1400px; }

    .page-header {
      margin-bottom: 2rem;
    }

    .page-header h1 {
      font-size: 1.75rem;
      color: #1e1e2f;
      margin: 0 0 0.25rem;
    }

    .page-header p {
      color: #6b7280;
      margin: 0;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1.5rem;
      margin-bottom: 2rem;
    }

    .stat-card {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      display: flex;
      align-items: center;
      gap: 1rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .stat-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 10px 25px -5px rgba(0,0,0,0.1);
    }

    .stat-icon {
      width: 56px;
      height: 56px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
    }

    .stat-card.patients .stat-icon { background: linear-gradient(135deg, #667eea, #764ba2); }
    .stat-card.cycles .stat-icon { background: linear-gradient(135deg, #06b6d4, #0891b2); }
    .stat-card.queue .stat-icon { background: linear-gradient(135deg, #10b981, #059669); }
    .stat-card.revenue .stat-icon { background: linear-gradient(135deg, #f59e0b, #d97706); }

    .stat-content {
      display: flex;
      flex-direction: column;
    }

    .stat-value {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1e1e2f;
    }

    .stat-label {
      font-size: 0.875rem;
      color: #6b7280;
    }

    .charts-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
      gap: 1.5rem;
    }

    .chart-card {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .chart-card h3 {
      font-size: 1rem;
      color: #1e1e2f;
      margin: 0 0 1.25rem;
    }

    .success-chart {
      display: flex;
      align-items: center;
      gap: 2rem;
    }

    .success-rate {
      text-align: center;
    }

    .rate-circle {
      width: 120px;
      height: 120px;
      border-radius: 50%;
      background: conic-gradient(#10b981 var(--rate, 0%), #e5e7eb 0%);
      display: flex;
      align-items: center;
      justify-content: center;
      position: relative;
    }

    .rate-circle::before {
      content: '';
      width: 90px;
      height: 90px;
      background: white;
      border-radius: 50%;
      position: absolute;
    }

    .rate-value {
      position: relative;
      z-index: 1;
      font-size: 1.5rem;
      font-weight: 700;
      color: #10b981;
    }

    .rate-label {
      display: block;
      margin-top: 0.5rem;
      font-size: 0.875rem;
      color: #6b7280;
    }

    .success-breakdown {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .breakdown-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
      color: #374151;
    }

    .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
    }

    .dot.pregnant { background: #10b981; }
    .dot.not-pregnant { background: #ef4444; }
    .dot.cancelled { background: #6b7280; }
    .dot.frozen { background: #06b6d4; }

    .quick-actions {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .action-btn {
      padding: 0.875rem 1rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      text-align: left;
      font-size: 0.9375rem;
      cursor: pointer;
      transition: all 0.2s;
    }

    .action-btn:hover {
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border-color: transparent;
    }
  `]
})
export class DashboardComponent implements OnInit {
  stats = signal<DashboardStats | null>(null);
  successRates = signal<CycleSuccessRates | null>(null);

  constructor(private api: ApiService, private router: Router) { }

  ngOnInit(): void {
    this.api.getDashboardStats().subscribe(data => this.stats.set(data));
    this.api.getCycleSuccessRates().subscribe(data => this.successRates.set(data));
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
  }

  goToAddPatient(): void { this.router.navigate(['/patients'], { queryParams: { action: 'new' } }); }
  goToIssueQueue(): void { this.router.navigate(['/reception'], { queryParams: { action: 'queue' } }); }
  goToAppointments(): void { this.router.navigate(['/queue/US']); }
  goToReports(): void { this.router.navigate(['/reports']); }
}
