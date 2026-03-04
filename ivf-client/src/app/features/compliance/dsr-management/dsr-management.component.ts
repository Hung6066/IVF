import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComplianceService } from '../../../core/services/compliance.service';
import { UserService } from '../../../core/services/user.service';
import {
  DataSubjectRequest,
  DsrDashboard,
  DsrType,
  DsrStatus,
} from '../../../core/models/compliance.model';

@Component({
  selector: 'app-dsr-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dsr-management.component.html',
  styleUrls: ['./dsr-management.component.scss'],
})
export class DsrManagementComponent implements OnInit {
  private complianceService = inject(ComplianceService);
  private userService = inject(UserService);

  dsrs = signal<DataSubjectRequest[]>([]);
  dashboard = signal<DsrDashboard | null>(null);
  totalCount = signal(0);
  loading = signal(true);
  selectedDsr = signal<DataSubjectRequest | null>(null);
  users = signal<{ id: string; username: string; fullName: string; role: string }[]>([]);
  userSearchQuery = '';

  // Filters
  filterStatus = '';
  filterType = '';
  filterOverdue = false;
  filterSearch = '';
  page = 1;
  pageSize = 20;

  get totalPages(): number {
    return Math.ceil(this.totalCount() / this.pageSize);
  }

  // Create Modal
  showCreateModal = false;
  createForm = {
    dataSubjectName: '',
    dataSubjectEmail: '',
    requestType: 'Access' as DsrType,
    patientId: '',
    description: '',
  };

  // Action Modal
  showActionModal = false;
  actionType = '';
  actionForm: any = {};

  dsrTypes: DsrType[] = [
    'Access',
    'Rectification',
    'Erasure',
    'Restriction',
    'Portability',
    'Objection',
  ];
  dsrStatuses: DsrStatus[] = [
    'Received',
    'IdentityVerified',
    'InProgress',
    'EscalatedToDpo',
    'Completed',
    'Rejected',
  ];

  ngOnInit() {
    this.loadDsrs();
    this.loadDashboard();
    this.loadUsers();
  }

  loadUsers(search?: string) {
    this.userService.getUsers(search, undefined, undefined, 1, 100).subscribe({
      next: (result: any) => {
        const items = result?.items ?? result?.Items ?? [];
        this.users.set(items);
      },
      error: (err: any) => console.error('Failed to load users:', err),
    });
  }

  onUserSearch(query: string) {
    this.userSearchQuery = query;
    this.loadUsers(query || undefined);
  }

  getUserDisplay(userId: string): string {
    const user = this.users().find((u) => u.id === userId);
    return user ? `${user.fullName} (${user.username})` : userId;
  }

