import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-invoice-list',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="billing-dashboard">
      <header class="page-header">
        <h1>💰 Hoá đơn & Thanh toán</h1>
        <div class="tabs">
          <button class="tab" [class.active]="activeTab === 'invoices'" (click)="activeTab = 'invoices'">📋 Hoá đơn</button>
          <button class="tab" [class.active]="activeTab === 'payments'" (click)="activeTab = 'payments'">💳 Thanh toán</button>
          <button class="tab" [class.active]="activeTab === 'revenue'" (click)="activeTab = 'revenue'">📊 Doanh thu</button>
        </div>
      </header>
      <div class="stats-grid">
        <div class="stat-card"><span class="icon">📄</span><div><span class="value">{{ todayInvoices() }}</span><span class="label">Hoá đơn hôm nay</span></div></div>
        <div class="stat-card"><span class="icon">💰</span><div><span class="value">{{ formatCurrency(todayRevenue()) }}</span><span class="label">Thu hôm nay</span></div></div>
        <div class="stat-card"><span class="icon">⏳</span><div><span class="value">{{ pendingPayments() }}</span><span class="label">Chờ thanh toán</span></div></div>
        <div class="stat-card"><span class="icon">🏦</span><div><span class="value">{{ formatCurrency(monthRevenue()) }}</span><span class="label">Thu tháng này</span></div></div>
      </div>
      @if (activeTab === 'invoices') {
        <section class="data-section">
          <div class="section-header"><h2>Danh sách hoá đơn</h2><button class="btn-new" (click)="showCreateInvoice = true">➕ Tạo hoá đơn</button></div>
          <div class="filter-row">
            <input type="date" [(ngModel)]="filterDate" />
            <select [(ngModel)]="filterStatus"><option value="">Tất cả</option><option value="Paid">Đã thanh toán</option><option value="Pending">Chờ thanh toán</option><option value="Partial">Thanh toán 1 phần</option></select>
            <input type="text" placeholder="Tìm mã hoá đơn..." [(ngModel)]="searchTerm" />
          </div>
          <table><thead><tr><th>Mã HD</th><th>Bệnh nhân</th><th>Ngày</th><th>Tổng tiền</th><th>Đã thu</th><th>Còn lại</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>@for (inv of filteredInvoices(); track inv.id) {<tr><td class="code">{{ inv.code }}</td><td>{{ inv.patient }}</td><td>{{ inv.date }}</td><td class="amount">{{ formatCurrency(inv.total) }}</td><td>{{ formatCurrency(inv.paid) }}</td><td [class.pending]="inv.remaining > 0">{{ formatCurrency(inv.remaining) }}</td><td><span class="badge" [class]="inv.status.toLowerCase()">{{ getStatusName(inv.status) }}</span></td><td><button class="btn-icon" title="Chi tiết" (click)="viewInvoice(inv)">📋</button><button class="btn-icon" title="Thu tiền" (click)="payInvoice(inv)">💳</button><button class="btn-icon" title="In" (click)="printInvoice(inv)">🖨️</button></td></tr>} @empty {<tr><td colspan="8" class="empty">Không có hoá đơn</td></tr>}</tbody>
          </table>
        </section>
      }
      @if (activeTab === 'payments') {
        <section class="data-section">
          <h2>Lịch sử thanh toán</h2>
          <table><thead><tr><th>Mã</th><th>Hoá đơn</th><th>Bệnh nhân</th><th>Số tiền</th><th>Phương thức</th><th>Thời gian</th><th>Thu ngân</th></tr></thead>
            <tbody>@for (p of payments(); track p.id) {<tr><td class="code">{{ p.code }}</td><td class="code">{{ p.invoice }}</td><td>{{ p.patient }}</td><td class="amount">{{ formatCurrency(p.amount) }}</td><td>{{ p.method }}</td><td>{{ p.datetime }}</td><td>{{ p.cashier }}</td></tr>} @empty {<tr><td colspan="7" class="empty">Không có lịch sử</td></tr>}</tbody>
          </table>
        </section>
      }
      @if (activeTab === 'revenue') {
        <section class="data-section">
          <h2>Báo cáo doanh thu</h2>
          <div class="revenue-grid">
            <div class="rev-card"><h4>Tuần này</h4><span class="rev-value">{{ formatCurrency(weekRevenue()) }}</span><span class="rev-trend up">↑ 12%</span></div>
            <div class="rev-card"><h4>Tháng này</h4><span class="rev-value">{{ formatCurrency(monthRevenue()) }}</span><span class="rev-trend up">↑ 8%</span></div>
            <div class="rev-card"><h4>Quý này</h4><span class="rev-value">{{ formatCurrency(quarterRevenue()) }}</span><span class="rev-trend down">↓ 3%</span></div>
          </div>
          <div class="chart-placeholder"><h4>Biểu đồ doanh thu theo ngày</h4><div class="bars">@for (d of chartData(); track d.day) {<div class="bar-col"><div class="bar" [style.height.%]="d.pct"></div><span>{{ d.day }}</span></div>}</div></div>
        </section>
      }
    </div>
  `,
    styles: [`.billing-dashboard{max-width:1400px}.page-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:1.5rem;flex-wrap:wrap;gap:1rem}h1{font-size:1.5rem;color:#1e1e2f;margin:0}.tabs{display:flex;gap:.5rem}.tab{padding:.5rem 1rem;background:#f1f5f9;border:none;border-radius:8px;cursor:pointer;font-size:.8rem}.tab.active{background:linear-gradient(135deg,#667eea,#764ba2);color:#fff}.stats-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:1rem;margin-bottom:1.5rem}.stat-card{background:#fff;border-radius:12px;padding:1rem;display:flex;align-items:center;gap:1rem;box-shadow:0 2px 8px rgba(0,0,0,.08)}.stat-card .icon{font-size:1.75rem}.stat-card .value{display:block;font-size:1.25rem;font-weight:700;color:#1e1e2f}.stat-card .label{color:#6b7280;font-size:.75rem}.data-section{background:#fff;border-radius:16px;padding:1.5rem;box-shadow:0 4px 6px -1px rgba(0,0,0,.1)}.section-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:1rem}h2{font-size:1rem;color:#374151;margin:0}.btn-new{padding:.5rem 1rem;background:linear-gradient(135deg,#667eea,#764ba2);color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:.8rem}.filter-row{display:flex;gap:.75rem;margin-bottom:1rem}.filter-row input,.filter-row select{padding:.5rem;border:1px solid #e2e8f0;border-radius:6px;font-size:.875rem}table{width:100%;border-collapse:collapse;font-size:.8rem}th,td{padding:.5rem;text-align:left;border-bottom:1px solid #f1f5f9}th{background:#f8fafc;color:#6b7280}.code{font-family:monospace;color:#667eea}.amount{font-weight:600}.pending{color:#f59e0b}.empty{text-align:center;color:#9ca3af;padding:2rem}.btn-icon{padding:.25rem;background:#f1f5f9;border:none;border-radius:4px;cursor:pointer;margin-right:.25rem}.badge{padding:.2rem .5rem;border-radius:4px;font-size:.7rem}.badge.paid{background:#d1fae5;color:#065f46}.badge.pending{background:#fef3c7;color:#92400e}.badge.partial{background:#dbeafe;color:#1d4ed8}.revenue-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:1rem;margin-bottom:1.5rem}.rev-card{background:#f8fafc;padding:1.25rem;border-radius:12px;text-align:center}.rev-card h4{margin:0 0 .5rem;font-size:.8rem;color:#6b7280}.rev-value{font-size:1.5rem;font-weight:700;color:#1e1e2f}.rev-trend{font-size:.75rem;margin-left:.5rem}.rev-trend.up{color:#10b981}.rev-trend.down{color:#ef4444}.chart-placeholder{background:#f8fafc;padding:1.5rem;border-radius:12px}.chart-placeholder h4{margin:0 0 1rem;font-size:.9rem}.bars{display:flex;align-items:flex-end;gap:.5rem;height:120px}.bar-col{flex:1;display:flex;flex-direction:column;align-items:center}.bar{width:100%;background:linear-gradient(180deg,#667eea,#764ba2);border-radius:4px 4px 0 0}.bar-col span{font-size:.625rem;color:#6b7280;margin-top:.25rem}`]
})
export class InvoiceListComponent implements OnInit {
    activeTab = 'invoices';
    invoices = signal<any[]>([]);
    payments = signal<any[]>([]);
    chartData = signal<any[]>([]);

