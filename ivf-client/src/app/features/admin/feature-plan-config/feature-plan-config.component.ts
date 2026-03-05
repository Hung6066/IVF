import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PricingConfigService } from '../../../core/services/pricing-config.service';
import { TenantService } from '../../../core/services/tenant.service';
import {
  FeatureDefinitionDto,
  PlanDefinitionDto,
  CreateFeatureRequest,
  UpdateFeatureRequest,
  CreatePlanRequest,
  UpdatePlanRequest,
  TenantListItem,
  TenantFeatureDto,
} from '../../../core/models/tenant.model';

@Component({
  selector: 'app-feature-plan-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './feature-plan-config.component.html',
  styleUrls: ['./feature-plan-config.component.scss'],
})
export class FeaturePlanConfigComponent implements OnInit {
  activeTab = signal<'features' | 'plans' | 'mapping' | 'tenant-features'>('features');
  loading = signal(false);

  // ── Features ──
  features = signal<FeatureDefinitionDto[]>([]);
  showFeatureModal = signal(false);
  editingFeature = signal<FeatureDefinitionDto | null>(null);
  featureForm: CreateFeatureRequest & { isActive?: boolean } = this.emptyFeatureForm();

  // ── Plans ──
  plans = signal<PlanDefinitionDto[]>([]);
  showPlanModal = signal(false);
  editingPlan = signal<PlanDefinitionDto | null>(null);
  planForm: CreatePlanRequest & { isActive?: boolean } = this.emptyPlanForm();

  // ── Plan-Feature Mapping ──
  selectedPlanId = signal<string>('');
  selectedPlan = computed(() => this.plans().find((p) => p.id === this.selectedPlanId()));
  planFeatureIds = signal<Set<string>>(new Set());

  // ── Tenant Feature Overrides ──
  tenants = signal<TenantListItem[]>([]);
  selectedTenantId = signal<string>('');
  tenantFeatures = signal<TenantFeatureDto[]>([]);
  tenantFeaturesLoading = signal(false);
  enabledTenantFeatureCount = computed(
    () => this.tenantFeatures().filter((f) => f.isEnabled).length,
  );

  categories = computed(() => {
    const cats = new Set(this.features().map((f) => f.category));
    return Array.from(cats).sort();
  });

  constructor(
    private pricingService: PricingConfigService,
    private tenantService: TenantService,
  ) {}

  ngOnInit(): void {
    this.loadFeatures();
    this.loadPlans();
    this.loadTenants();
  }

  // ═══════════════════════════════════════
  // Features CRUD
  // ═══════════════════════════════════════

  loadFeatures(): void {
    this.pricingService.getFeatures().subscribe({
      next: (data) => this.features.set(data),
    });
  }

  openCreateFeature(): void {
    this.editingFeature.set(null);
    this.featureForm = this.emptyFeatureForm();
    this.showFeatureModal.set(true);
  }

  openEditFeature(f: FeatureDefinitionDto): void {
    this.editingFeature.set(f);
    this.featureForm = {
      code: f.code,
      displayName: f.displayName,
      description: f.description,
      icon: f.icon,
      category: f.category,
      sortOrder: f.sortOrder,
      isActive: f.isActive,
    };
    this.showFeatureModal.set(true);
  }

