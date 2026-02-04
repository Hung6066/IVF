import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
  selector: 'app-sperm-bank-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="sperm-bank">
      <header class="page-header">
        <h1>🏦 Ngân hàng tinh trùng</h1>
        <div class="tabs">
          <button class="tab" [class.active]="activeTab === 'donors'" (click)="activeTab = 'donors'">👤 Người hiến</button>
          <button class="tab" [class.active]="activeTab === 'samples'" (click)="activeTab = 'samples'">🧪 Mẫu lưu trữ</button>
          <button class="tab" [class.active]="activeTab === 'matching'" (click)="activeTab = 'matching'">🔗 Ghép đôi</button>
        </div>
      </header>
      <div class="stats-grid">
        <div class="stat-card"><span class="icon">👥</span><div><span class="value">{{ totalDonors() }}</span><span class="label">Người hiến</span></div></div>
        <div class="stat-card"><span class="icon">🧊</span><div><span class="value">{{ totalSamples() }}</span><span class="label">Mẫu đông lạnh</span></div></div>
        <div class="stat-card"><span class="icon">✅</span><div><span class="value">{{ availableSamples() }}</span><span class="label">Sẵn sàng</span></div></div>
        <div class="stat-card"><span class="icon">⏳</span><div><span class="value">{{ quarantineSamples() }}</span><span class="label">Cách ly</span></div></div>
      </div>
      @if (activeTab === 'donors') {
        <section class="data-section">
          <div class="section-header"><h2>Người hiến tinh trùng</h2><button class="btn-new" (click)="showNewDonor = true">➕ Đăng ký mới</button></div>
          <table><thead><tr><th>Mã</th><th>Nhóm máu</th><th>Tuổi</th><th>Số mẫu</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>@for (d of donors(); track d.id) {<tr><td class="code">{{ d.code }}</td><td>{{ d.bloodType }}</td><td>{{ d.age }}</td><td>{{ d.samples }}</td><td><span class="badge" [class]="d.status">{{ d.status }}</span></td><td><button class="btn-icon" (click)="viewDonor(d)">📋</button><button class="btn-icon" (click)="editDonor(d)">✏️</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Không có dữ liệu</td></tr>}</tbody>
          </table>
        </section>
      }
      @if (activeTab === 'samples') {
        <section class="data-section">
          <div class="section-header"><h2>Kho mẫu</h2><button class="btn-new" (click)="showNewSample = true">➕ Thêm mẫu</button></div>
          <div class="sample-grid">@for (s of samples(); track s.id) {<div class="sample-card" [class]="s.status"><div class="header">{{ s.code }} <button class="btn-sm" (click)="editSample(s)">✏️</button><button class="btn-sm" (click)="deleteSample(s)">🗑️</button></div><div class="info">{{ s.donor }} • {{ s.vials }} vial</div></div>} @empty {<div class="empty">Không có mẫu</div>}</div>
        </section>
      }
      @if (activeTab === 'matching') {
        <section class="data-section">
          <div class="section-header"><h2>Ghép đôi</h2><button class="btn-new" (click)="showNewMatch = true">➕ Ghép đôi mới</button></div>
          <table><thead><tr><th>Người nhận</th><th>Người hiến</th><th>Ngày</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>@for (m of matches(); track m.id) {<tr><td>{{ m.recipient }}</td><td class="code">{{ m.donor }}</td><td>{{ m.date }}</td><td><span class="badge">{{ m.status }}</span></td><td><button class="btn-icon" (click)="viewMatch(m)">👁️</button><button class="btn-icon" (click)="editMatch(m)">✏️</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Không có</td></tr>}</tbody>
          </table>
        </section>
      }
    </div>
  `,
  styles: [`.sperm-bank{max-width:1400px}.page-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:1.5rem;flex-wrap:wrap;gap:1rem}h1{font-size:1.5rem;color:#1e1e2f;margin:0}.tabs{display:flex;gap:.5rem}.tab{padding:.5rem 1rem;background:#f1f5f9;border:none;border-radius:8px;cursor:pointer;font-size:.8rem}.tab.active{background:linear-gradient(135deg,#667eea,#764ba2);color:#fff}.stats-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:1rem;margin-bottom:1.5rem}.stat-card{background:#fff;border-radius:12px;padding:1rem;display:flex;align-items:center;gap:1rem;box-shadow:0 2px 8px rgba(0,0,0,.08)}.stat-card .icon{font-size:1.75rem}.stat-card .value{display:block;font-size:1.5rem;font-weight:700;color:#1e1e2f}.stat-card .label{color:#6b7280;font-size:.75rem}.data-section{background:#fff;border-radius:16px;padding:1.5rem;box-shadow:0 4px 6px -1px rgba(0,0,0,.1)}.section-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:1rem}h2{font-size:1rem;color:#374151;margin:0}.btn-new{padding:.5rem 1rem;background:linear-gradient(135deg,#667eea,#764ba2);color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:.8rem}table{width:100%;border-collapse:collapse;font-size:.8rem}th,td{padding:.5rem;text-align:left;border-bottom:1px solid #f1f5f9}th{background:#f8fafc;color:#6b7280}.code{font-family:monospace;color:#667eea}.empty{text-align:center;color:#9ca3af;padding:2rem}.btn-icon{padding:.25rem;background:#f1f5f9;border:none;border-radius:4px;cursor:pointer}.badge{padding:.2rem .5rem;border-radius:4px;font-size:.7rem}.badge.Active{background:#d1fae5;color:#065f46}.badge.Screening{background:#fef3c7;color:#92400e}.sample-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:1rem}.sample-card{background:#f8fafc;border-radius:12px;padding:1rem;border-left:4px solid #667eea}.sample-card.Available{border-left-color:#10b981}.sample-card.Quarantine{border-left-color:#f59e0b}.sample-card .header{font-weight:600;font-family:monospace}.sample-card .info{font-size:.8rem;color:#6b7280}`]
})
export class SpermBankDashboardComponent implements OnInit {
  activeTab = 'donors';
  donors = signal<any[]>([]);
  samples = signal<any[]>([]);
  matches = signal<any[]>([]);
  totalDonors = signal(12);
  totalSamples = signal(48);
  availableSamples = signal(32);
  quarantineSamples = signal(16);
  showNewDonor = false;

