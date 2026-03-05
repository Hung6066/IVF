import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TenantService } from '../../../core/services/tenant.service';
import { PricingConfigService } from '../../../core/services/pricing-config.service';
import {
  Tenant,
  UpdateTenantRequest,
  UpdateBrandingRequest,
  UpdateLimitsRequest,
  UpdateSubscriptionRequest,
  DataIsolationStrategy,
  UpdateIsolationRequest,
  TenantFeatureDto,
  TenantUsageAnalytics,
  UsageAlert,
  UsageDetailResult,
  UsageDetailItem,
  TenantUserDto,
  TenantApiCallsResult,
  ApiCallStats,
} from '../../../core/models/tenant.model';

@Component({
  selector: 'app-tenant-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tenant-detail.component.html',
  styleUrls: ['./tenant-detail.component.scss'],
})
export class TenantDetailComponent implements OnInit {
  Math = Math; // Expose Math for template
  tenant = signal<Tenant | null>(null);
  loading = signal(false);
  activeTab = signal<'info' | 'subscription' | 'usage' | 'branding' | 'limits' | 'isolation' | 'users' | 'apiCalls'>(
    'info',
  );
  saving = signal(false);

  // Dynamic features
  tenantFeatures = signal<TenantFeatureDto[]>([]);
  featuresLoading = signal(false);
  featuresSaving = signal(false);
  enabledFeatureCount = computed(() => this.tenantFeatures().filter((f) => f.isEnabled).length);

  // Usage analytics
  usageAnalytics = signal<TenantUsageAnalytics | null>(null);
  usageLoading = signal(false);
  usageRefreshing = signal(false);

  // Usage detail drill-down
  usageDetail = signal<UsageDetailResult | null>(null);
  usageDetailLoading = signal(false);
  usageDetailOpen = signal(false);

  // Tenant users management
  tenantUsers = signal<TenantUserDto[]>([]);
  tenantUsersTotal = signal(0);
  usersLoading = signal(false);
  usersPage = signal(1);
  usersSearch = signal('');
  usersRoleFilter = signal('');
  usersActiveFilter = signal<string>('');

  // Password reset modal
  resetModalOpen = signal(false);
  resetTargetUser = signal<TenantUserDto | null>(null);
  resetNewPassword = signal('');
  resetSaving = signal(false);

  // API calls
  apiCallsData = signal<TenantApiCallsResult | null>(null);
  apiCallsLoading = signal(false);
  apiCallsPage = signal(1);
  apiCallsMethodFilter = signal('');
  apiCallsStatusFilter = signal<string>('');

