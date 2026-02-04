import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
  selector: 'app-pharmacy-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="pharmacy-dashboard">
      <header class="page-header">
        <h1>💊 Nhà thuốc</h1>
        <div class="tabs">
          <button class="tab" [class.active]="activeTab === 'prescriptions'" (click)="activeTab = 'prescriptions'">📋 Đơn thuốc</button>
          <button class="tab" [class.active]="activeTab === 'inventory'" (click)="activeTab = 'inventory'">📦 Tồn kho</button>
          <button class="tab" [class.active]="activeTab === 'import'" (click)="activeTab = 'import'">📥 Nhập kho</button>
        </div>
      </header>
      <div class="stats-grid">
        <div class="stat-card"><span class="icon">📋</span><div><span class="value">{{ pendingRx() }}</span><span class="label">Chờ phát</span></div></div>
        <div class="stat-card"><span class="icon">✅</span><div><span class="value">{{ completedRx() }}</span><span class="label">Đã phát</span></div></div>
        <div class="stat-card"><span class="icon">⚠️</span><div><span class="value">{{ lowStockCount() }}</span><span class="label">Sắp hết</span></div></div>
        <div class="stat-card"><span class="icon">📦</span><div><span class="value">{{ totalItems() }}</span><span class="label">Tổng mặt hàng</span></div></div>
      </div>
      @if (activeTab === 'prescriptions') {
        <section class="data-section">
          <h2>Hàng đợi đơn thuốc</h2>
          <div class="rx-queue">@for (rx of prescriptions(); track rx.id) {
            <div class="rx-card" [class]="rx.status.toLowerCase()">
              <div class="rx-header"><span class="rx-patient">{{ rx.patient }}</span><span class="rx-time">{{ rx.time }}</span></div>
              <div class="rx-info">👨‍⚕️ {{ rx.doctor }} • 💊 {{ rx.items }} thuốc</div>
              <div class="rx-actions">
                @if (rx.status === 'Pending') {<button class="btn-process" (click)="processRx(rx)">Bắt đầu</button>}
                @else if (rx.status === 'Processing') {<button class="btn-complete" (click)="completeRx(rx)">Hoàn thành</button>}
                @else {<span class="done-badge">✓ Đã phát</span>}
              </div>
            </div>
          } @empty {<div class="empty">Không có đơn thuốc</div>}</div>
        </section>
      }
      @if (activeTab === 'inventory') {
        <section class="data-section">
          <div class="section-header"><h2>Tồn kho thuốc</h2><input type="text" placeholder="Tìm thuốc..." [(ngModel)]="drugSearch" /></div>
          <table><thead><tr><th>Mã</th><th>Tên thuốc</th><th>ĐVT</th><th>Tồn</th><th>Tối thiểu</th><th>Hạn sử dụng</th><th>Trạng thái</th></tr></thead>
            <tbody>@for (d of filteredDrugs(); track d.id) {<tr [class.low-stock]="d.stock < d.minStock"><td class="code">{{ d.code }}</td><td>{{ d.name }}</td><td>{{ d.unit }}</td><td>{{ d.stock }}</td><td>{{ d.minStock }}</td><td>{{ d.expiry }}</td><td><span class="badge" [class]="d.stock < d.minStock ? 'warning' : 'ok'">{{ d.stock < d.minStock ? 'Sắp hết' : 'Đủ' }}</span></td></tr>} @empty {<tr><td colspan="7" class="empty">Không có dữ liệu</td></tr>}</tbody>
          </table>
        </section>
      }
      @if (activeTab === 'import') {
        <section class="data-section">
          <div class="section-header"><h2>Lịch sử nhập kho</h2><button class="btn-new" (click)="showNewImport = true">➕ Nhập kho mới</button></div>
          <table><thead><tr><th>Mã phiếu</th><th>Ngày</th><th>NCC</th><th>Số mặt hàng</th><th>Tổng tiền</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>@for (i of imports(); track i.id) {<tr><td class="code">{{ i.code }}</td><td>{{ i.date }}</td><td>{{ i.supplier }}</td><td>{{ i.items }}</td><td>{{ formatCurrency(i.total) }}</td><td><span class="badge completed">{{ i.status }}</span></td><td><button class="btn-icon" (click)="viewImport(i)">👁️</button><button class="btn-icon" (click)="editImport(i)">✏️</button><button class="btn-icon" (click)="deleteImport(i)">🗑️</button></td></tr>} @empty {<tr><td colspan="7" class="empty">Không có lịch sử</td></tr>}</tbody>
          </table>
        </section>
      }
    </div>
  `,
  styles: [`.pharmacy-dashboard{max-width:1400px}.page-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:1.5rem;flex-wrap:wrap;gap:1rem}h1{font-size:1.5rem;color:#1e1e2f;margin:0}.tabs{display:flex;gap:.5rem}.tab{padding:.5rem 1rem;background:#f1f5f9;border:none;border-radius:8px;cursor:pointer;font-size:.8rem}.tab.active{background:linear-gradient(135deg,#667eea,#764ba2);color:#fff}.stats-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:1rem;margin-bottom:1.5rem}.stat-card{background:#fff;border-radius:12px;padding:1rem;display:flex;align-items:center;gap:1rem;box-shadow:0 2px 8px rgba(0,0,0,.08)}.stat-card .icon{font-size:1.75rem}.stat-card .value{display:block;font-size:1.5rem;font-weight:700;color:#1e1e2f}.stat-card .label{color:#6b7280;font-size:.75rem}.data-section{background:#fff;border-radius:16px;padding:1.5rem;box-shadow:0 4px 6px -1px rgba(0,0,0,.1)}.section-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:1rem;gap:1rem}.section-header input{padding:.5rem;border:1px solid #e2e8f0;border-radius:6px}h2{font-size:1rem;color:#374151;margin:0}.btn-new{padding:.5rem 1rem;background:linear-gradient(135deg,#667eea,#764ba2);color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:.8rem}.rx-queue{display:flex;flex-direction:column;gap:1rem}.rx-card{background:#f8fafc;border-radius:12px;padding:1rem;border-left:4px solid #f59e0b}.rx-card.processing{border-left-color:#3b82f6;background:#eff6ff}.rx-card.completed{border-left-color:#10b981;opacity:.7}.rx-header{display:flex;justify-content:space-between;margin-bottom:.5rem}.rx-patient{font-weight:600}.rx-time{font-size:.75rem;color:#9ca3af}.rx-info{font-size:.875rem;color:#6b7280;margin-bottom:.75rem}.rx-actions{display:flex;gap:.5rem}.btn-process,.btn-complete{padding:.5rem 1rem;border:none;border-radius:6px;cursor:pointer;font-size:.75rem}.btn-process{background:#fef3c7;color:#92400e}.btn-complete{background:#667eea;color:#fff}.done-badge{color:#10b981;font-size:.875rem}table{width:100%;border-collapse:collapse;font-size:.8rem}th,td{padding:.5rem;text-align:left;border-bottom:1px solid #f1f5f9}th{background:#f8fafc;color:#6b7280}.code{font-family:monospace;color:#667eea}.empty{text-align:center;color:#9ca3af;padding:2rem}.low-stock{background:#fef2f2}.badge{padding:.2rem .5rem;border-radius:4px;font-size:.7rem}.badge.ok{background:#d1fae5;color:#065f46}.badge.warning{background:#fee2e2;color:#991b1b}.badge.completed{background:#d1fae5;color:#065f46}`]
})
export class PharmacyDashboardComponent implements OnInit {
  activeTab = 'prescriptions';
  prescriptions = signal<any[]>([]);
  drugs = signal<any[]>([]);
  imports = signal<any[]>([]);
  pendingRx = signal(0);
  completedRx = signal(0);
  lowStockCount = signal(0);
  totalItems = signal(0);
  drugSearch = '';

