import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TenantCaService } from '../../../core/services/tenant-ca.service';
import { SigningAdminService } from '../../../core/services/signing-admin.service';
import { UserService } from '../../../core/services/user.service';
import {
  TenantSubCaStatusDto,
  ProvisionTenantCaRequest,
  TenantCaConfigRequest,
  TenantUserCertProvisionResponse,
  AvailableTenantDto,
  TENANT_CA_STATUSES,
  EjbcaCA,
  EjbcaProfile,
  TenantWorker,
  TenantEnrolledUser,
} from '../../../core/models/api.models';

@Component({
  selector: 'app-tenant-ca',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tenant-ca.component.html',
  styleUrls: ['./tenant-ca.component.scss'],
})
export class TenantCaComponent implements OnInit {
  readonly caStatuses = TENANT_CA_STATUSES;

  // List state
  tenantCAs = signal<TenantSubCaStatusDto[]>([]);
  loading = signal(false);

  // Computed stats
  activeCount = computed(() => this.tenantCAs().filter(t => t.subCaStatus === 'Active').length);
  suspendedCount = computed(() => this.tenantCAs().filter(t => t.subCaStatus === 'Suspended').length);
  revokedCount = computed(() => this.tenantCAs().filter(t => t.subCaStatus === 'Revoked').length);

  // Detail/edit state
  selectedTenant = signal<TenantSubCaStatusDto | null>(null);
  activeTab = 'list';

  // Available tenants (for creating new Sub-CA)
  availableTenants = signal<AvailableTenantDto[]>([]);

  // Provision form (create new Sub-CA)
  showProvisionModal = signal(false);
  provisionTenantId = '';
  provisionRequest: ProvisionTenantCaRequest = {};
  provisioning = signal(false);
  provisionResult = signal<{ success: boolean; message: string } | null>(null);

  // Config form
  showConfigModal = signal(false);
  configRequest: TenantCaConfigRequest = {};
  updatingConfig = signal(false);
  configResult = signal<{ success: boolean; message?: string } | null>(null);

  // Suspend/Revoke/Delete confirmation
  confirmAction = signal<{
    type: 'suspend' | 'revoke' | 'delete';
    tenantId: string;
    tenantName: string;
  } | null>(null);
  actionLoading = signal(false);
  actionResult = signal<{ success: boolean; message: string } | null>(null);

  // User cert provision
  showUserCertModal = signal(false);
  userCertTenantId = '';
  userCertUserId = '';
  userSearchQuery = '';
  userSearchResults = signal<any[]>([]);
  searchingUsers = signal(false);
  provisioningUserCert = signal(false);
  userCertResult = signal<TenantUserCertProvisionResponse | null>(null);

  // EJBCA reference data
  ejbcaCAs = signal<EjbcaCA[]>([]);
  ejbcaCertProfiles = signal<EjbcaProfile[]>([]);
  ejbcaEeProfiles = signal<EjbcaProfile[]>([]);

  // Tenant workers & enrolled users
  tenantWorkers = signal<TenantWorker[]>([]);
  enrolledUsers = signal<TenantEnrolledUser[]>([]);
  loadingWorkers = signal(false);
  loadingEnrolledUsers = signal(false);

  constructor(
    private tenantCaService: TenantCaService,
    private signingService: SigningAdminService,
    private userService: UserService,
  ) {}

  ngOnInit() {
    this.loadTenantCAs();
    this.loadEjbcaReferenceData();
  }

  // ─── List ───────────────────────────────────────────────

  loadTenantCAs() {
    this.loading.set(true);
    this.tenantCaService.listTenantCAs().subscribe({
      next: (data) => {
        this.tenantCAs.set(data.items || []);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load tenant CAs:', err);
        this.loading.set(false);
      },
    });
  }

  loadEjbcaReferenceData() {
    this.signingService.getEjbcaCAs().subscribe({
      next: (data) =>
        this.ejbcaCAs.set(
          Array.isArray(data)
            ? data
            : data?.certificate_authorities || [],
        ),
      error: () => {},
    });
    this.signingService.getEjbcaCertificateProfiles().subscribe({
      next: (data) =>
        this.ejbcaCertProfiles.set(
          Array.isArray(data)
            ? data
            : data?.certificate_profiles || [],
        ),
      error: () => {},
    });
    this.signingService.getEjbcaEndEntityProfiles().subscribe({
      next: (data) =>
        this.ejbcaEeProfiles.set(
          Array.isArray(data)
            ? data
            : data?.end_entity_profiles || [],
        ),
      error: () => {},
    });
  }

