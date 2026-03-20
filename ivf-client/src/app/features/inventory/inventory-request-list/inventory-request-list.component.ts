import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { InventoryRequestService } from '../../../core/services/inventory-request.service';
import {
  InventoryRequestDto,
  InventoryRequestStatus,
} from '../../../core/models/clinical-management.models';

@Component({
  selector: 'app-inventory-request-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './inventory-request-list.component.html',
  styleUrls: ['./inventory-request-list.component.scss'],
})
export class InventoryRequestListComponent implements OnInit {
  private requestService = inject(InventoryRequestService);

  requests = signal<InventoryRequestDto[]>([]);
  loading = signal(false);
  statusFilter: InventoryRequestStatus | '' = '';
  rejectReason = '';
  rejectingId = signal<string | null>(null);

  ngOnInit() {
    this.loadRequests();
  }

  loadRequests() {
    this.loading.set(true);
    const status = this.statusFilter || undefined;
    this.requestService.search(undefined, status, undefined, 1, 50).subscribe({
      next: (res) => {
        this.requests.set(res.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  approve(id: string) {
    this.requestService.approve(id, '').subscribe(() => this.loadRequests());
  }

  openRejectDialog(id: string) {
    this.rejectingId.set(id);
    this.rejectReason = '';
  }

  confirmReject() {
    const id = this.rejectingId();
    if (!id || !this.rejectReason.trim()) return;
    this.requestService.reject(id, this.rejectReason).subscribe(() => {
      this.rejectingId.set(null);
      this.loadRequests();
    });
  }

  cancelReject() {
    this.rejectingId.set(null);
  }

  statusLabel(status: InventoryRequestStatus): string {
    const map: Record<InventoryRequestStatus, string> = {
      Pending: 'Chờ duyệt',
      Approved: 'Đã duyệt',
      Rejected: 'Từ chối',
      Fulfilled: 'Đã thực hiện',
      Cancelled: 'Đã hủy',
    };
    return map[status] ?? status;
  }

  statusClass(status: InventoryRequestStatus): string {
    const map: Record<InventoryRequestStatus, string> = {
      Pending: 'bg-yellow-100 text-yellow-800',
      Approved: 'bg-blue-100 text-blue-800',
      Rejected: 'bg-red-100 text-red-800',
      Fulfilled: 'bg-green-100 text-green-800',
      Cancelled: 'bg-gray-100 text-gray-800',
    };
    return map[status] ?? 'bg-gray-100 text-gray-800';
  }

  requestTypeLabel(type: string): string {
    const map: Record<string, string> = {
      Restock: 'Nhập thêm',
      Usage: 'Sử dụng',
      PurchaseOrder: 'Đặt mua',
      Return: 'Trả hàng',
    };
    return map[type] ?? type;
  }
}
