import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Invoice } from '../../../core/models/api.models';

@Component({
    selector: 'app-invoice-list',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="invoice-list">
      <header class="page-header">
        <div>
          <h1>Hoá đơn</h1>
          <p>Quản lý thanh toán và hoá đơn</p>
        </div>
        <button class="btn-primary">➕ Tạo hoá đơn</button>
      </header>

      <div class="search-bar">
        <input
          type="text"
          [(ngModel)]="searchQuery"
          (input)="onSearch()"
          placeholder="🔍 Tìm theo mã hoá đơn, tên bệnh nhân..."
        />
      </div>

      <div class="invoice-grid">
        @for (invoice of invoices(); track invoice.id) {
          <div class="invoice-card" [class]="invoice.status.toLowerCase()">
            <div class="invoice-header">
              <span class="invoice-number">{{ invoice.invoiceNumber }}</span>
              <span class="invoice-status">{{ getStatusName(invoice.status) }}</span>
            </div>
            <div class="invoice-date">{{ formatDate(invoice.invoiceDate) }}</div>
            <div class="invoice-amount">
              <span class="label">Tổng tiền</span>
              <span class="value">{{ formatCurrency(invoice.totalAmount) }}</span>
            </div>
            <div class="invoice-paid">
              <span class="label">Đã thanh toán</span>
              <span class="value">{{ formatCurrency(invoice.paidAmount) }}</span>
            </div>
            <div class="invoice-remaining" *ngIf="invoice.totalAmount > invoice.paidAmount">
              <span class="label">Còn lại</span>
              <span class="value warning">{{ formatCurrency(invoice.totalAmount - invoice.paidAmount) }}</span>
            </div>
            <div class="invoice-actions">
              <button class="btn-action" (click)="viewInvoice(invoice.id)">👁️ Xem</button>
              @if (invoice.status === 'Draft') {
                <button class="btn-action issue" (click)="issueInvoice(invoice.id)">📤 Phát hành</button>
              }
              @if (invoice.status === 'Issued' || invoice.status === 'PartiallyPaid') {
                <button class="btn-action pay" (click)="openPayment(invoice)">💳 Thanh toán</button>
              }
            </div>
          </div>
        } @empty {
          <div class="empty-state">
            {{ loading() ? 'Đang tải...' : 'Không có hoá đơn' }}
          </div>
        }
      </div>

      <!-- Payment Modal -->
      @if (paymentInvoice()) {
        <div class="modal-overlay" (click)="closePayment()">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <h3>Thanh toán hoá đơn {{ paymentInvoice()!.invoiceNumber }}</h3>
            <div class="form-group">
              <label>Số tiền cần thanh toán</label>
              <input type="number" [(ngModel)]="paymentAmount" [max]="paymentInvoice()!.totalAmount - paymentInvoice()!.paidAmount" />
            </div>
            <div class="form-group">
              <label>Phương thức</label>
              <select [(ngModel)]="paymentMethod">
                <option value="Cash">Tiền mặt</option>
                <option value="Card">Thẻ</option>
                <option value="Transfer">Chuyển khoản</option>
              </select>
            </div>
            <div class="modal-actions">
              <button class="btn-cancel" (click)="closePayment()">Huỷ</button>
              <button class="btn-confirm" (click)="submitPayment()">Xác nhận thanh toán</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
    styles: [`
    .invoice-list { max-width: 1400px; }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }

    .page-header h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0 0 0.25rem; }
    .page-header p { color: #6b7280; margin: 0; }

    .btn-primary {
      padding: 0.75rem 1.25rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 8px;
      font-weight: 500;
      cursor: pointer;
    }

    .search-bar { margin-bottom: 1.5rem; }

    .search-bar input {
      width: 100%;
      max-width: 400px;
      padding: 0.75rem 1rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
    }

    .invoice-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
      gap: 1rem;
    }

    .invoice-card {
      background: white;
      border-radius: 12px;
      padding: 1.25rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
      border-left: 4px solid #e5e7eb;
    }

    .invoice-card.draft { border-left-color: #6b7280; }
    .invoice-card.issued { border-left-color: #3b82f6; }
    .invoice-card.partiallypaid { border-left-color: #f59e0b; }
    .invoice-card.paid { border-left-color: #10b981; }
    .invoice-card.cancelled { border-left-color: #ef4444; opacity: 0.7; }

    .invoice-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.5rem;
    }

    .invoice-number { font-weight: 600; font-size: 1rem; }

    .invoice-status {
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 500;
      background: #f1f5f9;
    }

    .invoice-card.paid .invoice-status { background: #d1fae5; color: #065f46; }
    .invoice-card.partiallypaid .invoice-status { background: #fef3c7; color: #92400e; }

    .invoice-date { color: #6b7280; font-size: 0.875rem; margin-bottom: 1rem; }

    .invoice-amount, .invoice-paid, .invoice-remaining {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }

    .label { color: #6b7280; font-size: 0.875rem; }
    .value { font-weight: 500; }
    .value.warning { color: #f59e0b; }

    .invoice-actions {
      display: flex;
      gap: 0.5rem;
      margin-top: 1rem;
      padding-top: 1rem;
      border-top: 1px solid #f1f5f9;
    }

    .btn-action {
      flex: 1;
      padding: 0.5rem;
      background: #f1f5f9;
      border: none;
      border-radius: 6px;
      font-size: 0.8125rem;
      cursor: pointer;
    }

    .btn-action.issue { background: #dbeafe; color: #1d4ed8; }
    .btn-action.pay { background: #d1fae5; color: #065f46; }

    .empty-state {
      grid-column: 1 / -1;
      text-align: center;
      color: #9ca3af;
      padding: 3rem;
      background: white;
      border-radius: 12px;
    }

    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }

    .modal-content {
      background: white;
      border-radius: 16px;
      padding: 2rem;
      width: 100%;
      max-width: 400px;
    }

    .modal-content h3 { margin: 0 0 1.5rem; }

    .form-group { margin-bottom: 1rem; }
    .form-group label { display: block; margin-bottom: 0.5rem; font-size: 0.875rem; color: #374151; }
    .form-group input, .form-group select {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
    }

    .modal-actions {
      display: flex;
      gap: 1rem;
      margin-top: 1.5rem;
    }

    .btn-cancel, .btn-confirm {
      flex: 1;
      padding: 0.75rem;
      border: none;
      border-radius: 8px;
      cursor: pointer;
    }

    .btn-cancel { background: #f1f5f9; }
    .btn-confirm { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }
  `]
})
export class InvoiceListComponent implements OnInit {
    invoices = signal<Invoice[]>([]);
    loading = signal(false);
    searchQuery = '';
    paymentInvoice = signal<Invoice | null>(null);
    paymentAmount = 0;
    paymentMethod = 'Cash';

    private searchTimeout?: ReturnType<typeof setTimeout>;

    constructor(private api: ApiService) { }

    ngOnInit(): void {
        this.loadInvoices();
    }

    loadInvoices(): void {
        this.loading.set(true);
        this.api.searchInvoices(this.searchQuery || undefined).subscribe({
            next: (res) => {
                this.invoices.set(res.items);
                this.loading.set(false);
            },
            error: () => this.loading.set(false)
        });
    }

    onSearch(): void {
        clearTimeout(this.searchTimeout);
        this.searchTimeout = setTimeout(() => this.loadInvoices(), 300);
    }

    getStatusName(status: string): string {
        const names: Record<string, string> = {
            'Draft': 'Nháp', 'Issued': 'Đã phát hành', 'PartiallyPaid': 'Thanh toán một phần',
            'Paid': 'Đã thanh toán', 'Refunded': 'Hoàn tiền', 'Cancelled': 'Đã huỷ'
        };
        return names[status] || status;
    }

    formatDate(date: string): string {
        return new Date(date).toLocaleDateString('vi-VN');
    }

    formatCurrency(value: number): string {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
    }

    viewInvoice(id: string): void {
        // Navigate to invoice detail
    }

    issueInvoice(id: string): void {
        this.api.issueInvoice(id).subscribe(() => this.loadInvoices());
    }

    openPayment(invoice: Invoice): void {
        this.paymentInvoice.set(invoice);
        this.paymentAmount = invoice.totalAmount - invoice.paidAmount;
    }

    closePayment(): void {
        this.paymentInvoice.set(null);
    }

    submitPayment(): void {
        const invoice = this.paymentInvoice();
        if (invoice) {
            this.api.recordPayment(invoice.id, {
                amount: this.paymentAmount,
                paymentMethod: this.paymentMethod
            }).subscribe(() => {
                this.closePayment();
                this.loadInvoices();
            });
        }
    }
}