    todayInvoices = signal(0);
    todayRevenue = signal(0);
    pendingPayments = signal(0);
    weekRevenue = signal(0);
    monthRevenue = signal(0);
    quarterRevenue = signal(0);

    filterDate = '';
    filterStatus = '';
    searchTerm = '';

    constructor(private api: ApiService) { }

    ngOnInit(): void {
        this.invoices.set([
            { id: '1', code: 'HD-001', patient: 'Nguyễn T.H', date: '04/02/2024', total: 25000000, paid: 25000000, remaining: 0, status: 'Paid' },
            { id: '2', code: 'HD-002', patient: 'Trần M.L', date: '04/02/2024', total: 18000000, paid: 10000000, remaining: 8000000, status: 'Partial' },
            { id: '3', code: 'HD-003', patient: 'Lê V.A', date: '04/02/2024', total: 5000000, paid: 0, remaining: 5000000, status: 'Pending' }
        ]);
        this.payments.set([
            { id: '1', code: 'TT-001', invoice: 'HD-001', patient: 'Nguyễn T.H', amount: 25000000, method: 'Chuyển khoản', datetime: '04/02 09:30', cashier: 'Thu Hà' },
            { id: '2', code: 'TT-002', invoice: 'HD-002', patient: 'Trần M.L', amount: 10000000, method: 'Tiền mặt', datetime: '04/02 10:15', cashier: 'Thu Hà' }
        ]);
        this.chartData.set([{ day: 'T2', pct: 65 }, { day: 'T3', pct: 80 }, { day: 'T4', pct: 55 }, { day: 'T5', pct: 90 }, { day: 'T6', pct: 75 }, { day: 'T7', pct: 40 }, { day: 'CN', pct: 20 }]);
        this.todayInvoices.set(12);
        this.todayRevenue.set(185000000);
        this.pendingPayments.set(5);
        this.weekRevenue.set(850000000);
        this.monthRevenue.set(2450000000);
        this.quarterRevenue.set(7200000000);
    }

    filteredInvoices(): any[] {
        let result = this.invoices();
        if (this.filterStatus) result = result.filter(i => i.status === this.filterStatus);
        if (this.searchTerm) result = result.filter(i => i.code.includes(this.searchTerm));
        return result;
    }

    formatCurrency(v: number): string { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(v); }
    getStatusName(s: string): string { return { Paid: 'Đã TT', Pending: 'Chờ TT', Partial: 'TT 1 phần' }[s] || s; }

    showCreateInvoice = false;
    viewInvoice(inv: any): void { alert('Chi tiết hoá đơn: ' + inv.code); }
    payInvoice(inv: any): void { alert('Thu tiền cho: ' + inv.code + ' - Còn lại: ' + this.formatCurrency(inv.remaining)); }
    printInvoice(inv: any): void { window.print(); }
}
