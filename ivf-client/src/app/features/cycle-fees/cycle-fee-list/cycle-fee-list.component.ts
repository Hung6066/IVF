import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CycleFeeService, CreateCycleFeeRequest } from '../../../core/services/cycle-fee.service';
import { CycleFeeDto } from '../../../core/models/clinical-management.models';

@Component({
  selector: 'app-cycle-fee-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './cycle-fee-list.component.html',
  styleUrls: ['./cycle-fee-list.component.scss'],
})
export class CycleFeeListComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private cycleFeeService = inject(CycleFeeService);

  cycleId = signal('');
  fees = signal<CycleFeeDto[]>([]);
  loading = signal(false);
  error = signal('');

  // Create modal
  showCreate = signal(false);
  saving = signal(false);
  createForm: CreateCycleFeeRequest = {
    cycleId: '',
    patientId: '',
    feeType: '',
    description: '',
    amount: 0,
    isOneTimePerCycle: false,
  };

  // Waive modal
  showWaive = signal(false);
  waiveTargetId = signal('');
  waiveReason = '';
  waiveUserId = '';
  waiving = signal(false);

  readonly feeTypes = [
    'Consultation',
    'Procedure',
    'Medication',
    'Laboratory',
    'Ultrasound',
    'Embryo Storage',
    'Sperm Banking',
    'Other',
  ];

  readonly statusLabels: Record<string, string> = {
    Pending: 'Chờ thanh toán',
    Paid: 'Đã thanh toán',
    PartiallyPaid: 'Thanh toán một phần',
    Waived: 'Miễn giảm',
    Refunded: 'Hoàn tiền',
    Cancelled: 'Đã huỷ',
  };

  readonly statusClasses: Record<string, string> = {
    Pending: 'bg-yellow-100 text-yellow-800',
    Paid: 'bg-green-100 text-green-800',
    PartiallyPaid: 'bg-blue-100 text-blue-800',
    Waived: 'bg-gray-100 text-gray-700',
    Refunded: 'bg-purple-100 text-purple-800',
    Cancelled: 'bg-red-100 text-red-800',
  };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('cycleId') ?? '';
    this.cycleId.set(id);
    this.createForm.cycleId = id;
    this.load();
  }

  load(): void {
    const id = this.cycleId();
    if (!id) return;
    this.loading.set(true);
    this.error.set('');
    this.cycleFeeService.getByCycle(id).subscribe({
      next: (list) => {
        this.fees.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Không thể tải danh sách phí chu kỳ.');
        this.loading.set(false);
      },
    });
  }

  get totalAmount(): number {
    return this.fees().reduce((s, f) => s + f.amount, 0);
  }

  get totalPaid(): number {
    return this.fees().reduce((s, f) => s + f.paidAmount, 0);
  }

  get totalBalance(): number {
    return this.fees().reduce((s, f) => s + f.balanceDue, 0);
  }

  openCreate(): void {
    this.createForm = {
      cycleId: this.cycleId(),
      patientId: '',
      feeType: '',
      description: '',
      amount: 0,
      isOneTimePerCycle: false,
    };
    this.showCreate.set(true);
  }

  create(): void {
    if (!this.createForm.feeType || !this.createForm.description || this.createForm.amount <= 0)
      return;
    this.saving.set(true);
    this.cycleFeeService.create(this.createForm).subscribe({
      next: () => {
        this.showCreate.set(false);
        this.saving.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Không thể thêm phí. Vui lòng thử lại.');
      },
    });
  }

  openWaive(fee: CycleFeeDto): void {
    this.waiveTargetId.set(fee.id);
    this.waiveReason = '';
    this.waiveUserId = '';
    this.showWaive.set(true);
  }

  confirmWaive(): void {
    if (!this.waiveReason.trim() || !this.waiveUserId.trim()) return;
    this.waiving.set(true);
    this.cycleFeeService.waive(this.waiveTargetId(), this.waiveReason, this.waiveUserId).subscribe({
      next: () => {
        this.showWaive.set(false);
        this.waiving.set(false);
        this.load();
      },
      error: () => {
        this.waiving.set(false);
        this.error.set('Không thể miễn giảm phí.');
      },
    });
  }

  refund(fee: CycleFeeDto): void {
    if (!confirm(`Xác nhận hoàn tiền cho: ${fee.description}?`)) return;
    this.cycleFeeService.refund(fee.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Không thể hoàn tiền.'),
    });
  }

  statusLabel(status: string): string {
    return this.statusLabels[status] ?? status;
  }

  statusClass(status: string): string {
    return this.statusClasses[status] ?? 'bg-gray-100 text-gray-700';
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
  }
}