  constructor(private api: ApiService) { }

  ngOnInit(): void {
    this.donors.set([
      { id: '1', code: 'NH-001', bloodType: 'O+', age: 28, samples: 5, status: 'Active' },
      { id: '2', code: 'NH-002', bloodType: 'A+', age: 32, samples: 0, status: 'Screening' }
    ]);
    this.samples.set([
      { id: '1', code: 'SP-001-A', donor: 'NH-001', vials: 3, status: 'Available' },
      { id: '2', code: 'SP-001-B', donor: 'NH-001', vials: 2, status: 'Quarantine' }
    ]);
    this.matches.set([
      { id: '1', recipient: 'Nguyễn T.H', donor: 'NH-001', date: '01/02/2024', status: 'Confirmed' }
    ]);
  }

  showNewSample = false;
  showNewMatch = false;
  viewDonor(d: any): void { alert('Xem thông tin người hiến: ' + d.code); }
  editDonor(d: any): void { alert('Sửa người hiến: ' + d.code); }
  editSample(s: any): void { alert('Sửa mẫu: ' + s.code); }
  deleteSample(s: any): void { if (confirm('Xóa mẫu ' + s.code + '?')) console.log('Deleted', s); }
  viewMatch(m: any): void { alert('Xem ghép đôi: ' + m.recipient + ' - ' + m.donor); }
  editMatch(m: any): void { alert('Sửa ghép đôi'); }
}