  editInfo: UpdateTenantRequest = {
    id: '',
    name: '',
    address: '',
    phone: '',
    email: '',
    website: '',
    taxId: '',
  };
  editBranding: UpdateBrandingRequest = { id: '', logoUrl: '', primaryColor: '', customDomain: '' };
  editLimits: UpdateLimitsRequest = {
    id: '',
    maxUsers: 10,
    maxPatientsPerMonth: 100,
    storageLimitMb: 5120,
    aiEnabled: false,
    digitalSigningEnabled: false,
    biometricsEnabled: false,
    advancedReportingEnabled: false,
  };
  editIsolation: UpdateIsolationRequest = { isolationStrategy: 'SharedDatabase' };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private tenantService: TenantService,
    private pricingConfigService: PricingConfigService,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadTenant(id);
  }

  loadTenant(id: string): void {
    this.loading.set(true);
    this.tenantService.getById(id).subscribe({
      next: (t) => {
        this.tenant.set(t);
        this.editInfo = {
          id: t.id,
          name: t.name,
          address: t.address,
          phone: t.phone,
          email: t.email,
          website: t.website,
          taxId: t.taxId,
        };
        this.editBranding = {
          id: t.id,
          logoUrl: t.logoUrl,
          primaryColor: t.primaryColor,
          customDomain: t.customDomain,
        };
        this.editLimits = {
          id: t.id,
          maxUsers: t.maxUsers,
          maxPatientsPerMonth: t.maxPatientsPerMonth,
          storageLimitMb: t.storageLimitMb,
          aiEnabled: t.aiEnabled,
          digitalSigningEnabled: t.digitalSigningEnabled,
          biometricsEnabled: t.biometricsEnabled,
          advancedReportingEnabled: t.advancedReportingEnabled,
        };
        this.editIsolation = { isolationStrategy: t.isolationStrategy || 'SharedDatabase' };
        this.loading.set(false);
        this.loadTenantFeatures(t.id);
        this.loadUsageAnalytics(t.id);
      },
      error: () => this.loading.set(false),
    });
  }

  loadTenantFeatures(tenantId: string): void {
    this.featuresLoading.set(true);
    this.pricingConfigService.getTenantFeatures(tenantId).subscribe({
      next: (features) => {
        this.tenantFeatures.set(features);
        this.featuresLoading.set(false);
      },
      error: () => this.featuresLoading.set(false),
    });
  }

  toggleFeature(tf: TenantFeatureDto): void {
    this.tenantFeatures.update((list) =>
      list.map((f) =>
        f.featureDefinitionId === tf.featureDefinitionId ? { ...f, isEnabled: !f.isEnabled } : f,
      ),
    );
  }

  saveFeatures(): void {
    const t = this.tenant();
    if (!t) return;
    this.featuresSaving.set(true);
    const updates = this.tenantFeatures().map((f) => ({
      featureDefinitionId: f.featureDefinitionId,
      isEnabled: f.isEnabled,
    }));
    this.pricingConfigService.updateTenantFeatures(t.id, updates).subscribe({
      next: () => {
        this.featuresSaving.set(false);
        this.loadTenantFeatures(t.id);
      },
      error: () => this.featuresSaving.set(false),
    });
  }

  saveInfo(): void {
    this.saving.set(true);
    this.tenantService.update(this.editInfo).subscribe({
      next: () => {
        this.loadTenant(this.editInfo.id);
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }

  saveBranding(): void {
    this.saving.set(true);
    this.tenantService.updateBranding(this.editBranding).subscribe({
      next: () => {
        this.loadTenant(this.editBranding.id);
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }

  saveLimits(): void {
    this.saving.set(true);
    this.tenantService.updateLimits(this.editLimits).subscribe({
      next: () => {
        this.loadTenant(this.editLimits.id);
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }

  activateTenant(): void {
    const t = this.tenant();
    if (!t) return;
    this.tenantService.activate(t.id).subscribe(() => this.loadTenant(t.id));
  }

  suspendTenant(): void {
    const t = this.tenant();
    if (!t) return;
    const reason = prompt('Lý do tạm ngưng?');
    if (reason !== null) {
      this.tenantService.suspend(t.id, reason).subscribe(() => this.loadTenant(t.id));
    }
  }

  cancelTenant(): void {
    const t = this.tenant();
    if (!t) return;
    if (confirm('Bạn có chắc muốn hủy trung tâm này? Hành động này không thể hoàn tác.')) {
      this.tenantService.cancel(t.id).subscribe(() => this.loadTenant(t.id));
    }
  }

  goBack(): void {
    this.router.navigate(['/admin/tenants']);
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

  getUsagePercent(used: number, max: number): number {
    if (max === 0) return 0;
    return Math.min(Math.round((used / max) * 100), 100);
  }

  saveIsolation(): void {
    const t = this.tenant();
    if (!t) return;
    this.saving.set(true);
    this.tenantService.updateIsolation(t.id, this.editIsolation).subscribe({
      next: () => {
        this.loadTenant(t.id);
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }

  getIsolationLabel(strategy: string): string {
    const map: Record<string, string> = {
      SharedDatabase: 'Dùng chung DB',
      SeparateSchema: 'Schema riêng',
      SeparateDatabase: 'Database riêng',
    };
    return map[strategy] || strategy;
  }

  getIsolationClass(strategy: string): string {
    return `isolation-${strategy}`;
  }

  loadUsageAnalytics(tenantId: string): void {
    this.usageLoading.set(true);
    this.tenantService.getUsageAnalytics(tenantId).subscribe({
      next: (analytics) => {
        this.usageAnalytics.set(analytics);
        this.usageLoading.set(false);
      },
      error: () => this.usageLoading.set(false),
    });
  }

  refreshUsage(): void {
    const t = this.tenant();
    if (!t) return;
    this.usageRefreshing.set(true);
    this.tenantService.refreshUsage(t.id).subscribe({
      next: () => {
        this.usageRefreshing.set(false);
        this.loadUsageAnalytics(t.id);
        this.loadTenant(t.id);
      },
      error: () => this.usageRefreshing.set(false),
    });
  }

  getAlertClass(alert: UsageAlert): string {
    return `alert-${alert.type}`;
  }

  getAlertIcon(alert: UsageAlert): string {
    switch (alert.type) {
      case 'critical': return 'fas fa-exclamation-circle';
      case 'warning': return 'fas fa-exclamation-triangle';
      default: return 'fas fa-info-circle';
    }
  }

  getProgressClass(percent: number): string {
    if (percent >= 100) return 'progress-critical';
    if (percent >= 90) return 'progress-warning';
    if (percent >= 80) return 'progress-caution';
    return 'progress-normal';
  }

  getMonthLabel(month: number): string {
    const months = ['', 'Th1', 'Th2', 'Th3', 'Th4', 'Th5', 'Th6', 'Th7', 'Th8', 'Th9', 'Th10', 'Th11', 'Th12'];
    return months[month] || '';
  }

  openUsageDetail(metric: string): void {
    const t = this.tenant();
    if (!t) return;
    const analytics = this.usageAnalytics();
    const year = analytics?.currentUsage?.year || new Date().getFullYear();
    const month = analytics?.currentUsage?.month || (new Date().getMonth() + 1);

    this.usageDetailLoading.set(true);
    this.usageDetailOpen.set(true);
    this.usageDetail.set(null);

    this.tenantService.getUsageDetail(t.id, metric, year, month).subscribe({
      next: (result) => {
        this.usageDetail.set(result);
        this.usageDetailLoading.set(false);
      },
      error: () => {
        this.usageDetailLoading.set(false);
      },
    });
  }

  closeUsageDetail(): void {
    this.usageDetailOpen.set(false);
    this.usageDetail.set(null);
  }

  getDetailColumns(metric: string): { key: string; label: string }[] {
    switch (metric) {
      case 'users':
        return [
          { key: 'name', label: 'Họ tên' },
          { key: 'description', label: 'Username' },
          { key: 'extra.role', label: 'Vai trò' },
          { key: 'status', label: 'Trạng thái' },
          { key: 'createdAt', label: 'Ngày tạo' },
        ];
      case 'patients':
        return [
          { key: 'name', label: 'Họ tên' },
          { key: 'description', label: 'Mã BN' },
          { key: 'extra.phone', label: 'SĐT' },
          { key: 'createdAt', label: 'Ngày đăng ký' },
        ];
      case 'cycles':
        return [
          { key: 'name', label: 'Mã chu kỳ' },
          { key: 'description', label: 'Phương pháp' },
          { key: 'extra.phase', label: 'Giai đoạn' },
          { key: 'status', label: 'Kết quả' },
          { key: 'createdAt', label: 'Ngày tạo' },
        ];
      case 'forms':
        return [
          { key: 'name', label: 'Biểu mẫu' },
          { key: 'description', label: 'Bệnh nhân' },
          { key: 'status', label: 'Trạng thái' },
          { key: 'createdAt', label: 'Ngày tạo' },
        ];
      case 'documents':
        return [
          { key: 'name', label: 'Tiêu đề' },
          { key: 'description', label: 'Tên file' },
          { key: 'status', label: 'Loại' },
          { key: 'extra.fileSize', label: 'Kích thước' },
          { key: 'extra.signedBy', label: 'Người ký' },
          { key: 'extra.signedAt', label: 'Ngày ký' },
        ];
      case 'storage':
        return [
          { key: 'name', label: 'Tiêu đề' },
          { key: 'description', label: 'Tên file' },
          { key: 'status', label: 'Loại' },
          { key: 'extra.fileSize', label: 'Kích thước' },
          { key: 'extra.patient', label: 'Bệnh nhân' },
          { key: 'createdAt', label: 'Ngày tạo' },
        ];
      default:
        return [
          { key: 'name', label: 'Tên' },
          { key: 'description', label: 'Mô tả' },
          { key: 'status', label: 'Trạng thái' },
          { key: 'createdAt', label: 'Ngày tạo' },
        ];
    }
  }

  getDetailCellValue(item: UsageDetailItem, key: string): string {
    if (key === 'createdAt') {
      return new Date(item.createdAt).toLocaleDateString('vi-VN');
    }
    if (key.startsWith('extra.')) {
      const extraKey = key.substring(6);
      return item.extra?.[extraKey] ?? '—';
    }
    return (item as any)[key] ?? '—';
  }

  // ── Tenant Users ──

  loadTenantUsers(): void {
    const t = this.tenant();
    if (!t) return;
    this.usersLoading.set(true);
    const isActive = this.usersActiveFilter() === '' ? undefined : this.usersActiveFilter() === 'true';
    this.tenantService.getTenantUsers(
      t.id, this.usersPage(), 20,
      this.usersSearch() || undefined,
      this.usersRoleFilter() || undefined,
      isActive,
    ).subscribe({
      next: (res) => {
        this.tenantUsers.set(res.items);
        this.tenantUsersTotal.set(res.totalCount);
        this.usersLoading.set(false);
      },
      error: () => this.usersLoading.set(false),
    });
  }

  onUsersSearch(): void {
    this.usersPage.set(1);
    this.loadTenantUsers();
  }

  onUsersPageChange(page: number): void {
    this.usersPage.set(page);
    this.loadTenantUsers();
  }

  get usersTotalPages(): number {
    return Math.max(1, Math.ceil(this.tenantUsersTotal() / 20));
  }

  openResetPasswordModal(user: TenantUserDto): void {
    this.resetTargetUser.set(user);
    this.resetNewPassword.set('');
    this.resetModalOpen.set(true);
  }

  closeResetPasswordModal(): void {
    this.resetModalOpen.set(false);
    this.resetTargetUser.set(null);
    this.resetNewPassword.set('');
  }

  confirmResetPassword(): void {
    const t = this.tenant();
    const user = this.resetTargetUser();
    const pw = this.resetNewPassword();
    if (!t || !user || !pw) return;
    this.resetSaving.set(true);
    this.tenantService.resetUserPassword(t.id, user.id, pw).subscribe({
      next: () => {
        this.resetSaving.set(false);
        this.closeResetPasswordModal();
      },
      error: () => this.resetSaving.set(false),
    });
  }

  getRoleLabel(role: string): string {
    const map: Record<string, string> = {
      Admin: 'Quản trị',
      Doctor: 'Bác sĩ',
      Nurse: 'Y tá',
      LabTech: 'KTV xét nghiệm',
      Embryologist: 'Phôi học',
      Receptionist: 'Tiếp tân',
      Cashier: 'Thu ngân',
      Pharmacist: 'Dược sĩ',
    };
    return map[role] || role;
  }

  // ── API Calls ──

  loadApiCalls(): void {
    const t = this.tenant();
    if (!t) return;
    this.apiCallsLoading.set(true);
    const statusCode = this.apiCallsStatusFilter() ? Number(this.apiCallsStatusFilter()) : undefined;
    this.tenantService.getTenantApiCalls(
      t.id, this.apiCallsPage(), 20,
      this.apiCallsMethodFilter() || undefined,
      statusCode,
    ).subscribe({
      next: (res) => {
        this.apiCallsData.set(res);
        this.apiCallsLoading.set(false);
      },
      error: () => this.apiCallsLoading.set(false),
    });
  }

  onApiCallsFilter(): void {
    this.apiCallsPage.set(1);
    this.loadApiCalls();
  }

  onApiCallsPageChange(page: number): void {
    this.apiCallsPage.set(page);
    this.loadApiCalls();
  }

  get apiCallsTotalPages(): number {
    const data = this.apiCallsData();
    if (!data) return 1;
    return Math.max(1, Math.ceil(data.totalCount / 20));
  }

  getStatusCodeClass(code: number): string {
    if (code >= 200 && code < 300) return 'status-2xx';
    if (code >= 300 && code < 400) return 'status-3xx';
    if (code >= 400 && code < 500) return 'status-4xx';
    return 'status-5xx';
  }

  getMethodClass(method: string): string {
    return `method-${method.toLowerCase()}`;
  }

  getSuccessRate(): number {
    const stats = this.apiCallsData()?.stats;
    if (!stats || stats.totalCalls === 0) return 0;
    return Math.round((stats.successCalls / stats.totalCalls) * 100);
  }
}
