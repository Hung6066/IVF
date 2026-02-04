import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

interface EmbryoCard {
  id: string;
  cycleCode: string;
  patientName: string;
  embryoNumber: number;
  grade: string;
  day: string;
  status: string;
  location?: string;
  notes?: string;
}

interface ScheduleItem {
  id: string;
  time: string;
  patientName: string;
  cycleCode: string;
  procedure: string;
  type: string;
  status: string;
}

interface CryoLocation {
  tank: string;
  canister: number;
  cane: number;
  goblet: number;
  available: number;
  used: number;
}

@Component({
  selector: 'app-lab-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="lab-dashboard">
      <header class="page-header">
        <h1>🧫 Phòng Lab - LABO</h1>
        <div class="tabs">
          <button class="tab" [class.active]="activeTab === 'embryos'" (click)="activeTab = 'embryos'">
            🔬 Phôi đang nuôi
          </button>
          <button class="tab" [class.active]="activeTab === 'schedule'" (click)="activeTab = 'schedule'">
            📅 Lịch thủ thuật
          </button>
          <button class="tab" [class.active]="activeTab === 'cryo'" (click)="activeTab = 'cryo'">
            ❄️ Kho đông lạnh
          </button>
          <button class="tab" [class.active]="activeTab === 'report'" (click)="activeTab = 'report'">
            📊 Báo cáo phôi
          </button>
        </div>
      </header>

      <div class="date-nav">
        <button (click)="prevDay()">◀</button>
        <span class="current-date">{{ currentDate | date:'EEEE, dd/MM/yyyy' }}</span>
        <button (click)="nextDay()">▶</button>
        <button class="btn-today" (click)="goToday()">Hôm nay</button>
      </div>

      <div class="stats-row">
        <div class="stat-card retrieval"><span class="icon">🥚</span><div><span class="value">{{ eggRetrievalCount() }}</span><span class="label">Chọc hút</span></div></div>
        <div class="stat-card culture"><span class="icon">🔬</span><div><span class="value">{{ cultureCount() }}</span><span class="label">Đang nuôi</span></div></div>
        <div class="stat-card transfer"><span class="icon">💉</span><div><span class="value">{{ transferCount() }}</span><span class="label">Chuyển phôi</span></div></div>
        <div class="stat-card freeze"><span class="icon">❄️</span><div><span class="value">{{ freezeCount() }}</span><span class="label">Trữ phôi</span></div></div>
      </div>

      @if (activeTab === 'embryos') {
        <section class="content-section">
          <div class="section-header">
            <h2>Bảng theo dõi phôi - {{ currentDate | date:'dd/MM' }}</h2>
            <div class="filters">
              <select [(ngModel)]="embryoFilter">
                <option value="all">Tất cả</option>
                <option value="D3">Ngày 3</option>
                <option value="D5">Ngày 5</option>
                <option value="D6">Ngày 6</option>
              </select>
            </div>
          </div>
          <div class="embryo-grid">
            @for (embryo of filteredEmbryos(); track embryo.id) {
              <div class="embryo-card" [class]="embryo.status.toLowerCase()" (click)="selectEmbryo(embryo)">
                <div class="embryo-header">
                  <span class="cycle">{{ embryo.cycleCode }}</span>
                  <span class="day-badge">{{ embryo.day }}</span>
                </div>
                <div class="embryo-number">#{{ embryo.embryoNumber }}</div>
                <div class="embryo-grade">{{ embryo.grade }}</div>
                <div class="embryo-patient">{{ embryo.patientName }}</div>
                <div class="embryo-status">{{ getEmbryoStatusName(embryo.status) }}</div>
                @if (embryo.location) {
                  <div class="embryo-location">📍 {{ embryo.location }}</div>
                }
              </div>
            } @empty {
              <div class="empty-full">Không có phôi đang nuôi</div>
            }
          </div>
        </section>
      }

      @if (activeTab === 'schedule') {
        <section class="content-section">
          <div class="section-header">
            <h2>Lịch thủ thuật - {{ currentDate | date:'dd/MM/yyyy' }}</h2>
          </div>
          <div class="schedule-grid">
            <div class="schedule-column">
              <h3>🥚 Chọc hút ({{ getScheduleByType('retrieval').length }})</h3>
              @for (item of getScheduleByType('retrieval'); track item.id) {
                <div class="schedule-item retrieval" [class.done]="item.status === 'done'">
                  <span class="time">{{ item.time }}</span>
                  <div class="details">
                    <strong>{{ item.patientName }}</strong>
                    <span>{{ item.cycleCode }}</span>
                  </div>
                  <button class="btn-status" (click)="toggleStatus(item)">{{ item.status === 'done' ? '✓' : '○' }}</button>
                </div>
              } @empty {
                <div class="empty-schedule">Không có lịch</div>
              }
            </div>
            <div class="schedule-column">
              <h3>💉 Chuyển phôi ({{ getScheduleByType('transfer').length }})</h3>
              @for (item of getScheduleByType('transfer'); track item.id) {
                <div class="schedule-item transfer" [class.done]="item.status === 'done'">
                  <span class="time">{{ item.time }}</span>
                  <div class="details">
                    <strong>{{ item.patientName }}</strong>
                    <span>{{ item.cycleCode }} - {{ item.procedure }}</span>
                  </div>
                  <button class="btn-status" (click)="toggleStatus(item)">{{ item.status === 'done' ? '✓' : '○' }}</button>
                </div>
              } @empty {
                <div class="empty-schedule">Không có lịch</div>
              }
            </div>
            <div class="schedule-column">
              <h3>📋 Báo phôi ({{ getScheduleByType('report').length }})</h3>
              @for (item of getScheduleByType('report'); track item.id) {
                <div class="schedule-item report" [class.done]="item.status === 'done'">
                  <span class="time">{{ item.time }}</span>
                  <div class="details">
                    <strong>{{ item.patientName }}</strong>
                    <span>{{ item.cycleCode }} - {{ item.procedure }}</span>
                  </div>
                  <button class="btn-status" (click)="toggleStatus(item)">{{ item.status === 'done' ? '✓' : '○' }}</button>
                </div>
              } @empty {
                <div class="empty-schedule">Không có lịch</div>
              }
            </div>
          </div>
        </section>
      }

      @if (activeTab === 'cryo') {
        <section class="content-section">
          <div class="section-header">
            <h2>Quản lý kho đông lạnh</h2>
            <button class="btn-new" (click)="showAddCryoLocation = true">➕ Thêm vị trí</button>
          </div>
          <div class="cryo-grid">
            @for (loc of cryoLocations(); track loc.tank) {
              <div class="cryo-tank">
                <div class="tank-header">
                  <span class="tank-name">🧊 {{ loc.tank }}</span>
                  <span class="tank-capacity">{{ loc.used }}/{{ loc.available + loc.used }}</span>
                </div>
                <div class="tank-bar">
                  <div class="tank-fill" [style.width.%]="(loc.used / (loc.available + loc.used)) * 100"></div>
                </div>
                <div class="tank-details">
                  <span>{{ loc.canister }} canisters</span>
                  <span>{{ loc.cane }} canes</span>
                  <span>{{ loc.goblet }} goblets</span>
                </div>
              </div>
            }
          </div>
          <div class="cryo-summary">
            <div class="summary-item"><span class="label">Tổng phôi đông lạnh</span><span class="value">{{ totalFrozenEmbryos() }}</span></div>
            <div class="summary-item"><span class="label">Tổng trứng đông lạnh</span><span class="value">{{ totalFrozenEggs() }}</span></div>
            <div class="summary-item"><span class="label">Tổng tinh trùng đông lạnh</span><span class="value">{{ totalFrozenSperm() }}</span></div>
          </div>
        </section>
      }

      @if (activeTab === 'report') {
        <section class="content-section">
          <div class="section-header">
            <h2>Báo cáo phôi theo ngày</h2>
            <div class="report-actions">
              <button class="btn-export" (click)="exportExcel()">📄 Xuất Excel</button>
              <button class="btn-print" (click)="printReport()">🖨️ In báo cáo</button>
            </div>
          </div>
          <table class="report-table">
            <thead>
              <tr>
                <th>Mã CK</th>
                <th>Bệnh nhân</th>
                <th>Ngày chọc</th>
                <th>Số trứng</th>
                <th>MII</th>
                <th>2PN</th>
                <th>D3</th>
                <th>D5/D6</th>
                <th>Chuyển</th>
                <th>Trữ</th>
                <th>Kết quả</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td class="code">CK-001</td>
                <td>Nguyễn T.H</td>
                <td>28/01</td>
                <td>15</td>
                <td>12</td>
                <td>10</td>
                <td>8</td>
                <td>6 (4AA, 4BA, 3AB...)</td>
                <td>2</td>
                <td>4</td>
                <td><span class="badge completed">Hoàn thành</span></td>
              </tr>
              <tr>
                <td class="code">CK-002</td>
                <td>Trần M.L</td>
                <td>29/01</td>
                <td>8</td>
                <td>6</td>
                <td>5</td>
                <td>4</td>
                <td>—</td>
                <td>—</td>
                <td>—</td>
                <td><span class="badge processing">D3</span></td>
              </tr>
            </tbody>
          </table>
        </section>
      }
    </div>
  `,
  styles: [`
    .lab-dashboard { max-width: 1400px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; gap: 1rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .tabs { display: flex; gap: 0.5rem; }
    .tab { padding: 0.5rem 1rem; background: #f1f5f9; border: none; border-radius: 8px; cursor: pointer; font-size: 0.8rem; }
    .tab.active { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }

    .date-nav { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; background: white; padding: 0.75rem 1rem; border-radius: 12px; box-shadow: 0 2px 4px rgba(0,0,0,0.05); }
    .date-nav button { padding: 0.5rem 1rem; background: #f1f5f9; border: none; border-radius: 6px; cursor: pointer; }
    .btn-today { background: #667eea; color: white; }
    .current-date { font-weight: 600; color: #1e1e2f; }

    .stats-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; margin-bottom: 1.5rem; }
    .stat-card { background: white; border-radius: 12px; padding: 1rem; display: flex; align-items: center; gap: 1rem; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }
    .stat-card .icon { font-size: 1.75rem; }
    .stat-card .value { display: block; font-size: 1.5rem; font-weight: 700; color: #1e1e2f; }
    .stat-card .label { color: #6b7280; font-size: 0.75rem; }

    .content-section { background: white; border-radius: 16px; padding: 1.5rem; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    h2 { font-size: 1rem; color: #374151; margin: 0; }
    .btn-new { padding: 0.5rem 1rem; background: linear-gradient(135deg, #667eea, #764ba2); color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 0.8rem; }

    .embryo-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(130px, 1fr)); gap: 1rem; }
    .embryo-card { background: #f8fafc; border-radius: 12px; padding: 1rem; text-align: center; border: 2px solid transparent; cursor: pointer; transition: all 0.2s; }
    .embryo-card:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
    .embryo-card.developing { border-color: #fbbf24; }
    .embryo-card.frozen { border-color: #06b6d4; background: #ecfeff; }
    .embryo-card.transferred { border-color: #10b981; background: #ecfdf5; }
    .embryo-header { display: flex; justify-content: space-between; font-size: 0.7rem; margin-bottom: 0.5rem; }
    .cycle { color: #6b7280; }
    .day-badge { background: #667eea; color: white; padding: 0.125rem 0.375rem; border-radius: 4px; font-size: 0.625rem; }
    .embryo-number { font-size: 0.8rem; color: #6b7280; }
    .embryo-grade { font-size: 1.5rem; font-weight: 700; color: #1e1e2f; }
    .embryo-patient { font-size: 0.7rem; color: #6b7280; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .embryo-status { font-size: 0.625rem; color: #9ca3af; }
    .embryo-location { font-size: 0.625rem; color: #667eea; }

    .schedule-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1.5rem; }
    .schedule-column h3 { font-size: 0.9rem; color: #374151; margin: 0 0 1rem; }
    .schedule-item { display: flex; align-items: center; gap: 0.75rem; padding: 0.75rem; background: #f8fafc; border-radius: 8px; margin-bottom: 0.5rem; border-left: 3px solid #667eea; }
    .schedule-item.retrieval { border-left-color: #f59e0b; }
    .schedule-item.transfer { border-left-color: #10b981; }
    .schedule-item.report { border-left-color: #8b5cf6; }
    .schedule-item.done { opacity: 0.6; }
    .time { font-weight: 600; color: #667eea; font-size: 0.8rem; min-width: 45px; }
    .details { flex: 1; }
    .details strong { display: block; font-size: 0.8rem; }
    .details span { font-size: 0.7rem; color: #6b7280; }
    .btn-status { width: 24px; height: 24px; border-radius: 50%; border: 2px solid #667eea; background: white; cursor: pointer; font-size: 0.75rem; color: #667eea; }
    .empty-schedule { text-align: center; color: #9ca3af; padding: 1rem; font-size: 0.8rem; }

    .cryo-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
    .cryo-tank { background: #f8fafc; padding: 1rem; border-radius: 12px; }
    .tank-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .tank-name { font-weight: 600; }
    .tank-capacity { font-size: 0.8rem; color: #6b7280; }
    .tank-bar { height: 8px; background: #e2e8f0; border-radius: 4px; overflow: hidden; }
    .tank-fill { height: 100%; background: linear-gradient(90deg, #06b6d4, #667eea); }
    .tank-details { display: flex; justify-content: space-between; margin-top: 0.5rem; font-size: 0.75rem; color: #6b7280; }

    .cryo-summary { display: flex; gap: 1rem; padding: 1rem; background: #f8fafc; border-radius: 12px; }
    .summary-item { flex: 1; text-align: center; }
    .summary-item .label { display: block; font-size: 0.75rem; color: #6b7280; }
    .summary-item .value { font-size: 1.5rem; font-weight: 700; color: #667eea; }

    .report-actions { display: flex; gap: 0.5rem; }
    .btn-export, .btn-print { padding: 0.5rem 1rem; background: #f1f5f9; border: none; border-radius: 6px; cursor: pointer; font-size: 0.8rem; }

    .report-table { width: 100%; border-collapse: collapse; font-size: 0.8rem; }
    .report-table th, .report-table td { padding: 0.75rem 0.5rem; text-align: center; border-bottom: 1px solid #f1f5f9; }
    .report-table th { background: #f8fafc; font-weight: 500; color: #6b7280; }
    .code { font-family: monospace; color: #667eea; }
    .badge { padding: 0.2rem 0.4rem; border-radius: 4px; font-size: 0.7rem; }
    .badge.completed { background: #d1fae5; color: #065f46; }
    .badge.processing { background: #dbeafe; color: #1d4ed8; }

    .empty-full { grid-column: 1 / -1; text-align: center; color: #9ca3af; padding: 3rem; }
  `]
})
export class LabDashboardComponent implements OnInit {
  activeTab = 'embryos';
  currentDate = new Date();
  embryos = signal<EmbryoCard[]>([]);
  schedule = signal<ScheduleItem[]>([]);
  cryoLocations = signal<CryoLocation[]>([]);
  embryoFilter = 'all';

  eggRetrievalCount = signal(0);
  cultureCount = signal(0);
  transferCount = signal(0);
  freezeCount = signal(0);
  totalFrozenEmbryos = signal(0);
  totalFrozenEggs = signal(0);
  totalFrozenSperm = signal(0);

  constructor(private api: ApiService) { }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.embryos.set([
      { id: '1', cycleCode: 'CK-001', patientName: 'Nguyễn T.H', embryoNumber: 1, grade: '4AA', day: 'D5', status: 'Developing' },
      { id: '2', cycleCode: 'CK-001', patientName: 'Nguyễn T.H', embryoNumber: 2, grade: '3AB', day: 'D5', status: 'Developing' },
      { id: '3', cycleCode: 'CK-001', patientName: 'Nguyễn T.H', embryoNumber: 3, grade: '4BA', day: 'D5', status: 'Developing' },
      { id: '4', cycleCode: 'CK-002', patientName: 'Trần M.L', embryoNumber: 1, grade: '8A', day: 'D3', status: 'Developing' },
      { id: '5', cycleCode: 'CK-002', patientName: 'Trần M.L', embryoNumber: 2, grade: '7B', day: 'D3', status: 'Developing' },
      { id: '6', cycleCode: 'CK-003', patientName: 'Lê V.A', embryoNumber: 1, grade: '4AA', day: 'D6', status: 'Frozen', location: 'T1-C2-G5' }
    ]);
    this.schedule.set([
      { id: '1', time: '08:00', patientName: 'Phạm T.B', cycleCode: 'CK-010', procedure: 'Chọc hút', type: 'retrieval', status: 'pending' },
      { id: '2', time: '08:30', patientName: 'Hoàng T.C', cycleCode: 'CK-011', procedure: 'Chọc hút', type: 'retrieval', status: 'pending' },
      { id: '3', time: '09:30', patientName: 'Nguyễn T.H', cycleCode: 'CK-001', procedure: 'CP D5 - 2 phôi', type: 'transfer', status: 'pending' },
      { id: '4', time: '10:00', patientName: 'Trần M.L', cycleCode: 'CK-002', procedure: 'Báo phôi D3', type: 'report', status: 'done' }
    ]);
    this.cryoLocations.set([
      { tank: 'Tank 1', canister: 6, cane: 36, goblet: 216, available: 45, used: 171 },
      { tank: 'Tank 2', canister: 6, cane: 36, goblet: 216, available: 120, used: 96 },
      { tank: 'Tank 3', canister: 4, cane: 24, goblet: 144, available: 80, used: 64 }
    ]);
    this.eggRetrievalCount.set(3);
    this.cultureCount.set(12);
    this.transferCount.set(2);
    this.freezeCount.set(5);
    this.totalFrozenEmbryos.set(342);
    this.totalFrozenEggs.set(128);
    this.totalFrozenSperm.set(256);
  }

  filteredEmbryos(): EmbryoCard[] {
    if (this.embryoFilter === 'all') return this.embryos();
    return this.embryos().filter(e => e.day === this.embryoFilter);
  }

  getScheduleByType(type: string): ScheduleItem[] {
    return this.schedule().filter(s => s.type === type);
  }

  prevDay(): void { this.currentDate = new Date(this.currentDate.setDate(this.currentDate.getDate() - 1)); }
  nextDay(): void { this.currentDate = new Date(this.currentDate.setDate(this.currentDate.getDate() + 1)); }
  goToday(): void { this.currentDate = new Date(); }

  selectEmbryo(embryo: EmbryoCard): void { console.log('Selected', embryo); }
  toggleStatus(item: ScheduleItem): void { item.status = item.status === 'done' ? 'pending' : 'done'; this.schedule.update(s => [...s]); }

  getEmbryoStatusName(status: string): string {
    const names: Record<string, string> = { 'Developing': 'Đang nuôi', 'Frozen': 'Đông lạnh', 'Transferred': 'Đã chuyển', 'Discarded': 'Loại bỏ' };
    return names[status] || status;
  }

  showAddCryoLocation = false;
  exportExcel(): void { alert('Đang xuất file Excel...'); }
  printReport(): void { window.print(); }
}
