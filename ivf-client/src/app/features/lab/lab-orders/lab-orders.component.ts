import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LabOrderService } from '../../../core/services/lab-order.service';
import { LabOrderDto, LabOrderStatistics } from '../../../core/models/lab-order.models';
import { LabResultEntryComponent } from '../lab-result-entry/lab-result-entry.component';
import { LabResultViewComponent } from '../lab-result-view/lab-result-view.component';

@Component({
  selector: 'app-lab-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, LabResultEntryComponent, LabResultViewComponent],
  templateUrl: './lab-orders.component.html',
  styleUrls: ['./lab-orders.component.scss'],
})
export class LabOrdersComponent implements OnInit {
  private labOrderService = inject(LabOrderService);

  orders = signal<LabOrderDto[]>([]);
  statistics = signal<LabOrderStatistics>({
    orderedCount: 0,
    inProgressCount: 0,
    completedCount: 0,
    deliveredCount: 0,
  });
  total = signal(0);
  loading = signal(false);

  searchQuery = '';
  statusFilter = '';
  orderTypeFilter = '';
  page = 1;
  pageSize = 20;

  selectedOrder = signal<LabOrderDto | null>(null);
  showResultEntry = false;
  showResultView = false;

  ngOnInit(): void {
    this.loadOrders();
    this.loadStatistics();
  }

  loadOrders(): void {
    this.loading.set(true);
    this.labOrderService
      .search(
        this.searchQuery || undefined,
        this.statusFilter || undefined,
        this.orderTypeFilter || undefined,
        undefined,
        undefined,
        this.page,
        this.pageSize,
      )
      .subscribe({
        next: (result) => {
          this.orders.set(result.items);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  loadStatistics(): void {
    this.labOrderService.getStatistics().subscribe((stats) => this.statistics.set(stats));
  }

  onSearch(): void {
    this.page = 1;
    this.loadOrders();
  }

  onPageChange(newPage: number): void {
    this.page = newPage;
    this.loadOrders();
  }

  collectSample(order: LabOrderDto): void {
    this.labOrderService.collectSample(order.id).subscribe({
      next: () => {
        this.loadOrders();
        this.loadStatistics();
      },
      error: (err) => alert('Lỗi: ' + (err.error?.detail || err.message)),
    });
  }

  selectOrder(order: LabOrderDto): void {
    this.selectedOrder.set(order);
    // If the order has results (Completed/Delivered), show read-only view; otherwise show entry form
    if (order.status === 'Completed' || order.status === 'Delivered') {
      this.showResultView = true;
    } else {
      this.showResultEntry = true;
    }
  }

  openResultEntry(order: LabOrderDto): void {
    this.selectedOrder.set(order);
    this.showResultEntry = true;
  }

  openResultView(order: LabOrderDto): void {
    this.selectedOrder.set(order);
    this.showResultView = true;
  }

  closeResultEntry(): void {
    this.showResultEntry = false;
    this.selectedOrder.set(null);
  }

  closeResultView(): void {
    this.showResultView = false;
    this.selectedOrder.set(null);
  }

  onResultSaved(): void {
    this.showResultEntry = false;
    this.selectedOrder.set(null);
    this.loadOrders();
    this.loadStatistics();
  }

  deliverResults(order: LabOrderDto): void {
    const deliveredTo = prompt('Trả kết quả cho (Patient/Doctor/Nurse):');
    if (!deliveredTo) return;
    this.labOrderService
      .deliverResults(order.id, {
        deliveredByUserId: '', // Will be set by server from auth context
        deliveredTo,
      })
      .subscribe({
        next: () => {
          this.loadOrders();
          this.loadStatistics();
        },
        error: (err) => alert('Lỗi: ' + (err.error?.detail || err.message)),
      });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Ordered: 'Đã chỉ định',
      SampleCollected: 'Đã lấy mẫu',
      InProgress: 'Đang thực hiện',
      Completed: 'Hoàn thành',
      Delivered: 'Đã trả KQ',
    };
    return labels[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Ordered: 'text-yellow-600 bg-yellow-100',
      SampleCollected: 'text-blue-600 bg-blue-100',
      InProgress: 'text-indigo-600 bg-indigo-100',
      Completed: 'text-green-600 bg-green-100',
      Delivered: 'text-emerald-600 bg-emerald-100',
    };
    return classes[status] || 'text-gray-600 bg-gray-100';
  }

  getOrderTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      ROUTINE: 'Thường quy',
      HORMONAL: 'Nội tiết',
      PRE_ANESTHESIA: 'Tiền mê',
      BETA_HCG: 'Beta HCG',
      HIV_SCREENING: 'HIV',
      BLOOD_TYPE: 'Nhóm máu',
    };
    return labels[type] || type;
  }

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize);
  }
}
