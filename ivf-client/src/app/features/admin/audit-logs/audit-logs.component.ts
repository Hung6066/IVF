import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuditService } from '../../../core/services/audit.service';
import { AuditLog, AuditSearchParams } from '../../../core/models/api.models';

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './audit-logs.component.html',
  styleUrls: ['./audit-logs.component.scss']
})
export class AuditLogsComponent implements OnInit {
  logs = signal<AuditLog[]>([]);
  filters: AuditSearchParams = {
    page: 1,
    pageSize: 50
  };
  fromDate = '';
  toDate = '';

  constructor(private auditService: AuditService) { }

  ngOnInit() {
    this.loadLogs();
  }

  loadLogs() {
    this.auditService.getRecentAuditLogs(100).subscribe((logs: AuditLog[]) => {
      this.logs.set(logs);
    });
  }

  search() {
    const params: AuditSearchParams = {
      ...this.filters,
      from: this.fromDate ? new Date(this.fromDate) : undefined,
      to: this.toDate ? new Date(this.toDate) : undefined
    };
    this.auditService.searchAuditLogs(params).subscribe((logs: AuditLog[]) => {
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