  constructor(private api: ApiService, private router: Router) { }

  ngOnInit(): void {
    this.prescriptions.set([
      { id: '1', patient: 'Nguyễn T.A', doctor: 'BS. Trần B', items: 5, time: '09:30', status: 'Pending' },
      { id: '2', patient: 'Lê V.C', doctor: 'BS. Phạm D', items: 3, time: '10:15', status: 'Processing' },
      { id: '3', patient: 'Hoàng T.E', doctor: 'BS. Trần B', items: 2, time: '08:45', status: 'Completed' }
    ]);
    this.drugs.set([
      { id: '1', code: 'GNL450', name: 'Gonal-F 450IU', unit: 'Lọ', stock: 5, minStock: 20, expiry: '06/2025' },
      { id: '2', code: 'PRG200', name: 'Progesterone 200mg', unit: 'Viên', stock: 250, minStock: 100, expiry: '12/2025' },
      { id: '3', code: 'CTR025', name: 'Cetrotide 0.25mg', unit: 'Lọ', stock: 45, minStock: 30, expiry: '09/2025' }
    ]);
    this.imports.set([
      { id: '1', code: 'NK-001', date: '01/02/2024', supplier: 'Dược phẩm ABC', items: 15, total: 125000000, status: 'Hoàn thành' }
    ]);
    this.pendingRx.set(8);
    this.completedRx.set(24);
    this.lowStockCount.set(3);
    this.totalItems.set(156);
  }

  filteredDrugs(): any[] {
    if (!this.drugSearch) return this.drugs();
    return this.drugs().filter(d => d.name.toLowerCase().includes(this.drugSearch.toLowerCase()));
  }

  processRx(rx: any): void { rx.status = 'Processing'; this.prescriptions.update(l => [...l]); }
  completeRx(rx: any): void { rx.status = 'Completed'; this.prescriptions.update(l => [...l]); }
  formatCurrency(v: number): string { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(v); }

  showNewImport = false;
  viewImport(i: any): void { alert('Chi tiết phiếu nhập: ' + i.code + '\nNCC: ' + i.supplier + '\nSố mặt hàng: ' + i.items); }
  editImport(i: any): void { alert('Sửa phiếu nhập: ' + i.code); }
  deleteImport(i: any): void { if (confirm('Xóa phiếu nhập ' + i.code + '?')) { this.imports.update(list => list.filter(x => x.id !== i.id)); } }
}