  loadAvailableTenants() {
    this.tenantCaService.listAvailableTenants().subscribe({
      next: (tenants) => this.availableTenants.set(tenants),
      error: () => this.availableTenants.set([]),
    });
  }

  loadTenantWorkers(tenantId: string) {
    this.loadingWorkers.set(true);
    this.tenantCaService.getTenantWorkers(tenantId).subscribe({
      next: (data) => {
        this.tenantWorkers.set(data.workers || []);
        this.loadingWorkers.set(false);
      },
      error: () => {
        this.tenantWorkers.set([]);
        this.loadingWorkers.set(false);
      },
    });
  }

  loadEnrolledUsers(tenantId: string) {
    this.loadingEnrolledUsers.set(true);
    this.tenantCaService.getEnrolledUsers(tenantId).subscribe({
      next: (data) => {
        this.enrolledUsers.set(data.items || []);
        this.loadingEnrolledUsers.set(false);
      },
      error: () => {
        this.enrolledUsers.set([]);
        this.loadingEnrolledUsers.set(false);
      },
    });
  }

  viewDetail(tenant: TenantSubCaStatusDto) {
    this.selectedTenant.set(tenant);
    this.activeTab = 'detail';
    this.loadTenantWorkers(tenant.tenantId);
    this.loadEnrolledUsers(tenant.tenantId);
  }

  backToList() {
    this.selectedTenant.set(null);
    this.activeTab = 'list';
    this.loadTenantCAs();
  }

  refreshDetail(tenantId: string) {
    this.tenantCaService.getTenantCA(tenantId).subscribe({
      next: (data) => this.selectedTenant.set(data),
      error: (err) => console.error('Failed to refresh tenant CA detail:', err),
    });
    this.loadTenantWorkers(tenantId);
    this.loadEnrolledUsers(tenantId);
  }

  // ─── Provision (Create new Sub-CA) ──────────────────────

  openProvisionModal(tenantId?: string) {
    this.provisionTenantId = tenantId || '';
    this.provisionRequest = {};
    this.provisionResult.set(null);
    this.loadAvailableTenants();
    this.showProvisionModal.set(true);
  }

  closeProvisionModal() {
    this.showProvisionModal.set(false);
    this.provisionResult.set(null);
  }

  provision() {
    if (!this.provisionTenantId) return;
    this.provisioning.set(true);
    this.provisionResult.set(null);
    this.tenantCaService.provisionTenantCA(this.provisionTenantId, this.provisionRequest).subscribe({
      next: (result) => {
        this.provisionResult.set({ success: result.success, message: result.message });
        this.provisioning.set(false);
        if (result.success) {
          if (result.status) this.selectedTenant.set(result.status);
          setTimeout(() => {
            this.closeProvisionModal();
            this.loadTenantCAs();
          }, 1500);
        }
      },
      error: (err) => {
        this.provisionResult.set({
          success: false,
          message: err.error?.error || err.error?.message || err.message,
        });
        this.provisioning.set(false);
      },
    });
  }

  // ─── Config ─────────────────────────────────────────────

  openConfigModal(tenant: TenantSubCaStatusDto) {
    this.configRequest = {
      defaultCertValidityDays: tenant.defaultCertValidityDays,
      renewBeforeDays: tenant.renewBeforeDays,
      maxWorkers: tenant.maxWorkers,
      autoProvisionEnabled: tenant.autoProvisionEnabled,
      ejbcaCaName: tenant.ejbcaCaName,
      ejbcaCertProfileName: tenant.ejbcaCertProfileName,
      ejbcaEeProfileName: tenant.ejbcaEeProfileName,
    };
    this.configResult.set(null);
    this.showConfigModal.set(true);
  }

  closeConfigModal() {
    this.showConfigModal.set(false);
    this.configResult.set(null);
  }

