import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, AuditLog, AuditSearchParams } from '../../../core/services/api.service';

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="dashboard-layout">
      <header class="page-header">
        <div class="header-title">
          <h1>📋 Nhật ký hoạt động</h1>
        </div>
      </header>

      <!-- Filters -->
      <div class="card filter-card">
        <div class="filter-row">
          <div class="form-group">
            <label>Loại entity</label>
            <select [(ngModel)]="filters.entityType" (change)="search()">
              <option value="">Tất cả</option>
              <option value="Patient">Bệnh nhân</option>
              <option value="Couple">Cặp vợ chồng</option>
              <option value="TreatmentCycle">Chu kỳ điều trị</option>
              <option value="Appointment">Lịch hẹn</option>
              <option value="Invoice">Hóa đơn</option>
              <option value="User">Người dùng</option>
            </select>
          </div>
          <div class="form-group">
            <label>Hành động</label>
            <select [(ngModel)]="filters.action" (change)="search()">
              <option value="">Tất cả</option>
              <option value="Create">Tạo mới</option>
              <option value="Update">Cập nhật</option>
              <option value="Delete">Xóa</option>
            </select>
          </div>
          <div class="form-group">
            <label>Từ ngày</label>
            <input type="date" [(ngModel)]="fromDate" (change)="search()">
          </div>
          <div class="form-group">
            <label>Đến ngày</label>
            <input type="date" [(ngModel)]="toDate" (change)="search()">
          </div>
          <button class="btn-primary" (click)="search()">Tìm kiếm</button>
        </div>
      </div>

      <!-- Logs Table -->
      <div class="card">
        <div class="section-header">
          <h2>Danh sách nhật ký</h2>
        </div>
        <table class="data-table">
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Người dùng</th>
              <th>Hành động</th>
              <th>Đối tượng</th>
              <th>Thay đổi</th>
              <th>IP</th>
            </tr>
          </thead>
          <tbody>
            @for (log of logs(); track log.id) {
              <tr>
                <td class="time-cell">{{ formatDateTime(log.createdAt) }}</td>
                <td>{{ log.username || 'System' }}</td>
                <td>
                  <span class="status-badge" [class]="log.action.toLowerCase()">{{ getActionLabel(log.action) }}</span>
                </td>
                <td>
                  <div class="entity-type">{{ log.entityType }}</div>
                  <div class="entity-id">{{ log.entityId.substring(0, 8) }}...</div>
                </td>
                <td class="changes-cell">
                  @if (log.changedColumns) {
                    <span class="changed-columns">{{ log.changedColumns }}</span>
                  } @else if (log.action === 'Create') {
                    <span class="new-record">Bản ghi mới</span>
                  } @else if (log.action === 'Delete') {
                    <span class="deleted-record">Đã xóa</span>
                  }
                </td>
                <td class="ip-cell">{{ log.ipAddress || '-' }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="empty-state">Không có dữ liệu</td>
              </tr>
            }
          </tbody>
        </table>

        <!-- Pagination -->
        <div class="pagination">
          <button class="btn-secondary" [disabled]="filters.page === 1" (click)="prevPage()">← Trước</button>
          <span>Trang {{ filters.page }}</span>
          <button class="btn-secondary" [disabled]="logs().length < filters.pageSize!" (click)="nextPage()">Sau →</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-layout {
      padding: 1.5rem;
      max-width: 1400px;
      margin: 0 auto;
    }

    .page-header .header-title h1 {
      font-size: 1.875rem;
      font-weight: 700;
      color: var(--text-primary);
      margin: 0 0 2rem;
    }

    .card {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--border-color);
      margin-bottom: 1.5rem;
    }

    .filter-row {
      display: flex;
      gap: 1rem;
      align-items: flex-end;
      flex-wrap: wrap;
    }

    .form-group {
      flex: 1;
      min-width: 140px;
    }

    .form-group label {
      display: block;
      font-size: 0.75rem;
      color: var(--text-secondary);
      margin-bottom: 0.5rem;
      font-weight: 500;
    }

    .form-group select,
    .form-group input {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid var(--border-color);
      border-radius: 8px;
      background: white;
      color: var(--text-primary);
      font-size: 0.875rem;
    }

    .form-group select:focus,
    .form-group input:focus {
      outline: none;
      border-color: var(--primary);
    }

    .btn-primary {
      background: var(--primary);
      color: white;
      border: none;
      padding: 0.75rem 1.5rem;
      border-radius: 8px;
      font-weight: 500;
      cursor: pointer;
    }

    .btn-primary:hover {
      background: var(--primary-dark);
    }

    .btn-secondary {
      background: #f1f5f9;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
      padding: 0.5rem 1rem;
      border-radius: 6px;
      cursor: pointer;
    }

    .btn-secondary:hover:not(:disabled) {
      background: #e2e8f0;
    }

    .btn-secondary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }

    .section-header h2 {
      font-size: 1.25rem;
      margin: 0;
      color: var(--text-primary);
    }

    .data-table {
      width: 100%;
      border-collapse: collapse;
    }

    .data-table th {
      text-align: left;
      padding: 0.75rem 1rem;
      background: #f8fafc;
      color: var(--text-secondary);
      font-weight: 600;
      font-size: 0.8rem;
      text-transform: uppercase;
      border-bottom: 1px solid var(--border-color);
    }

    .data-table td {
      padding: 1rem;
      border-bottom: 1px solid var(--border-color);
      color: var(--text-primary);
      font-size: 0.875rem;
    }

    .data-table tr:hover td {
      background: #f8fafc;
    }

    .time-cell {
      font-family: monospace;
      font-size: 0.8rem;
      color: var(--text-secondary);
      white-space: nowrap;
    }

    .status-badge {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
    }

    .status-badge.create {
      background: #d1fae5;
      color: #065f46;
    }

    .status-badge.update {
      background: #dbeafe;
      color: #1e40af;
    }

    .status-badge.delete {
      background: #fee2e2;
      color: #991b1b;
    }

    .entity-type {
      font-weight: 500;
      color: var(--text-primary);
    }

    .entity-id {
      font-size: 0.75rem;
      color: var(--text-secondary);
      font-family: monospace;
    }

    .changes-cell {
      max-width: 200px;
    }

    .changed-columns {
      font-size: 0.8rem;
      color: var(--text-secondary);
      word-break: break-all;
    }

    .new-record {
      color: #059669;
      font-size: 0.8rem;
    }

    .deleted-record {
      color: #dc2626;
      font-size: 0.8rem;
    }

    .ip-cell {
      font-family: monospace;
      font-size: 0.8rem;
      color: var(--text-secondary);
    }

    .empty-state {
      text-align: center;
      color: var(--text-secondary);
      padding: 2rem;
    }

    .pagination {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 1rem;
      margin-top: 1.5rem;
      padding-top: 1.5rem;
      border-top: 1px solid var(--border-color);
    }

    .pagination span {
      font-size: 0.875rem;
      color: var(--text-secondary);
    }
  `]
})
export class AuditLogsComponent implements OnInit {
  logs = signal<AuditLog[]>([]);
  filters: AuditSearchParams = {
    page: 1,
    pageSize: 50
  };
  fromDate = '';
  toDate = '';

  constructor(private api: ApiService) { }

  ngOnInit() {
    this.loadLogs();
  }

  loadLogs() {
    this.api.getRecentAuditLogs(100).subscribe((logs: AuditLog[]) => {
      this.logs.set(logs);
    });
  }

  search() {
    const params: AuditSearchParams = {
      ...this.filters,
      from: this.fromDate ? new Date(this.fromDate) : undefined,
      to: this.toDate ? new Date(this.toDate) : undefined
    };
    this.api.searchAuditLogs(params).subscribe((logs: AuditLog[]) => {
      this.logs.set(logs);
    });
  }

  prevPage() {
    if (this.filters.page! > 1) {
      this.filters.page!--;
      this.search();
    }
  }

  nextPage() {
    this.filters.page!++;
    this.search();
  }

  formatDateTime(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  getActionLabel(action: string): string {
    const labels: Record<string, string> = {
      'Create': 'Tạo mới',
      'Update': 'Cập nhật',
      'Delete': 'Xóa'
    };
    return labels[action] || action;
  }
}
