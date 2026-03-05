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
} from '../../../core/models/tenant.model';

@Component({
  selector: 'app-tenant-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tenant-detail.component.html',
  styleUrls: ['./tenant-detail.component.scss'],
})
export class TenantDetailComponent implements OnInit {
  tenant = signal<Tenant | null>(null);
  loading = signal(false);
  activeTab = signal<'info' | 'subscription' | 'usage' | 'branding' | 'limits' | 'isolation'>(
    'info',
  );
  saving = signal(false);

  // Dynamic features
  tenantFeatures = signal<TenantFeatureDto[]>([]);
  featuresLoading = signal(false);
  featuresSaving = signal(false);
  enabledFeatureCount = computed(() => this.tenantFeatures().filter((f) => f.isEnabled).length);

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
}