  updateConfig() {
    const tenant = this.selectedTenant();
    if (!tenant) return;

    this.updatingConfig.set(true);
    this.configResult.set(null);
    this.tenantCaService.updateConfig(tenant.tenantId, this.configRequest).subscribe({
      next: (result) => {
        this.configResult.set({ success: result.success });
        this.updatingConfig.set(false);
        if (result.success && result.status) {
          this.selectedTenant.set(result.status);
          setTimeout(() => this.closeConfigModal(), 1500);
        }
      },
      error: (err) => {
        this.configResult.set({
          success: false,
          message: err.error?.error || err.error?.message || err.message,
        });
        this.updatingConfig.set(false);
      },
    });
  }

  // ─── Suspend / Revoke / Delete ──────────────────────────

  openConfirmAction(type: 'suspend' | 'revoke' | 'delete', tenantId: string, tenantName: string) {
    this.confirmAction.set({ type, tenantId, tenantName });
    this.actionResult.set(null);
  }

  cancelAction() {
    this.confirmAction.set(null);
    this.actionResult.set(null);
  }

  executeAction() {
    const action = this.confirmAction();
    if (!action) return;

    this.actionLoading.set(true);
    this.actionResult.set(null);

    let obs;
    switch (action.type) {
      case 'suspend':
        obs = this.tenantCaService.suspendTenantCA(action.tenantId);
        break;
      case 'revoke':
        obs = this.tenantCaService.revokeTenantCA(action.tenantId);
        break;
      case 'delete':
        obs = this.tenantCaService.deleteTenantCA(action.tenantId);
        break;
    }

    obs.subscribe({
      next: (result) => {
        this.actionResult.set(result);
        this.actionLoading.set(false);
        if (result.success) {
          if (action.type === 'delete') {
            setTimeout(() => {
              this.cancelAction();
              this.backToList();
            }, 1500);
          } else {
            this.refreshDetail(action.tenantId);
            setTimeout(() => {
              this.cancelAction();
              this.loadTenantCAs();
            }, 1500);
          }
        }
      },
      error: (err) => {
        this.actionResult.set({
          success: false,
          message: err.error?.error || err.error?.message || err.message,
        });
        this.actionLoading.set(false);
      },
    });
  }

  // ─── User Cert Provision ────────────────────────────────

  openUserCertModal(tenantId: string) {
    this.userCertTenantId = tenantId;
    this.userCertUserId = '';
    this.userSearchQuery = '';
    this.userSearchResults.set([]);
    this.userCertResult.set(null);
    this.showUserCertModal.set(true);
  }

  closeUserCertModal() {
    this.showUserCertModal.set(false);
    this.userCertResult.set(null);
  }

  searchUsers() {
    const q = this.userSearchQuery.trim();
    if (q.length < 2) return;
    this.searchingUsers.set(true);
    this.userService.getUsers(q, undefined, true, 1, 10).subscribe({
      next: (data) => {
        this.userSearchResults.set(data.items || []);
        this.searchingUsers.set(false);
      },
      error: () => {
        this.userSearchResults.set([]);
        this.searchingUsers.set(false);
      },
    });
  }

  selectUser(user: any) {
    this.userCertUserId = user.id;
    this.userSearchQuery = user.fullName || user.username;
    this.userSearchResults.set([]);
  }

  provisionUserCert() {
    if (!this.userCertUserId.trim()) return;

    this.provisioningUserCert.set(true);
    this.userCertResult.set(null);
    this.tenantCaService.provisionUserCert(this.userCertTenantId, this.userCertUserId.trim()).subscribe({
      next: (result) => {
        this.userCertResult.set(result);
        this.provisioningUserCert.set(false);
        if (result.success) this.refreshDetail(this.userCertTenantId);
      },
      error: (err) => {
        this.userCertResult.set({
          success: false,
          message: err.error?.error || err.error?.message || err.message,
        });
        this.provisioningUserCert.set(false);
      },
    });
  }

  // ─── Helpers ────────────────────────────────────────────

  getStatusInfo(status: string) {
    return this.caStatuses.find((s) => s.value === status) || {
      value: status,
      label: status,
      icon: '❓',
      cssClass: 'status-unknown',
    };
  }

  formatDate(dateStr: string | null | undefined): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString('vi-VN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  }
}
