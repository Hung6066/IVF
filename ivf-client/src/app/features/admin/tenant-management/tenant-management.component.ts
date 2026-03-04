import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TenantService } from '../../../core/services/tenant.service';
import {
  TenantListItem,
  TenantPlatformStats,
  CreateTenantRequest,
  TenantStatus,
  SubscriptionPlan,
  BillingCycle,
  DataIsolationStrategy,
} from '../../../core/models/tenant.model';

@Component({
  selector: 'app-tenant-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tenant-management.component.html',
  styleUrls: ['./tenant-management.component.scss'],
})
export class TenantManagementComponent implements OnInit {
  tenants = signal<TenantListItem[]>([]);
  stats = signal<TenantPlatformStats | null>(null);
  totalCount = signal(0);
  currentPage = signal(1);
  pageSize = signal(20);
  searchQuery = signal('');
  statusFilter = signal<string>('');
  loading = signal(false);
  showCreateModal = signal(false);
  activeTab = signal<'overview' | 'tenants'>('overview');

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  newTenant: CreateTenantRequest = {
    name: '',
    slug: '',
    email: '',
    phone: '',
    address: '',
    plan: 'Starter',
    billingCycle: 'Monthly',
    isolationStrategy: 'SharedDatabase',
    adminUsername: '',
    adminPassword: '',
    adminFullName: '',
  };

  constructor(
    private tenantService: TenantService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadStats();
    this.loadTenants();
  }

  loadStats(): void {
    this.tenantService.getStats().subscribe({
      next: (data) => this.stats.set(data),
      error: () => console.error('Failed to load stats'),
    });
  }

  loadTenants(): void {
    this.loading.set(true);
    this.tenantService
      .getAll(
        this.currentPage(),
        this.pageSize(),
        this.searchQuery() || undefined,
        this.statusFilter() || undefined,
      )
      .subscribe({
        next: (res) => {
          this.tenants.set(res.items);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onSearch(): void {
    this.currentPage.set(1);
    this.loadTenants();
  }

  onStatusFilter(status: string): void {
    this.statusFilter.set(status);
    this.currentPage.set(1);
    this.loadTenants();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadTenants();
  }

  viewTenant(id: string): void {
    this.router.navigate(['/admin/tenants', id]);
  }

  openCreateModal(): void {
    this.newTenant = {
      name: '',
      slug: '',
      email: '',
      phone: '',
      address: '',
      plan: 'Starter',
      billingCycle: 'Monthly',
      isolationStrategy: 'SharedDatabase',
      adminUsername: '',
      adminPassword: '',
      adminFullName: '',
    };
    this.showCreateModal.set(true);
  }

  generateSlug(): void {
    this.newTenant.slug = this.newTenant.name
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd')
      .replace(/Đ/g, 'd')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '');
  }

  createTenant(): void {
    if (
      !this.newTenant.name ||
      !this.newTenant.slug ||
      !this.newTenant.adminUsername ||
      !this.newTenant.adminPassword
    )
      return;
    this.loading.set(true);
    this.tenantService.create(this.newTenant).subscribe({
      next: (res) => {
        this.showCreateModal.set(false);
        this.loadTenants();
        this.loadStats();
      },
      error: () => this.loading.set(false),
    });
  }

  activateTenant(id: string, event: Event): void {
    event.stopPropagation();
    this.tenantService.activate(id).subscribe(() => {
      this.loadTenants();
      this.loadStats();
    });
  }

  suspendTenant(id: string, event: Event): void {
    event.stopPropagation();
    const reason = prompt('Lý do tạm ngưng?');
    if (reason !== null) {
      this.tenantService.suspend(id, reason).subscribe(() => {
        this.loadTenants();
        this.loadStats();
      });
    }
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Active: 'status-active',
      Trial: 'status-trial',
      Suspended: 'status-suspended',
      Cancelled: 'status-cancelled',
      PendingSetup: 'status-pending',
    };
    return map[status] || '';
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Active: 'Hoạt động',
      Trial: 'Dùng thử',
      Suspended: 'Tạm ngưng',
      Cancelled: 'Đã hủy',
      PendingSetup: 'Chờ thiết lập',
    };
    return map[status] || status;
  }

  getPlanLabel(plan?: string): string {
    if (!plan) return 'N/A';
    const map: Record<string, string> = {
      Trial: 'Dùng thử',
      Starter: 'Cơ bản',
      Professional: 'Chuyên nghiệp',
      Enterprise: 'Doanh nghiệp',
      Custom: 'Tùy chỉnh',
    };
    return map[plan] || plan;
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  }

  formatStorage(mb: number): string {
    if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`;
    return `${mb} MB`;
  }

  getIsolationLabel(strategy?: string): string {
    const map: Record<string, string> = {
      SharedDatabase: 'Chung DB',
      SeparateSchema: 'Riêng Schema',
      SeparateDatabase: 'Riêng Database',
    };
    return map[strategy || 'SharedDatabase'] || strategy || 'Chung DB';
  }
}
