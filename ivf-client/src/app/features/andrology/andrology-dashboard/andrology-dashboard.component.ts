import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

interface SemenAnalysis {
    id: string;
    patientName: string;
    patientCode: string;
    analysisDate: string;
    volume?: number;
    concentration?: number;
    progressiveMotility?: number;
    nonProgressiveMotility?: number;
    immotile?: number;
    morphology?: number;
    vitality?: number;
    status: string;
}

interface SpermWashing {
    id: string;
    cycleCode: string;
    patientName: string;
    method: string;
    prewashConc?: number;
    postwashConc?: number;
    postwashMotility?: number;
    washDate: string;
    status: string;
}

@Component({
    selector: 'app-andrology-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="andrology-dashboard">
      <header class="page-header">
        <h1>🔬 Nam Khoa - Andrology</h1>
        <div class="tabs">
          <button class="tab" [class.active]="activeTab === 'analysis'" (click)="activeTab = 'analysis'">
            📊 Tinh dịch đồ
          </button>
          <button class="tab" [class.active]="activeTab === 'washing'" (click)="activeTab = 'washing'">
            🧪 Lọc rửa
          </button>
          <button class="tab" [class.active]="activeTab === 'statistics'" (click)="activeTab = 'statistics'">
            📈 Thống kê
          </button>
        </div>
      </header>

      <div class="stats-row">
        <div class="stat-card">
          <span class="value">{{ todayAnalysis() }}</span>
          <span class="label">XN hôm nay</span>
        </div>
        <div class="stat-card">
          <span class="value">{{ todayWashing() }}</span>
          <span class="label">Lọc rửa hôm nay</span>
        </div>
        <div class="stat-card">
          <span class="value">{{ pendingCount() }}</span>
          <span class="label">Chờ kết quả</span>
        </div>
        <div class="stat-card">
          <span class="value">{{ avgConcentration() }}M</span>
          <span class="label">Mật độ TB</span>
        </div>
      </div>

      @if (activeTab === 'analysis') {
        <section class="content-section">
          <div class="section-header">
            <h2>Danh sách xét nghiệm tinh dịch đồ</h2>
            <button class="btn-new" (click)="showNewAnalysis = true">➕ Xét nghiệm mới</button>
          </div>
          <div class="filter-row">
            <input type="date" [(ngModel)]="filterDate" />
            <select [(ngModel)]="filterStatus">
              <option value="">Tất cả trạng thái</option>
              <option value="Pending">Chờ XN</option>
              <option value="Processing">Đang XN</option>
              <option value="Completed">Hoàn thành</option>
            </select>
            <input type="text" [(ngModel)]="searchTerm" placeholder="Tìm theo mã BN..." />
          </div>
          <table>
            <thead>
              <tr>
                <th>Mã BN</th>
                <th>Họ tên</th>
                <th>Ngày XN</th>
                <th>Thể tích</th>
                <th>Mật độ</th>
                <th>Di động PR</th>
                <th>Di động NP</th>
                <th>Bất động</th>
                <th>Hình thái</th>
                <th>Sống</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              @for (item of filteredAnalyses(); track item.id) {
                <tr>
                  <td class="code">{{ item.patientCode }}</td>
                  <td>{{ item.patientName }}</td>
                  <td>{{ formatDate(item.analysisDate) }}</td>
                  <td>{{ item.volume ?? '—' }} ml</td>
                  <td [class.low]="(item.concentration ?? 0) < 15">{{ item.concentration ?? '—' }} M/ml</td>
                  <td [class.low]="(item.progressiveMotility ?? 0) < 32">{{ item.progressiveMotility ?? '—' }}%</td>
                  <td>{{ item.nonProgressiveMotility ?? '—' }}%</td>
                  <td>{{ item.immotile ?? '—' }}%</td>
                  <td [class.low]="(item.morphology ?? 0) < 4">{{ item.morphology ?? '—' }}%</td>
                  <td>{{ item.vitality ?? '—' }}%</td>
                  <td><span class="badge" [class]="item.status.toLowerCase()">{{ getStatusName(item.status) }}</span></td>
                  <td class="actions">
                    <button class="btn-icon" title="Sửa" (click)="editAnalysis(item)">✏️</button>
                    <button class="btn-icon" title="In" (click)="printResult(item)">🖨️</button>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="12" class="empty">Không có dữ liệu</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      @if (activeTab === 'washing') {
        <section class="content-section">
          <div class="section-header">
            <h2>Danh sách lọc rửa tinh trùng</h2>
            <button class="btn-new" (click)="showNewWashing = true">➕ Lọc rửa mới</button>
          </div>
          <table>
            <thead>
              <tr>
                <th>Mã chu kỳ</th>
                <th>Họ tên</th>
                <th>Phương pháp</th>
                <th>Mật độ trước</th>
                <th>Mật độ sau</th>
                <th>Di động sau</th>
                <th>Ngày lọc</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              @for (item of washings(); track item.id) {
                <tr>
                  <td class="code">{{ item.cycleCode }}</td>
                  <td>{{ item.patientName }}</td>
                  <td>{{ item.method }}</td>
                  <td>{{ item.prewashConc ?? '—' }} M/ml</td>
                  <td>{{ item.postwashConc ?? '—' }} M/ml</td>
                  <td>{{ item.postwashMotility ?? '—' }}%</td>
                  <td>{{ formatDate(item.washDate) }}</td>
                  <td><span class="badge" [class]="item.status.toLowerCase()">{{ getStatusName(item.status) }}</span></td>
                  <td class="actions">
                    <button class="btn-icon" title="Sửa">✏️</button>
                    <button class="btn-icon" title="In">🖨️</button>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="9" class="empty">Không có dữ liệu</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      @if (activeTab === 'statistics') {
        <section class="content-section">
          <h2>Thống kê Nam khoa</h2>
          <div class="stats-grid">
            <div class="stat-box">
              <h4>Phân loại mật độ tinh trùng</h4>
              <div class="bar-chart">
                <div class="bar-item"><span class="bar-label">Normozoospermia</span><div class="bar" style="width: 45%"></div><span>45%</span></div>
                <div class="bar-item"><span class="bar-label">Oligozoospermia</span><div class="bar warning" style="width: 25%"></div><span>25%</span></div>
                <div class="bar-item"><span class="bar-label">Severe Oligo</span><div class="bar danger" style="width: 15%"></div><span>15%</span></div>
                <div class="bar-item"><span class="bar-label">Azoospermia</span><div class="bar danger" style="width: 15%"></div><span>15%</span></div>
              </div>
            </div>
            <div class="stat-box">
              <h4>Hiệu quả lọc rửa</h4>
              <div class="efficiency-stats">
                <div class="eff-item">
                  <span class="eff-label">Tỷ lệ cải thiện mật độ</span>
                  <span class="eff-value">+85%</span>
                </div>
                <div class="eff-item">
                  <span class="eff-label">Tỷ lệ cải thiện di động</span>
                  <span class="eff-value">+65%</span>
                </div>
                <div class="eff-item">
                  <span class="eff-label">Recovery rate TB</span>
                  <span class="eff-value">42%</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      }

      @if (showNewAnalysis) {
        <div class="modal-overlay" (click)="showNewAnalysis = false">
          <div class="modal" (click)="$event.stopPropagation()">
            <h3>Xét nghiệm tinh dịch đồ mới</h3>
            <form (ngSubmit)="submitAnalysis()">
              <div class="form-group">
                <label>Tìm bệnh nhân</label>
                <input type="text" [(ngModel)]="newAnalysis.patientSearch" name="search" placeholder="Mã BN hoặc tên..." />
              </div>
              <div class="form-section">
                <h4>Đại thể (Macroscopic)</h4>
                <div class="form-row">
                  <div class="form-group"><label>Thể tích (ml)</label><input type="number" [(ngModel)]="newAnalysis.volume" name="volume" step="0.1" /></div>
                  <div class="form-group"><label>Màu sắc</label><input type="text" [(ngModel)]="newAnalysis.appearance" name="appearance" /></div>
                  <div class="form-group"><label>Ly giải</label><input type="text" [(ngModel)]="newAnalysis.liquefaction" name="liquefaction" /></div>
                  <div class="form-group"><label>pH</label><input type="number" [(ngModel)]="newAnalysis.ph" name="ph" step="0.1" /></div>
                </div>
              </div>
              <div class="form-section">
                <h4>Vi thể (Microscopic)</h4>
                <div class="form-row">
                  <div class="form-group"><label>Mật độ (M/ml)</label><input type="number" [(ngModel)]="newAnalysis.concentration" name="concentration" step="0.1" /></div>
                  <div class="form-group"><label>Tổng số (M)</label><input type="number" [(ngModel)]="newAnalysis.totalCount" name="totalCount" step="0.1" /></div>
                </div>
                <div class="form-row">
                  <div class="form-group"><label>Di động PR (%)</label><input type="number" [(ngModel)]="newAnalysis.progressiveMotility" name="pr" max="100" /></div>
                  <div class="form-group"><label>Di động NP (%)</label><input type="number" [(ngModel)]="newAnalysis.nonProgressiveMotility" name="np" max="100" /></div>
                  <div class="form-group"><label>Bất động (%)</label><input type="number" [(ngModel)]="newAnalysis.immotile" name="im" max="100" /></div>
                </div>
                <div class="form-row">
                  <div class="form-group"><label>Hình thái BT (%)</label><input type="number" [(ngModel)]="newAnalysis.morphology" name="morph" max="100" /></div>
                  <div class="form-group"><label>Sống (%)</label><input type="number" [(ngModel)]="newAnalysis.vitality" name="vital" max="100" /></div>
                </div>
              </div>
              <div class="modal-actions">
                <button type="button" class="btn-cancel" (click)="showNewAnalysis = false">Huỷ</button>
                <button type="submit" class="btn-submit">Lưu kết quả</button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
    styles: [`
    .andrology-dashboard { max-width: 1400px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .tabs { display: flex; gap: 0.5rem; }
    .tab { padding: 0.5rem 1rem; background: #f1f5f9; border: none; border-radius: 8px; cursor: pointer; font-size: 0.875rem; }
    .tab.active { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }

    .stats-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; margin-bottom: 1.5rem; }
    .stat-card { background: white; border-radius: 12px; padding: 1.25rem; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }
    .stat-card .value { display: block; font-size: 1.75rem; font-weight: 700; color: #667eea; }
    .stat-card .label { color: #6b7280; font-size: 0.8rem; }

    .content-section { background: white; border-radius: 16px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    h2 { font-size: 1rem; color: #374151; margin: 0; }
    .btn-new { padding: 0.5rem 1rem; background: linear-gradient(135deg, #667eea, #764ba2); color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 0.8rem; }

    .filter-row { display: flex; gap: 0.75rem; margin-bottom: 1rem; }
    .filter-row input, .filter-row select { padding: 0.5rem; border: 1px solid #e2e8f0; border-radius: 6px; font-size: 0.875rem; }

    table { width: 100%; border-collapse: collapse; font-size: 0.8rem; }
    th, td { padding: 0.5rem; text-align: left; border-bottom: 1px solid #f1f5f9; }
    th { background: #f8fafc; font-weight: 500; color: #6b7280; position: sticky; top: 0; }
    .code { font-family: monospace; color: #667eea; }
    .empty { text-align: center; color: #9ca3af; padding: 2rem; }
    .low { color: #ef4444; font-weight: 600; }
    .actions { white-space: nowrap; }
    .btn-icon { padding: 0.25rem; background: none; border: none; cursor: pointer; font-size: 1rem; }

    .badge { padding: 0.2rem 0.4rem; border-radius: 4px; font-size: 0.7rem; }
    .badge.pending { background: #fef3c7; color: #92400e; }
    .badge.completed { background: #d1fae5; color: #065f46; }
    .badge.processing { background: #dbeafe; color: #1d4ed8; }

    .stats-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }
    .stat-box { background: #f8fafc; padding: 1.25rem; border-radius: 12px; }
    .stat-box h4 { margin: 0 0 1rem; font-size: 0.9rem; color: #374151; }

    .bar-chart { display: flex; flex-direction: column; gap: 0.75rem; }
    .bar-item { display: flex; align-items: center; gap: 0.5rem; font-size: 0.8rem; }
    .bar-label { width: 120px; color: #6b7280; }
    .bar { height: 20px; background: #10b981; border-radius: 4px; }
    .bar.warning { background: #f59e0b; }
    .bar.danger { background: #ef4444; }

    .efficiency-stats { display: flex; flex-direction: column; gap: 1rem; }
    .eff-item { display: flex; justify-content: space-between; padding: 0.75rem; background: white; border-radius: 8px; }
    .eff-label { color: #6b7280; font-size: 0.875rem; }
    .eff-value { font-weight: 700; color: #10b981; }

    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal { background: white; border-radius: 16px; padding: 2rem; width: 700px; max-width: 95vw; max-height: 90vh; overflow-y: auto; }
    .modal h3 { margin: 0 0 1.5rem; }
    .form-section { margin-bottom: 1.5rem; }
    .form-section h4 { font-size: 0.875rem; color: #667eea; margin: 0 0 0.75rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 0.5rem; }
    .form-group { margin-bottom: 0.75rem; }
    .form-group label { display: block; margin-bottom: 0.25rem; font-size: 0.75rem; color: #374151; }
    .form-group input { width: 100%; padding: 0.5rem; border: 1px solid #e2e8f0; border-radius: 6px; font-size: 0.875rem; }
    .form-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 0.75rem; }
    .modal-actions { display: flex; gap: 1rem; justify-content: flex-end; margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid #e2e8f0; }
    .btn-cancel, .btn-submit { padding: 0.75rem 1.5rem; border: none; border-radius: 8px; cursor: pointer; }
    .btn-cancel { background: #f1f5f9; color: #374151; }
    .btn-submit { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }
  `]
})
export class AndrologyDashboardComponent implements OnInit {
    activeTab = 'analysis';
    analyses = signal<SemenAnalysis[]>([]);
    washings = signal<SpermWashing[]>([]);
    todayAnalysis = signal(0);
    todayWashing = signal(0);
    pendingCount = signal(0);
    avgConcentration = signal(0);

    showNewAnalysis = false;
    showNewWashing = false;
    filterDate = '';
    filterStatus = '';
    searchTerm = '';

    newAnalysis: any = {};

    constructor(private api: ApiService) { }

    ngOnInit(): void {
        this.loadData();
    }

    loadData(): void {
        // Mock data - would be API calls
        this.analyses.set([
            { id: '1', patientCode: 'BN-2024-001', patientName: 'Nguyễn Văn A', analysisDate: new Date().toISOString(), volume: 3.2, concentration: 45, progressiveMotility: 42, nonProgressiveMotility: 18, immotile: 40, morphology: 8, vitality: 75, status: 'Completed' },
            { id: '2', patientCode: 'BN-2024-002', patientName: 'Trần Văn B', analysisDate: new Date().toISOString(), volume: 2.5, concentration: 12, progressiveMotility: 25, nonProgressiveMotility: 15, immotile: 60, morphology: 3, vitality: 55, status: 'Completed' },
            { id: '3', patientCode: 'BN-2024-003', patientName: 'Lê Văn C', analysisDate: new Date().toISOString(), status: 'Pending' }
        ]);
        this.washings.set([
            { id: '1', cycleCode: 'CK-001', patientName: 'Nguyễn Văn A', method: 'Gradient', prewashConc: 45, postwashConc: 85, postwashMotility: 92, washDate: new Date().toISOString(), status: 'Completed' },
            { id: '2', cycleCode: 'CK-002', patientName: 'Trần Văn B', method: 'Swim-up', prewashConc: 12, postwashConc: 28, postwashMotility: 85, washDate: new Date().toISOString(), status: 'Completed' }
        ]);
        this.todayAnalysis.set(8);
        this.todayWashing.set(5);
        this.pendingCount.set(3);
        this.avgConcentration.set(38);
    }

    filteredAnalyses(): SemenAnalysis[] {
        let result = this.analyses();
        if (this.filterStatus) result = result.filter(a => a.status === this.filterStatus);
        if (this.searchTerm) result = result.filter(a => a.patientCode.includes(this.searchTerm) || a.patientName.includes(this.searchTerm));
        return result;
    }

    formatDate(date: string): string { return new Date(date).toLocaleDateString('vi-VN'); }
    getStatusName(status: string): string {
        const names: Record<string, string> = { 'Pending': 'Chờ XN', 'Processing': 'Đang XN', 'Completed': 'Hoàn thành' };
        return names[status] || status;
    }

    editAnalysis(item: SemenAnalysis): void { console.log('Edit', item); }
    printResult(item: SemenAnalysis): void { console.log('Print', item); }
    submitAnalysis(): void { console.log('Submit', this.newAnalysis); this.showNewAnalysis = false; }
}
