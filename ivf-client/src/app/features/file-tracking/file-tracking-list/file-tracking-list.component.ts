import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  FileTrackingService,
  CreateFileTrackingRequest,
  TransferFileRequest,
} from '../../../core/services/file-tracking.service';
import { FileTrackingDto } from '../../../core/models/clinical-management.models';

@Component({
  selector: 'app-file-tracking-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './file-tracking-list.component.html',
  styleUrls: ['./file-tracking-list.component.scss'],
})
export class FileTrackingListComponent implements OnInit {
  private service = inject(FileTrackingService);

  loading = signal(false);
  saving = signal(false);
  error = signal('');
  items = signal<FileTrackingDto[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = 20;

  searchQuery = '';
  filterStatus = '';
  expandedId = signal<string | null>(null);
  showCreateForm = signal(false);
  showTransferForm = signal(false);
  transferTarget = signal<FileTrackingDto | null>(null);

  createForm: CreateFileTrackingRequest = {
    patientId: '',
    fileCode: '',
    currentLocation: '',
    notes: '',
  };
  transferForm: TransferFileRequest = { toLocation: '', transferredByUserId: '', reason: '' };

  statuses = ['InStorage', 'CheckedOut', 'InTransit', 'Lost', 'Archived'];
  statusLabels: Record<string, string> = {
    InStorage: 'Tại kho',
    CheckedOut: 'Đã mượn',
    InTransit: 'Đang luân chuyển',
    Lost: 'Mất',
    Archived: 'Lưu trữ',
  };
  statusClasses: Record<string, string> = {
    InStorage: 'bg-green-50 text-green-700',
    CheckedOut: 'bg-yellow-50 text-yellow-700',
    InTransit: 'bg-blue-50 text-blue-700',
    Lost: 'bg-red-50 text-red-700',
    Archived: 'bg-gray-100 text-gray-500',
  };

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service
      .search(
        this.searchQuery || undefined,
        this.filterStatus || undefined,
        undefined,
        this.page(),
        this.pageSize,
      )
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.totalCount);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  search() {
    this.page.set(1);
    this.load();
  }

  openCreate() {
    this.createForm = { patientId: '', fileCode: '', currentLocation: '', notes: '' };
    this.showCreateForm.set(true);
  }

  create() {
    this.saving.set(true);
    this.error.set('');
    this.service.create(this.createForm).subscribe({
      next: () => {
        this.saving.set(false);
        this.showCreateForm.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi tạo hồ sơ');
        this.saving.set(false);
      },
    });
  }

  openTransfer(item: FileTrackingDto) {
    this.transferTarget.set(item);
    this.transferForm = { toLocation: '', transferredByUserId: '', reason: '' };
    this.showTransferForm.set(true);
  }

  transfer() {
    const target = this.transferTarget();
    if (!target) return;
    this.saving.set(true);
    this.error.set('');
    this.service.transfer(target.id, this.transferForm).subscribe({
      next: () => {
        this.saving.set(false);
        this.showTransferForm.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi luân chuyển hồ sơ');
        this.saving.set(false);
      },
    });
  }

  markReceived(item: FileTrackingDto) {
    this.service.markReceived(item.id).subscribe({ next: () => this.load() });
  }

  markLost(item: FileTrackingDto) {
    if (!confirm(`Xác nhận đánh dấu mất hồ sơ ${item.fileCode}?`)) return;
    this.service.markLost(item.id).subscribe({ next: () => this.load() });
  }

  toggleExpand(id: string) {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }
  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize);
  }
  prevPage() {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.load();
    }
  }
  nextPage() {
    if (this.page() < this.totalPages) {
      this.page.update((p) => p + 1);
      this.load();
    }
  }
}
