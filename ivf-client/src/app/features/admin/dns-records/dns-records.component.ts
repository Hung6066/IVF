import { Component, OnInit, signal, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { DnsManagementService } from '../../../core/services/dns-management.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import {
  DnsRecordTypeEnum,
  DnsListResponse,
  CreateDnsRecordRequest,
} from '../../../core/models/dns-record.model';

@Component({
  selector: 'app-dns-records',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dns-records.component.html',
  styleUrls: ['./dns-records.component.scss'],
})
export class DnsRecordsComponent implements OnInit {
  embedded = input(false);
  records = signal<DnsListResponse[]>([]);
  loading = signal(false);
  isCreating = signal(false);
  error = signal<string | null>(null);

  // Form fields
  formVisible = signal(false);
  recordType = signal<DnsRecordTypeEnum>(DnsRecordTypeEnum.A);
  name = signal('');
  content = signal('');
  ttlSeconds = signal(3600);

  // Record types for dropdown
  recordTypes = [
    DnsRecordTypeEnum.A,
    DnsRecordTypeEnum.AAAA,
    DnsRecordTypeEnum.CNAME,
    DnsRecordTypeEnum.MX,
    DnsRecordTypeEnum.TXT,
    DnsRecordTypeEnum.NS,
  ];

  // TTL options
  ttlOptions = [
    { label: 'Auto', value: 1 },
    { label: '5 phút', value: 300 },
    { label: '30 phút', value: 1800 },
    { label: '1 giờ', value: 3600 },
    { label: '6 giờ', value: 21600 },
    { label: '1 ngày', value: 86400 },
  ];

  constructor(
    private dnsService: DnsManagementService,
    private notificationService: GlobalNotificationService,
  ) {}

  ngOnInit(): void {
    this.loadRecords();
  }

  loadRecords(): void {
    this.loading.set(true);
    this.dnsService.getDnsRecords().subscribe({
      next: (data: DnsListResponse[]) => {
        this.records.set(data);
        this.error.set(null);
      },
      error: (err: HttpErrorResponse) => {
        console.error('Failed to load DNS records:', err);
        this.error.set('Không thể tải DNS records. Vui lòng thử lại.');
        this.notificationService.error('Lỗi', 'Không thể tải DNS records', 7000);
      },
      complete: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.formVisible.set(true);
    this.resetForm();
  }

  closeForm(): void {
    this.formVisible.set(false);
    this.resetForm();
  }

  resetForm(): void {
    this.recordType.set(DnsRecordTypeEnum.A);
    this.name.set('');
    this.content.set('');
    this.ttlSeconds.set(3600);
  }

  isFormValid(): boolean {
    const nameRegex = /^[a-zA-Z0-9.-]+$/;
    const name = this.name().trim();

    if (!name || !nameRegex.test(name)) {
      return false;
    }

    const ttl = this.ttlSeconds();
    if (ttl < 300 || ttl > 86400) {
      return false;
    }

    if (!this.content().trim()) {
      return false;
    }

    return true;
  }

  createRecord(): void {
    if (!this.isFormValid()) {
      this.notificationService.warning('Cảnh báo', 'Vui lòng kiểm tra lại dữ liệu nhập vào');
      return;
    }

    this.isCreating.set(true);

    const request: CreateDnsRecordRequest = {
      recordType: this.recordType(),
      name: this.name().trim(),
      content: this.content().trim(),
      ttlSeconds: this.ttlSeconds(),
    };

    this.dnsService.createRecord(request).subscribe({
      next: (response) => {
        this.notificationService.success('Thành công', `DNS record ${request.name} đã được tạo`);
        this.formVisible.set(false);
        this.resetForm();
        this.loadRecords();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Failed to create DNS record:', err);
        const errorMsg = err.error?.error || 'Không thể tạo DNS record';
        this.notificationService.error('Lỗi', errorMsg, 7000);
      },
      complete: () => this.isCreating.set(false),
    });
  }

  confirmDelete(id: string, name: string): void {
    if (
      !confirm(`Bạn có chắc chắn muốn xóa DNS record "${name}"? Hành động này không thể hoàn tác.`)
    ) {
      return;
    }

    this.deleteRecord(id);
  }

  deleteRecord(id: string): void {
    this.dnsService.deleteRecord(id).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'DNS record đã được xóa');
        this.loadRecords();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Failed to delete DNS record:', err);
        const errorMsg = err.error?.error || 'Không thể xóa DNS record';
        this.notificationService.error('Lỗi', errorMsg, 7000);
      },
    });
  }

  formatDate(dateStr: string): string {
    try {
      const date = new Date(dateStr);
      return date.toLocaleString('vi-VN');
    } catch {
      return dateStr;
    }
  }

  getRecordTypeLabelClass(type: string): string {
    const classes: { [key: string]: string } = {
      A: 'bg-blue-500',
      AAAA: 'bg-indigo-500',
      CNAME: 'bg-purple-500',
      MX: 'bg-orange-500',
      TXT: 'bg-green-500',
      NS: 'bg-red-500',
    };
    return classes[type] || 'bg-gray-500';
  }

  get recordCount(): number {
    return this.records().length;
  }
}
