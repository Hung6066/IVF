import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-reports-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="reports-dashboard">
      <header class="page-header">
        <h1>📊 Báo cáo & Thống kê</h1>
        <div class="date-range">
          <input type="date" [(ngModel)]="startDate" />
          <span>→</span>
          <input type="date" [(ngModel)]="endDate" />
          <button class="btn-filter" (click)="loadReports()">Lọc</button>
        </div>
      </header>

      <div class="kpi-grid">
        <div class="kpi-card success">
          <div class="kpi-value">{{ successRate() }}%</div>
          <div class="kpi-label">Tỷ lệ thành công IVF</div>
          <div class="kpi-trend up">↑ 3.2% so với tháng trước</div>
        </div>
        <div class="kpi-card cycles">
          <div class="kpi-value">{{ totalCycles() }}</div>
          <div class="kpi-label">Tổng chu kỳ điều trị</div>
          <div class="kpi-sub">IVF: {{ ivfCycles() }} | IUI: {{ iuiCycles() }}</div>
        </div>
        <div class="kpi-card revenue">
          <div class="kpi-value">{{ formatCurrency(totalRevenue()) }}</div>
          <div class="kpi-label">Doanh thu</div>
          <div class="kpi-trend up">↑ 12% so với tháng trước</div>
        </div>
        <div class="kpi-card patients">
          <div class="kpi-value">{{ newPatients() }}</div>
          <div class="kpi-label">Bệnh nhân mới</div>
          <div class="kpi-sub">Tổng: {{ totalPatients() }}</div>
        </div>
      </div>

      <div class="charts-grid">
        <section class="chart-card">
          <h2>Phân bố phương pháp điều trị</h2>
          <div class="pie-chart-mock">
            <div class="pie-segment icsi" style="--percent: 45">ICSI 45%</div>
            <div class="pie-segment iui" style="--percent: 30">IUI 30%</div>
            <div class="pie-segment ivm" style="--percent: 15">IVM 15%</div>
            <div class="pie-segment qhtn" style="--percent: 10">QHTN 10%</div>
          </div>
        </section>

        <section class="chart-card">
          <h2>Kết quả chu kỳ theo tháng</h2>
          <div class="bar-chart">
            @for (month of monthlyData(); track month.name) {
              <div class="bar-group">
                <div class="bar success" [style.height.%]="month.success * 2"></div>
                <div class="bar failed" [style.height.%]="month.failed * 2"></div>
                <div class="bar-label">{{ month.name }}</div>
              </div>
            }
          </div>
          <div class="chart-legend">
            <span class="legend-item success">● Thành công</span>
            <span class="legend-item failed">● Thất bại</span>
          </div>
        </section>
      </div>

      <div class="tables-grid">
        <section class="table-card">
          <h2>Top 10 bác sĩ theo chu kỳ</h2>
          <table>
            <thead><tr><th>Bác sĩ</th><th>Chu kỳ</th><th>Thành công</th></tr></thead>
            <tbody>
              @for (doc of topDoctors(); track doc.name) {
                <tr>
                  <td>{{ doc.name }}</td>
                  <td>{{ doc.cycles }}</td>
                  <td><span class="pct">{{ doc.successRate }}%</span></td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <section class="table-card">
          <h2>Tình trạng kho đông lạnh</h2>
          <div class="cryo-stats">
            <div class="cryo-item">
              <span class="cryo-icon">🧫</span>
              <div class="cryo-info">
                <span class="cryo-value">{{ frozenEmbryos() }}</span>
                <span class="cryo-label">Phôi đông lạnh</span>
              </div>
            </div>
            <div class="cryo-item">
              <span class="cryo-icon">🥚</span>
              <div class="cryo-info">
                <span class="cryo-value">{{ frozenEggs() }}</span>
                <span class="cryo-label">Trứng đông lạnh</span>
              </div>
            </div>
            <div class="cryo-item">
              <span class="cryo-icon">🧪</span>
              <div class="cryo-info">
                <span class="cryo-value">{{ frozenSperm() }}</span>
                <span class="cryo-label">Tinh trùng đông lạnh</span>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `,
    styles: [`
    .reports-dashboard { max-width: 1400px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; flex-wrap: wrap; gap: 1rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }
    .date-range { display: flex; align-items: center; gap: 0.5rem; }
    .date-range input { padding: 0.5rem; border: 1px solid #e2e8f0; border-radius: 6px; }
    .date-range span { color: #6b7280; }
    .btn-filter { padding: 0.5rem 1rem; background: #667eea; color: white; border: none; border-radius: 6px; cursor: pointer; }

    .kpi-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; margin-bottom: 2rem; }
    .kpi-card { background: white; border-radius: 16px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); border-top: 4px solid #667eea; }
    .kpi-card.success { border-top-color: #10b981; }
    .kpi-card.cycles { border-top-color: #667eea; }
    .kpi-card.revenue { border-top-color: #f59e0b; }
    .kpi-card.patients { border-top-color: #ec4899; }
    .kpi-value { font-size: 2rem; font-weight: 700; color: #1e1e2f; }
    .kpi-label { color: #6b7280; font-size: 0.875rem; margin-top: 0.25rem; }
    .kpi-trend { font-size: 0.75rem; margin-top: 0.5rem; }
    .kpi-trend.up { color: #10b981; }
    .kpi-trend.down { color: #ef4444; }
    .kpi-sub { font-size: 0.75rem; color: #9ca3af; margin-top: 0.5rem; }

    .charts-grid { display: grid; grid-template-columns: 1fr 2fr; gap: 1.5rem; margin-bottom: 1.5rem; }
    .chart-card { background: white; border-radius: 16px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
    h2 { font-size: 1rem; color: #374151; margin: 0 0 1rem; }

    .pie-chart-mock { display: flex; flex-wrap: wrap; gap: 0.5rem; justify-content: center; }
    .pie-segment { padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.75rem; font-weight: 500; }
    .pie-segment.icsi { background: #dbeafe; color: #1d4ed8; }
    .pie-segment.iui { background: #d1fae5; color: #065f46; }
    .pie-segment.ivm { background: #fef3c7; color: #92400e; }
    .pie-segment.qhtn { background: #fce7f3; color: #9d174d; }

    .bar-chart { display: flex; align-items: flex-end; gap: 1rem; height: 150px; padding: 1rem 0; }
    .bar-group { display: flex; flex-direction: column; align-items: center; gap: 0.25rem; flex: 1; }
    .bar { width: 20px; border-radius: 4px 4px 0 0; }
    .bar.success { background: #10b981; }
    .bar.failed { background: #ef4444; }
    .bar-label { font-size: 0.625rem; color: #6b7280; }
    .chart-legend { display: flex; gap: 1rem; justify-content: center; margin-top: 0.5rem; }
    .legend-item { font-size: 0.75rem; }
    .legend-item.success { color: #10b981; }
    .legend-item.failed { color: #ef4444; }

    .tables-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }
    .table-card { background: white; border-radius: 16px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.5rem; text-align: left; border-bottom: 1px solid #f1f5f9; font-size: 0.875rem; }
    th { background: #f8fafc; color: #6b7280; font-weight: 500; }
    .pct { color: #10b981; font-weight: 600; }

    .cryo-stats { display: flex; flex-direction: column; gap: 1rem; }
    .cryo-item { display: flex; align-items: center; gap: 1rem; padding: 1rem; background: #f8fafc; border-radius: 12px; }
    .cryo-icon { font-size: 2rem; }
    .cryo-value { display: block; font-size: 1.5rem; font-weight: 700; color: #1e1e2f; }
    .cryo-label { color: #6b7280; font-size: 0.875rem; }
  `]
})
export class ReportsDashboardComponent implements OnInit {
    startDate = '';
    endDate = '';
    successRate = signal(42);
    totalCycles = signal(156);
    ivfCycles = signal(98);
    iuiCycles = signal(58);
    totalRevenue = signal(2450000000);
    newPatients = signal(45);
    totalPatients = signal(1250);
    frozenEmbryos = signal(342);
    frozenEggs = signal(128);
    frozenSperm = signal(256);
    monthlyData = signal<any[]>([]);
    topDoctors = signal<any[]>([]);

    constructor(private api: ApiService) { }

    ngOnInit(): void {
        const today = new Date();
        this.endDate = today.toISOString().split('T')[0];
        this.startDate = new Date(today.setMonth(today.getMonth() - 1)).toISOString().split('T')[0];
        this.loadReports();
    }

    loadReports(): void {
        this.monthlyData.set([
            { name: 'T1', success: 35, failed: 15 },
            { name: 'T2', success: 40, failed: 12 },
            { name: 'T3', success: 38, failed: 18 },
            { name: 'T4', success: 45, failed: 10 },
            { name: 'T5', success: 42, failed: 14 },
            { name: 'T6', success: 48, failed: 8 }
        ]);
        this.topDoctors.set([
            { name: 'BS. Nguyễn Văn A', cycles: 45, successRate: 48 },
            { name: 'BS. Trần Thị B', cycles: 38, successRate: 45 },
            { name: 'BS. Lê Văn C', cycles: 32, successRate: 42 }
        ]);
    }

    formatCurrency(value: number): string {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value);
    }
}