  loadDsrs() {
    this.loading.set(true);
    this.complianceService
      .getDsrList({
        status: this.filterStatus || undefined,
        requestType: this.filterType || undefined,
        overdue: this.filterOverdue || undefined,
        page: this.page,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.dsrs.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  loadDashboard() {
    this.complianceService.getDsrDashboard().subscribe({
      next: (data) => this.dashboard.set(data),
    });
  }

  onFilterChange() {
    this.page = 1;
    this.loadDsrs();
  }

  nextPage() {
    if (this.page * this.pageSize < this.totalCount()) {
      this.page++;
      this.loadDsrs();
    }
  }

  prevPage() {
    if (this.page > 1) {
      this.page--;
      this.loadDsrs();
    }
  }

  openCreateModal() {
    this.createForm = {
      dataSubjectName: '',
      dataSubjectEmail: '',
      requestType: 'Access',
      patientId: '',
      description: '',
    };
    this.showCreateModal = true;
  }

  submitCreate() {
    if (!this.createForm.dataSubjectName || !this.createForm.dataSubjectEmail) return;
    this.complianceService
      .createDsr({
        dataSubjectName: this.createForm.dataSubjectName,
        dataSubjectEmail: this.createForm.dataSubjectEmail,
        requestType: this.createForm.requestType,
        patientId: this.createForm.patientId || undefined,
        description: this.createForm.description || undefined,
      })
      .subscribe({
        next: () => {
          this.showCreateModal = false;
          this.loadDsrs();
          this.loadDashboard();
        },
      });
  }

  selectDsr(dsr: DataSubjectRequest) {
    this.complianceService.getDsr(dsr.id).subscribe({
      next: (data) => this.selectedDsr.set(data),
    });
  }

  closeDetail() {
    this.selectedDsr.set(null);
  }

  openAction(type: string) {
    this.actionType = type;
    this.actionForm = {};
    this.userSearchQuery = '';
    if (this.users().length === 0) this.loadUsers();
    this.showActionModal = true;
  }

  submitAction() {
    const dsr = this.selectedDsr();
    if (!dsr) return;

    const actions: Record<string, () => void> = {
      verify: () =>
        this.complianceService
          .verifyDsrIdentity(
            dsr.id,
            this.actionForm.method || 'ID Document',
            this.actionForm.verifiedBy || 'admin',
          )
          .subscribe(() => this.refreshDsr(dsr.id)),
      assign: () =>
        this.complianceService
          .assignDsr(dsr.id, this.actionForm.assignedTo)
          .subscribe(() => this.refreshDsr(dsr.id)),
      extend: () =>
        this.complianceService
          .extendDsrDeadline(
            dsr.id,
            this.actionForm.additionalDays || 30,
            this.actionForm.reason || '',
          )
          .subscribe(() => this.refreshDsr(dsr.id)),
      complete: () =>
        this.complianceService
          .completeDsr(dsr.id, this.actionForm.responseSummary)
          .subscribe(() => this.refreshDsr(dsr.id)),
      reject: () =>
        this.complianceService
          .rejectDsr(dsr.id, this.actionForm.rejectionReason, this.actionForm.legalBasis)
          .subscribe(() => this.refreshDsr(dsr.id)),
      escalate: () =>
        this.complianceService.escalateDsr(dsr.id).subscribe(() => this.refreshDsr(dsr.id)),
      notify: () =>
        this.complianceService.notifyDsrSubject(dsr.id).subscribe(() => this.refreshDsr(dsr.id)),
      note: () =>
        this.complianceService
          .addDsrNote(dsr.id, this.actionForm.note)
          .subscribe(() => this.refreshDsr(dsr.id)),
    };

    actions[this.actionType]?.();
    this.showActionModal = false;
  }

  refreshDsr(id: string) {
    this.complianceService.getDsr(id).subscribe({
      next: (data) => {
        this.selectedDsr.set(data);
        this.loadDsrs();
        this.loadDashboard();
      },
    });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Received: 'Đã nhận',
      IdentityVerified: 'Đã xác minh',
      InProgress: 'Đang xử lý',
      EscalatedToDpo: 'Chuyển DPO',
      Completed: 'Hoàn thành',
      Rejected: 'Từ chối',
    };
    return labels[status] || status;
  }

  getTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      Access: 'Truy cập',
      Rectification: 'Chỉnh sửa',
      Erasure: 'Xoá',
      Restriction: 'Hạn chế',
      Portability: 'Di chuyển',
      Objection: 'Phản đối',
    };
    return labels[type] || type;
  }

  getTypeIcon(type: string): string {
    const icons: Record<string, string> = {
      Access: '👁️',
      Rectification: '✏️',
      Erasure: '🗑️',
      Restriction: '🔒',
      Portability: '📦',
      Objection: '✋',
    };
    return icons[type] || '📝';
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      Received: 'blue',
      IdentityVerified: 'indigo',
      InProgress: 'amber',
      EscalatedToDpo: 'orange',
      Completed: 'green',
      Rejected: 'red',
    };
    return colors[status] || 'gray';
  }

  getActionTitle(): string {
    const titles: Record<string, string> = {
      verify: 'Xác minh danh tính',
      assign: 'Giao cho nhân viên',
      extend: 'Gia hạn thời hạn',
      complete: 'Hoàn thành yêu cầu',
      reject: 'Từ chối yêu cầu',
      escalate: 'Chuyển cho DPO',
      notify: 'Thông báo chủ thể',
      note: 'Thêm ghi chú',
    };
    return titles[this.actionType] || '';
  }
}
