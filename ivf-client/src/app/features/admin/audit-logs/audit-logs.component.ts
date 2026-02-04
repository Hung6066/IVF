import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, AuditLog, AuditSearchParams } from '../../../core/services/api.service';

@Component({
    selector: 'app-audit-logs',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="audit-container">
      <header class="page-header">
        <h1>📋 Nhật ký hoạt động</h1>
      </header>

      <!-- Filters -->
      <div class="filters">
        <div class="filter-group">
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
        <div class="filter-group">
          <label>Hành động</label>
          <select [(ngModel)]="filters.action" (change)="search()">
            <option value="">Tất cả</option>
            <option value="Create">Tạo mới</option>
            <option value="Update">Cập nhật</option>
            <option value="Delete">Xóa</option>
          </select>
        </div>
        <div class="filter-group">
          <label>Từ ngày</label>
          <input type="date" [(ngModel)]="fromDate" (change)="search()">
        </div>
        <div class="filter-group">
          <label>Đến ngày</label>
          <input type="date" [(ngModel)]="toDate" (change)="search()">
        </div>
        <button class="btn-primary" (click)="search()">Tìm kiếm</button>
      </div>

      <!-- Logs Table -->
      <div class="logs-table">
        <table>
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
                  <span class="action-badge" [class]="log.action.toLowerCase()">{{ getActionLabel(log.action) }}</span>
                </td>
                <td>
                  <span class="entity-type">{{ log.entityType }}</span>
                  <span class="entity-id">{{ log.entityId.substring(0, 8) }}...</span>
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
      </div>

      <!-- Pagination -->
      <div class="pagination">
        <button [disabled]="filters.page === 1" (click)="prevPage()">← Trước</button>
        <span>Trang {{ filters.page }}</span>
        <button [disabled]="logs().length < filters.pageSize!" (click)="nextPage()">Sau →</button>
      </div>
    </div>
  `,
    styles: [`
    .audit-container {
      padding: 24px;
      max-width: 1600px;
      margin: 0 auto;
    }
    
    .page-header h1 {
      font-size: 28px;
      font-weight: 700;
      color: #f1f5f9;
      margin: 0 0 24px;
    }
    
    .filters {
      display: flex;
      gap: 16px;
      align-items: flex-end;
      margin-bottom: 24px;
      flex-wrap: wrap;
      background: #1e293b;
      padding: 16px;
      border-radius: 12px;
    }
    
    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 8px;
      
      label {
        font-size: 12px;
        color: #94a3b8;
      }
      
      select, input {
        padding: 10px 12px;
        border: 1px solid #334155;
        border-radius: 8px;
        background: #0f172a;
        color: #f1f5f9;
        min-width: 150px;
        
        &:focus {
          outline: none;
          border-color: #60a5fa;
        }
      }
    }
    
    .btn-primary {
      background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
      color: white;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      cursor: pointer;
      height: fit-content;
      
      &:hover {
        transform: translateY(-1px);
      }
    }
    
    .logs-table {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 12px;
      overflow: hidden;
      
      table {
        width: 100%;
        border-collapse: collapse;
      }
      
      th, td {
        padding: 12px 16px;
        text-align: left;
        border-bottom: 1px solid #334155;
      }
      
      th {
        background: #0f172a;
        font-weight: 500;
        color: #94a3b8;
        font-size: 12px;
        text-transform: uppercase;
      }
      
      td {
        color: #f1f5f9;
        font-size: 14px;
      }
      
      tr:hover td {
        background: rgba(255, 255, 255, 0.02);
      }
    }
    
    .time-cell {
      font-family: monospace;
      font-size: 13px;
      color: #94a3b8 !important;
    }
    
    .action-badge {
      display: inline-block;
      padding: 4px 10px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 500;
      
      &.create { background: rgba(34, 197, 94, 0.2); color: #4ade80; }
      &.update { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
      &.delete { background: rgba(239, 68, 68, 0.2); color: #f87171; }
    }
    
    .entity-type {
      display: block;
      font-weight: 500;
    }
    
    .entity-id {
      font-size: 11px;
      color: #64748b;
      font-family: monospace;
    }
    
    .changes-cell {
      max-width: 200px;
      
      .changed-columns {
        font-size: 12px;
        color: #94a3b8;
        word-break: break-all;
      }
      
      .new-record {
        color: #4ade80;
        font-size: 12px;
      }
      
      .deleted-record {
        color: #f87171;
        font-size: 12px;
      }
    }
    
    .ip-cell {
      font-family: monospace;
      font-size: 12px;
      color: #64748b !important;
    }
    
    .empty-state {
      text-align: center;
      color: #64748b !important;
      padding: 40px !important;
    }
    
    .pagination {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 16px;
      margin-top: 24px;
      
      button {
        padding: 8px 16px;
        background: #334155;
        border: none;
        border-radius: 6px;
        color: #f1f5f9;
        cursor: pointer;
        
        &:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }
        
        &:not(:disabled):hover {
          background: #475569;
        }
      }
      
      span {
        color: #94a3b8;
      }
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
        this.api.getRecentAuditLogs(100).subscribe(logs => {
            this.logs.set(logs);
        });
    }

    search() {
        const params: AuditSearchParams = {
            ...this.filters,
            from: this.fromDate ? new Date(this.fromDate) : undefined,
            to: this.toDate ? new Date(this.toDate) : undefined
        };
        this.api.searchAuditLogs(params).subscribe(logs => {
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