  saveFeature(): void {
    if (!this.featureForm.code || !this.featureForm.displayName) return;
    this.loading.set(true);
    const editing = this.editingFeature();

    if (editing) {
      const req: UpdateFeatureRequest = {
        displayName: this.featureForm.displayName,
        description: this.featureForm.description,
        icon: this.featureForm.icon,
        category: this.featureForm.category,
        sortOrder: this.featureForm.sortOrder,
        isActive: this.featureForm.isActive ?? true,
      };
      this.pricingService.updateFeature(editing.id, req).subscribe({
        next: () => {
          this.showFeatureModal.set(false);
          this.loadFeatures();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else {
      const req: CreateFeatureRequest = {
        code: this.featureForm.code,
        displayName: this.featureForm.displayName,
        description: this.featureForm.description,
        icon: this.featureForm.icon,
        category: this.featureForm.category,
        sortOrder: this.featureForm.sortOrder,
      };
      this.pricingService.createFeature(req).subscribe({
        next: () => {
          this.showFeatureModal.set(false);
          this.loadFeatures();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  deleteFeature(f: FeatureDefinitionDto): void {
    if (!confirm(`Xóa tính năng "${f.displayName}"?`)) return;
    this.pricingService.deleteFeature(f.id).subscribe({
      next: () => this.loadFeatures(),
    });
  }

  // ═══════════════════════════════════════
  // Plans CRUD
  // ═══════════════════════════════════════

  loadPlans(): void {
    this.pricingService.getPlans().subscribe({
      next: (data) => this.plans.set(data),
    });
  }

  openCreatePlan(): void {
    this.editingPlan.set(null);
    this.planForm = this.emptyPlanForm();
    this.showPlanModal.set(true);
  }

  openEditPlan(p: PlanDefinitionDto): void {
    this.editingPlan.set(p);
    this.planForm = {
      plan: p.plan,
      displayName: p.displayName,
      description: p.description,
      monthlyPrice: p.monthlyPrice,
      currency: p.currency,
      duration: p.duration,
      maxUsers: p.maxUsers,
      maxPatientsPerMonth: p.maxPatientsPerMonth,
      storageLimitMb: p.storageLimitMb,
      sortOrder: p.sortOrder,
      isFeatured: p.isFeatured,
      isActive: p.isActive,
    };
    this.showPlanModal.set(true);
  }

  savePlan(): void {
    if (!this.planForm.plan || !this.planForm.displayName) return;
    this.loading.set(true);
    const editing = this.editingPlan();

    if (editing) {
      const req: UpdatePlanRequest = {
        displayName: this.planForm.displayName,
        description: this.planForm.description,
        monthlyPrice: this.planForm.monthlyPrice,
        duration: this.planForm.duration,
        maxUsers: this.planForm.maxUsers,
        maxPatientsPerMonth: this.planForm.maxPatientsPerMonth,
        storageLimitMb: this.planForm.storageLimitMb,
        sortOrder: this.planForm.sortOrder,
        isFeatured: this.planForm.isFeatured,
        isActive: this.planForm.isActive ?? true,
      };
      this.pricingService.updatePlan(editing.id, req).subscribe({
        next: () => {
          this.showPlanModal.set(false);
          this.loadPlans();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else {
      this.pricingService.createPlan(this.planForm).subscribe({
        next: () => {
          this.showPlanModal.set(false);
          this.loadPlans();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  deletePlan(p: PlanDefinitionDto): void {
    if (!confirm(`Xóa gói "${p.displayName}"?`)) return;
    this.pricingService.deletePlan(p.id).subscribe({
      next: () => this.loadPlans(),
    });
  }

  // ═══════════════════════════════════════
  // Plan-Feature Mapping
  // ═══════════════════════════════════════

  onPlanSelected(planId: string): void {
    this.selectedPlanId.set(planId);
    const plan = this.plans().find((p) => p.id === planId);
    if (plan) {
      this.planFeatureIds.set(new Set(plan.features.map((f) => f.featureDefinitionId)));
    } else {
      this.planFeatureIds.set(new Set());
    }
  }

  togglePlanFeature(featureId: string): void {
    const ids = new Set(this.planFeatureIds());
    if (ids.has(featureId)) {
      ids.delete(featureId);
    } else {
      ids.add(featureId);
    }
    this.planFeatureIds.set(ids);
  }

  isPlanFeatureSelected(featureId: string): boolean {
    return this.planFeatureIds().has(featureId);
  }

  savePlanFeatures(): void {
    const planId = this.selectedPlanId();
    if (!planId) return;
    this.loading.set(true);
    this.pricingService.updatePlanFeatures(planId, Array.from(this.planFeatureIds())).subscribe({
      next: () => {
        this.loadPlans();
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  // ═══════════════════════════════════════
  // Tenant Feature Overrides
  // ═══════════════════════════════════════

  loadTenants(): void {
    this.tenantService.getAll(1, 100).subscribe({
      next: (res) => this.tenants.set(res.items),
    });
  }

  onTenantSelected(tenantId: string): void {
    this.selectedTenantId.set(tenantId);
    if (!tenantId) {
      this.tenantFeatures.set([]);
      return;
    }
    this.tenantFeaturesLoading.set(true);
    this.pricingService.getTenantFeatures(tenantId).subscribe({
      next: (data) => {
        this.tenantFeatures.set(data);
        this.tenantFeaturesLoading.set(false);
      },
      error: () => this.tenantFeaturesLoading.set(false),
    });
  }

  toggleTenantFeature(tf: TenantFeatureDto): void {
    const updated = this.tenantFeatures().map((f) =>
      f.featureDefinitionId === tf.featureDefinitionId ? { ...f, isEnabled: !f.isEnabled } : f,
    );
    this.tenantFeatures.set(updated);
  }

  saveTenantFeatures(): void {
    const tenantId = this.selectedTenantId();
    if (!tenantId) return;
    this.loading.set(true);
    const updates = this.tenantFeatures().map((f) => ({
      featureDefinitionId: f.featureDefinitionId,
      isEnabled: f.isEnabled,
    }));
    this.pricingService.updateTenantFeatures(tenantId, updates).subscribe({
      next: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }

  // ═══════════════════════════════════════
  // Helpers
  // ═══════════════════════════════════════

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  }

  formatStorage(mb: number): string {
    if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`;
    return `${mb} MB`;
  }

  featuresByCategory(): { category: string; features: FeatureDefinitionDto[] }[] {
    const map = new Map<string, FeatureDefinitionDto[]>();
    for (const f of this.features()) {
      const list = map.get(f.category) || [];
      list.push(f);
      map.set(f.category, list);
    }
    return Array.from(map.entries())
      .map(([category, features]) => ({ category, features }))
      .sort((a, b) => a.category.localeCompare(b.category));
  }

  private emptyFeatureForm(): CreateFeatureRequest & { isActive?: boolean } {
    return {
      code: '',
      displayName: '',
      description: '',
      icon: 'fas fa-cog',
      category: '',
      sortOrder: 0,
    };
  }

  private emptyPlanForm(): CreatePlanRequest & { isActive?: boolean } {
    return {
      plan: '',
      displayName: '',
      description: '',
      monthlyPrice: 0,
      currency: 'VND',
      duration: '/tháng',
      maxUsers: 5,
      maxPatientsPerMonth: 100,
      storageLimitMb: 1024,
      sortOrder: 0,
      isFeatured: false,
    };
  }
}
