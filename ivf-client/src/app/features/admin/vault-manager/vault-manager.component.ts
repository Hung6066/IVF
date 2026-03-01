import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { KeyVaultService } from '../../../core/services/keyvault.service';
import { ZeroTrustService } from '../../../core/services/zerotrust.service';
import { environment } from '../../../../environments/environment';
import { SecretRotationTabComponent } from './tabs/secret-rotation-tab.component';
import { DekRotationTabComponent } from './tabs/dek-rotation-tab.component';
import { DbCredentialRotationTabComponent } from './tabs/db-credential-rotation-tab.component';
import { ComplianceTabComponent } from './tabs/compliance-tab.component';
import { VaultDrTabComponent } from './tabs/vault-dr-tab.component';
import { SiemTabComponent } from './tabs/siem-tab.component';
import { MultiUnsealTabComponent } from './tabs/multi-unseal-tab.component';
import { VaultMetricsTabComponent } from './tabs/vault-metrics-tab.component';
import {
  VaultStatus,
  ApiKeyResponse,
  CreateApiKeyRequest,
  RotateKeyRequest,
  InitializeVaultRequest,
  SecretEntry,
  SecretDetail,
  SECRET_TEMPLATES,
  SecretTemplate,
  WrappedKeyResult,
  EncryptedPayload,
  AutoUnsealStatus,
  KeyPurpose,
  VaultSettings,
  VaultPolicy,
  VaultUserPolicy,
  VaultLease,
  DynamicCredential,
  VaultToken,
  VaultAuditLog,
  VaultAuditLogResponse,
  TokenCreateResponse,
  SecretImportResult,
  EncryptionConfigResponse,
  FieldAccessPolicyResponse,
  AccessLevel,
  SecurityDashboard,
  SecurityEvent,
  DbTableSchema,
} from '../../../core/models/keyvault.model';
import {
  ZTPolicyResponse,
  ZTAccessDecision,
  UpdateZTPolicyRequest,
} from '../../../core/models/zerotrust.model';

type VaultTab =
  | 'secrets'
  | 'database'
  | 'api-keys'
  | 'policies'
  | 'user-policies'
  | 'leases'
  | 'rotation'
  | 'dynamic'
  | 'tokens'
  | 'settings'
  | 'import'
  | 'history'
  | 'encryption'
  | 'field-access'
  | 'zero-trust'
  | 'secret-rotation'
  | 'dek-rotation'
  | 'db-rotation'
  | 'compliance'
  | 'vault-dr'
  | 'siem'
  | 'multi-unseal'
  | 'vault-metrics';

@Component({
  selector: 'app-vault-manager',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SecretRotationTabComponent,
    DekRotationTabComponent,
    DbCredentialRotationTabComponent,
    ComplianceTabComponent,
    VaultDrTabComponent,
    SiemTabComponent,
    MultiUnsealTabComponent,
    VaultMetricsTabComponent,
  ],
  templateUrl: './vault-manager.component.html',
  styleUrls: ['./vault-manager.component.scss'],
})
export class VaultManagerComponent implements OnInit {
  Math = Math;
  // ─── Vault State ────────────────────────────────
  vaultStatus = signal<VaultStatus | null>(null);
  isHealthy = signal(false);
  loading = signal(false);
  activeTab = signal<VaultTab>('secrets');

  // ─── Secrets ────────────────────────────────────
  secrets = signal<SecretEntry[]>([]);
  breadcrumb = signal<string[]>([]);
  selectedSecret = signal<SecretDetail | null>(null);
  showSecretData = signal<Record<string, boolean>>({});
  showNewSecretDialog = signal(false);
  showSecretDetailDialog = signal(false);
  newSecretForm = { path: '', data: '{\n  \n}', ttl: '' };
  secretTemplates = SECRET_TEMPLATES;

  // ─── API Keys ───────────────────────────────────
  expiringKeys = signal<ApiKeyResponse[]>([]);
  showCreateKeyForm = signal(false);
  rotatingKey = signal<{ keyName: string; serviceName: string } | null>(null);
  initForm: InitializeVaultRequest = { masterPassword: '', userId: '' };
  createForm: CreateApiKeyRequest = {
    keyName: '',
    serviceName: '',
    keyPrefix: '',
    keyHash: '',
    environment: 'Development',
    createdBy: '',
    rotationIntervalDays: 90,
  };
  rotateForm: RotateKeyRequest = {
    serviceName: '',
    keyName: '',
    newKeyHash: '',
    rotatedBy: '',
  };

  // ─── Zero Trust ─────────────────────────────────
  ztPolicies = signal<ZTPolicyResponse[]>([]);
  editingZTPolicy = signal(false);
  accessDecision = signal<ZTAccessDecision | null>(null);
  accessCheckAction = '';
  editForm: UpdateZTPolicyRequest = {
    action: '',
    requiredAuthLevel: 'Session',
    maxAllowedRisk: 'Medium',
    requireTrustedDevice: false,
    requireFreshSession: false,
    blockAnomaly: false,
    requireGeoFence: false,
    allowedCountries: null,
    blockVpnTor: false,
    allowBreakGlassOverride: false,
    updatedBy: '',
  };

  // ─── Status Message ─────────────────────────────
  statusMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  // ─── Encryption / Key Wrap ──────────────────────
  wrapForm = { plaintext: '', keyName: 'unseal-key' };
  wrapResult = signal<WrappedKeyResult | null>(null);
  unwrapForm = { wrappedKeyBase64: '', ivBase64: '', keyName: 'unseal-key' };
  unwrapResult = signal<string | null>(null);
  encryptForm = { plaintext: '', purpose: 'Data' as KeyPurpose };
  encryptResult = signal<EncryptedPayload | null>(null);
  decryptForm = { ciphertextBase64: '', ivBase64: '', purpose: 'Data' as KeyPurpose };
  decryptResult = signal<string | null>(null);
  autoUnsealStatus = signal<AutoUnsealStatus | null>(null);
  autoUnsealForm = { masterPassword: '', azureKeyName: 'unseal-key' };
  keyPurposes: KeyPurpose[] = ['Data', 'Session', 'Api', 'Backup', 'MasterSalt'];

  // ─── Settings ───────────────────────────────────
  vaultSettings = signal<VaultSettings | null>(null);
  settingsForm = { vaultUrl: '', keyName: '', tenantId: '', clientId: '', clientSecret: '' };
  testingConnection = signal(false);
  connectionResult = signal<{ connected: boolean; message: string } | null>(null);

  // ─── Database Secrets ───────────────────────────
  databaseSecrets = signal<{ name: string; host?: string; database?: string; port?: number }[]>([]);
  showDbDialog = signal(false);
  newDbForm = { name: '', host: '', port: 5432, username: '', password: '', database: '' };

  // ─── Policies ───────────────────────────────────
  policies = signal<VaultPolicy[]>([]);
  showPolicyDialog = signal(false);
  editingPolicy = signal<VaultPolicy | null>(null);
  newPolicyForm = {
    name: '',
    description: '',
    pathPattern: '*',
    capabilities: ['read', 'list'] as string[],
  };
  allCapabilities = ['read', 'list', 'create', 'update', 'delete', 'sudo'];

  // ─── User Policies ─────────────────────────────
  userPolicies = signal<VaultUserPolicy[]>([]);
  showUserPolicyDialog = signal(false);
  newUserPolicyForm = { userId: '', policyId: '' };

  // ─── Leases ─────────────────────────────────────
  leases = signal<VaultLease[]>([]);
  showLeaseDialog = signal(false);
  newLeaseForm = { secretPath: '', ttlSeconds: 3600, renewable: true };

  // ─── Dynamic Credentials ───────────────────────
  dynamicCredentials = signal<DynamicCredential[]>([]);
  showDynamicDialog = signal(false);
  newDynamicForm = {
    backend: 'postgres',
    username: '',
    dbHost: 'localhost',
    dbPort: 5432,
    dbName: '',
    adminUsername: '',
    adminPassword: '',
    ttlSeconds: 3600,
  };

  // ─── Tokens ─────────────────────────────────────
  tokens = signal<VaultToken[]>([]);
  showTokenDialog = signal(false);
  newTokenForm = { displayName: '', policies: '', tokenType: 'service', ttl: 3600, numUses: 0 };
  createdToken = signal<TokenCreateResponse | null>(null);

  // ─── Audit Logs ─────────────────────────────────
  auditLogs = signal<VaultAuditLog[]>([]);
  auditLogPage = signal(1);
  auditLogTotalCount = signal(0);

  // ─── Import ─────────────────────────────────────
  importData = signal<string>('');
  importFormat = signal<'json' | 'env'>('json');
  importPrefix = signal<string>('');
  importResult = signal<{ success: number; failed: number } | null>(null);

  // ─── Encryption Config (Auto-Encryption) ────────
  encryptionConfigs = signal<EncryptionConfigResponse[]>([]);
  showEncConfigDialog = signal(false);
  editingEncConfig = signal<EncryptionConfigResponse | null>(null);
  newEncConfigForm = {
    tableName: '',
    encryptedFields: [] as string[],
    dekPurpose: 'data',
    description: '',
    isDefault: false,
  };
  availableFields: string[] = [];
  dekPurposeOptions = ['data', 'session', 'api', 'backup'];
  // Dynamic DB schema loaded from backend
  dbTables = signal<DbTableSchema[]>([]);
  dbTableNames = computed(() => this.dbTables().map((t) => t.tableName));

  // ─── Field Access Policies ─────────────────────
  fieldAccessPolicies = signal<FieldAccessPolicyResponse[]>([]);
  showFAPolicyDialog = signal(false);
  editingFAPolicy = signal<FieldAccessPolicyResponse | null>(null);
  faActiveSubTab = signal<'policies' | 'audit'>('policies');
  faAuditLogs = signal<VaultAuditLog[]>([]);
  newFAPolicyForm = {
    tableName: '',
    fieldNames: [] as string[],
    role: '',
    accessLevel: 'none' as AccessLevel,
    maskPattern: '********',
    partialLength: 5,
    description: '',
  };
  faAvailableFields: string[] = [];
  faRoles = signal<string[]>([]);
  faAccessLevels: AccessLevel[] = ['full', 'partial', 'masked', 'none'];
  faPoliciesGrouped = computed(() => {
    const policies = this.fieldAccessPolicies();
    const grouped: Record<string, FieldAccessPolicyResponse[]> = {};
    for (const p of policies) {
      if (!grouped[p.tableName]) grouped[p.tableName] = [];
      grouped[p.tableName].push(p);
    }
    return grouped;
  });
  faGroupKeys = computed(() => Object.keys(this.faPoliciesGrouped()));
  faExpandedGroups = signal<Record<string, boolean>>({});

  // ─── Security Dashboard ────────────────────────
  securityDashboard = signal<SecurityDashboard | null>(null);

  // ─── Computed ───────────────────────────────────
  isInitialized = computed(() => this.vaultStatus()?.isInitialized ?? false);
  activeKeyCount = computed(() => this.vaultStatus()?.activeKeyCount ?? 0);
  activeZTPolicies = computed(() => this.ztPolicies().filter((p) => p.isActive).length);
  vpnBlockCount = computed(() => this.ztPolicies().filter((p) => p.blockVpnTor).length);
  trustedDeviceCount = computed(
    () => this.ztPolicies().filter((p) => p.requireTrustedDevice).length,
  );
  currentPath = computed(() => this.breadcrumb().join('/'));

  tabs: { key: VaultTab; label: string; icon: string }[] = [
    { key: 'secrets', label: 'Secrets', icon: '🔑' },
    { key: 'database', label: 'Database', icon: '🗄️' },
    { key: 'api-keys', label: 'API Keys', icon: '🗝️' },
    { key: 'policies', label: 'Policies', icon: '📋' },
    { key: 'user-policies', label: 'User Policies', icon: '👤' },
    { key: 'leases', label: 'Leases', icon: '⏱️' },
    { key: 'rotation', label: 'Rotation', icon: '🔄' },
    { key: 'dynamic', label: 'Dynamic', icon: '⚡' },
    { key: 'tokens', label: 'Tokens', icon: '🎟️' },
    { key: 'settings', label: 'Settings', icon: '⚙️' },
    { key: 'import', label: 'Import', icon: '📥' },
    { key: 'history', label: 'Lịch sử', icon: '📜' },
    { key: 'encryption', label: 'Encryption', icon: '🔒' },
    { key: 'field-access', label: 'Phân quyền', icon: '🔐' },
    { key: 'zero-trust', label: 'Zero Trust', icon: '🛡️' },
    { key: 'secret-rotation', label: 'Secret Rotation', icon: '🔄' },
    { key: 'dek-rotation', label: 'DEK Rotation', icon: '🗝️' },
    { key: 'db-rotation', label: 'DB Credentials', icon: '🗄️' },
    { key: 'compliance', label: 'Compliance', icon: '✅' },
    { key: 'vault-dr', label: 'DR & Backup', icon: '💾' },
    { key: 'siem', label: 'SIEM Events', icon: '🔍' },
    { key: 'multi-unseal', label: 'Multi-Unseal', icon: '🔓' },
    { key: 'vault-metrics', label: 'Metrics', icon: '📊' },
  ];

  constructor(
    private kvService: KeyVaultService,
    private ztService: ZeroTrustService,
    private http: HttpClient,
  ) {}

  ngOnInit() {
    this.loadVaultStatus();
    this.loadHealth();
    this.loadExpiringKeys();
  }

  // ─── Tab Switch ─────────────────────────────────
  onTabChange(tab: VaultTab) {
    this.activeTab.set(tab);
    switch (tab) {
      case 'secrets':
        this.loadSecrets();
        break;
      case 'database':
        this.loadDatabaseSecrets();
        break;
      case 'api-keys':
        this.loadVaultStatus();
        break;
      case 'policies':
        this.loadPolicies();
        break;
      case 'user-policies':
        this.loadUserPolicies();
        break;
      case 'leases':
        this.loadLeases();
        break;
      case 'rotation':
        this.loadVaultStatus();
        break;
      case 'dynamic':
        this.loadDynamicCredentials();
        break;
      case 'tokens':
        this.loadTokens();
        break;
      case 'settings':
        this.loadSettings();
        break;
      case 'zero-trust':
        this.loadZTPolicies();
        this.loadSecurityDashboard();
        break;
      case 'encryption':
        this.loadEncryptionConfigs();
        this.loadAutoUnsealStatus();
        this.loadDbSchema();
        break;
      case 'field-access':
        this.loadFieldAccessPolicies();
        this.loadDbSchema();
        this.loadRoles();
        break;
      case 'history':
        this.loadAuditLogs();
        break;
      case 'secret-rotation':
      case 'dek-rotation':
      case 'db-rotation':
      case 'compliance':
      case 'vault-dr':
      case 'siem':
      case 'multi-unseal':
      case 'vault-metrics':
        break; // These tabs self-load via their own ngOnInit
    }
  }

  // ─── Vault Status ──────────────────────────────
  loadVaultStatus() {
    this.kvService.getVaultStatus().subscribe({
      next: (s) => this.vaultStatus.set(s),
      error: () => this.showStatus('Không thể tải trạng thái vault', 'error'),
    });
  }

  loadHealth() {
    this.kvService.checkHealth().subscribe({
      next: (r) => this.isHealthy.set(r.healthy),
      error: () => this.isHealthy.set(false),
    });
  }

  loadExpiringKeys() {
    this.kvService.getExpiringKeys(30).subscribe({
      next: (keys) => this.expiringKeys.set(keys),
      error: () => {},
    });
  }

  initializeVault() {
    this.loading.set(true);
    this.kvService.initializeVault(this.initForm).subscribe({
      next: () => {
        this.showStatus('Vault đã được khởi tạo thành công', 'success');
        this.loadVaultStatus();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi khởi tạo vault', 'error');
        this.loading.set(false);
      },
    });
  }

  // ─── Secrets ────────────────────────────────────
  loadSecrets() {
    const prefix = this.currentPath() || undefined;
    this.kvService.listSecrets(prefix).subscribe({
      next: (list) => this.secrets.set(list),
      error: () => this.showStatus('Không thể tải danh sách secrets', 'error'),
    });
  }

  navigateToFolder(folderName: string) {
    const cleaned = folderName.replace(/\/$/, '');
    this.breadcrumb.update((b) => [...b, cleaned]);
    this.loadSecrets();
  }

  navigateHome() {
    this.breadcrumb.set([]);
    this.loadSecrets();
  }

  navigateToBreadcrumb(index: number) {
    this.breadcrumb.update((b) => b.slice(0, index + 1));
    this.loadSecrets();
  }

  viewSecret(name: string) {
    const fullPath = this.currentPath() ? `${this.currentPath()}/${name}` : name;
    this.kvService.getSecret(fullPath).subscribe({
      next: (detail) => {
        this.selectedSecret.set(detail);
        this.showSecretDetailDialog.set(true);
        this.showSecretData.set({});
      },
      error: () => this.showStatus('Không thể tải secret', 'error'),
    });
  }

  deleteSecretEntry(name: string) {
    const fullPath = this.currentPath() ? `${this.currentPath()}/${name}` : name;
    this.kvService.deleteSecret(fullPath).subscribe({
      next: () => {
        this.showStatus('Secret đã được xóa', 'success');
        this.loadSecrets();
      },
      error: () => this.showStatus('Lỗi xóa secret', 'error'),
    });
  }

  applyTemplate(template: SecretTemplate) {
    this.newSecretForm.path = template.path;
    this.newSecretForm.data = template.data;
  }

  createSecret() {
    const path = this.currentPath()
      ? `${this.currentPath()}/${this.newSecretForm.path}`
      : this.newSecretForm.path;
    this.loading.set(true);
    this.kvService.createSecret({ name: path, value: this.newSecretForm.data }).subscribe({
      next: () => {
        this.showStatus('Secret đã được tạo thành công', 'success');
        this.showNewSecretDialog.set(false);
        this.newSecretForm = { path: '', data: '{\n  \n}', ttl: '' };
        this.loadSecrets();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo secret', 'error');
        this.loading.set(false);
      },
    });
  }

  toggleSecretField(field: string) {
    this.showSecretData.update((m) => ({ ...m, [field]: !m[field] }));
  }

  copyToClipboard(value: string) {
    navigator.clipboard.writeText(value).then(
      () => this.showStatus('Đã sao chép!', 'success'),
      () => this.showStatus('Không thể sao chép', 'error'),
    );
  }

  getSecretFields(): { key: string; value: string }[] {
    const detail = this.selectedSecret();
    if (!detail?.value) return [];
    try {
      const parsed = JSON.parse(detail.value);
      return Object.entries(parsed).map(([key, value]) => ({
        key,
        value: typeof value === 'string' ? value : JSON.stringify(value),
      }));
    } catch {
      return [{ key: 'value', value: detail.value }];
    }
  }

  // ─── API Keys ───────────────────────────────────
  createKey() {
    this.loading.set(true);
    this.kvService.createKey(this.createForm).subscribe({
      next: () => {
        this.showStatus('Key đã được tạo thành công', 'success');
        this.loadVaultStatus();
        this.showCreateKeyForm.set(false);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo key', 'error');
        this.loading.set(false);
      },
    });
  }

  openRotateDialog(key: { keyName: string; serviceName: string }) {
    this.rotatingKey.set(key);
    this.rotateForm = {
      serviceName: key.serviceName,
      keyName: key.keyName,
      newKeyHash: '',
      rotatedBy: '',
    };
  }

  rotateKey() {
    this.loading.set(true);
    this.kvService.rotateKey(this.rotateForm).subscribe({
      next: () => {
        this.showStatus('Key đã được xoay thành công', 'success');
        this.rotatingKey.set(null);
        this.loadVaultStatus();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi xoay key', 'error');
        this.loading.set(false);
      },
    });
  }

  // ─── Zero Trust ─────────────────────────────────
  loadZTPolicies() {
    this.ztService.getAllPolicies().subscribe({
      next: (p) => this.ztPolicies.set(p),
      error: () => this.showStatus('Không thể tải chính sách ZT', 'error'),
    });
  }

  editPolicy(policy: ZTPolicyResponse) {
    this.editForm = {
      action: policy.action,
      requiredAuthLevel: policy.requiredAuthLevel,
      maxAllowedRisk: policy.maxAllowedRisk,
      requireTrustedDevice: policy.requireTrustedDevice,
      requireFreshSession: policy.requireFreshSession,
      blockAnomaly: policy.blockAnomaly,
      requireGeoFence: policy.requireGeoFence,
      allowedCountries: policy.allowedCountries,
      blockVpnTor: policy.blockVpnTor,
      allowBreakGlassOverride: policy.allowBreakGlassOverride,
      updatedBy: '',
    };
    this.editingZTPolicy.set(true);
  }

  savePolicy() {
    this.loading.set(true);
    this.ztService.updatePolicy(this.editForm).subscribe({
      next: () => {
        this.showStatus('Chính sách đã được cập nhật', 'success');
        this.editingZTPolicy.set(false);
        this.loadZTPolicies();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi cập nhật chính sách', 'error');
        this.loading.set(false);
      },
    });
  }

  testAccess() {
    if (!this.accessCheckAction) return;
    this.loading.set(true);
    this.ztService.checkAccess({ action: this.accessCheckAction }).subscribe({
      next: (d) => {
        this.accessDecision.set(d);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi kiểm tra truy cập', 'error');
        this.loading.set(false);
      },
    });
  }

  getAuthLevelClass(level: string): string {
    const map: Record<string, string> = {
      None: 'badge-none',
      Session: 'badge-session',
      Password: 'badge-password',
      MFA: 'badge-mfa',
      FreshSession: 'badge-freshsession',
      Biometric: 'badge-biometric',
    };
    return map[level] || 'badge-none';
  }

  getRiskClass(risk: string): string {
    const map: Record<string, string> = {
      Low: 'badge-low',
      Medium: 'badge-medium',
      High: 'badge-high',
      Critical: 'badge-critical',
    };
    return map[risk] || 'badge-secondary';
  }

  // ─── Encryption / Key Wrap ───────────────────────
  wrapKey() {
    const plaintextBase64 = btoa(this.wrapForm.plaintext);
    this.loading.set(true);
    this.kvService.wrapKey({ plaintextBase64, keyName: this.wrapForm.keyName }).subscribe({
      next: (r) => {
        this.wrapResult.set(r);
        this.loading.set(false);
        this.showStatus('Key wrapped thành công', 'success');
      },
      error: () => {
        this.showStatus('Lỗi wrap key', 'error');
        this.loading.set(false);
      },
    });
  }

  unwrapKey() {
    this.loading.set(true);
    this.kvService
      .unwrapKey({
        wrappedKeyBase64: this.unwrapForm.wrappedKeyBase64,
        ivBase64: this.unwrapForm.ivBase64,
        keyName: this.unwrapForm.keyName,
      })
      .subscribe({
        next: (r) => {
          this.unwrapResult.set(atob(r.plaintextBase64));
          this.loading.set(false);
          this.showStatus('Key unwrapped thành công', 'success');
        },
        error: () => {
          this.showStatus('Lỗi unwrap key', 'error');
          this.loading.set(false);
        },
      });
  }

  encryptData() {
    const plaintextBase64 = btoa(this.encryptForm.plaintext);
    this.loading.set(true);
    this.kvService.encrypt({ plaintextBase64, purpose: this.encryptForm.purpose }).subscribe({
      next: (r) => {
        this.encryptResult.set(r);
        this.loading.set(false);
        this.showStatus('Dữ liệu đã được mã hóa', 'success');
      },
      error: () => {
        this.showStatus('Lỗi mã hóa dữ liệu', 'error');
        this.loading.set(false);
      },
    });
  }

  decryptData() {
    this.loading.set(true);
    this.kvService
      .decrypt({
        ciphertextBase64: this.decryptForm.ciphertextBase64,
        ivBase64: this.decryptForm.ivBase64,
        purpose: this.decryptForm.purpose,
      })
      .subscribe({
        next: (r) => {
          this.decryptResult.set(atob(r.plaintextBase64));
          this.loading.set(false);
          this.showStatus('Dữ liệu đã được giải mã', 'success');
        },
        error: () => {
          this.showStatus('Lỗi giải mã dữ liệu', 'error');
          this.loading.set(false);
        },
      });
  }

  loadAutoUnsealStatus() {
    this.kvService.getAutoUnsealStatus().subscribe({
      next: (s) => this.autoUnsealStatus.set(s),
      error: () => {},
    });
  }

  configureAutoUnseal() {
    this.loading.set(true);
    this.kvService.configureAutoUnseal(this.autoUnsealForm).subscribe({
      next: () => {
        this.showStatus('Auto-unseal đã được cấu hình', 'success');
        this.loadAutoUnsealStatus();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi cấu hình auto-unseal', 'error');
        this.loading.set(false);
      },
    });
  }

  triggerAutoUnseal() {
    this.loading.set(true);
    this.kvService.autoUnseal().subscribe({
      next: (r) => {
        this.showStatus(
          r.success ? 'Vault đã được auto-unseal' : 'Auto-unseal thất bại',
          r.success ? 'success' : 'error',
        );
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi auto-unseal', 'error');
        this.loading.set(false);
      },
    });
  }

  useWrapResultForUnwrap() {
    const wr = this.wrapResult();
    if (wr) {
      this.unwrapForm.wrappedKeyBase64 = wr.wrappedKeyBase64;
      this.unwrapForm.ivBase64 = wr.ivBase64;
      this.unwrapForm.keyName = wr.keyName;
    }
  }

  useEncryptResultForDecrypt() {
    const er = this.encryptResult();
    if (er) {
      this.decryptForm.ciphertextBase64 = er.ciphertextBase64;
      this.decryptForm.ivBase64 = er.ivBase64;
      this.decryptForm.purpose = er.purpose;
    }
  }

  // ─── Settings ────────────────────────────────────
  loadSettings() {
    this.kvService.getSettings().subscribe({
      next: (s) => {
        this.vaultSettings.set(s);
        this.settingsForm.vaultUrl = s.azure?.vaultUrl || '';
        this.settingsForm.keyName = s.azure?.keyName || '';
        this.settingsForm.tenantId = s.azure?.tenantId || '';
        this.settingsForm.clientId = s.azure?.clientId || '';
        this.settingsForm.clientSecret = '';
      },
      error: () => this.showStatus('Không thể tải cấu hình vault', 'error'),
    });
  }

  saveSettings() {
    this.loading.set(true);
    const req: Record<string, string> = {
      vaultUrl: this.settingsForm.vaultUrl,
      keyName: this.settingsForm.keyName,
      tenantId: this.settingsForm.tenantId,
      clientId: this.settingsForm.clientId,
    };
    if (this.settingsForm.clientSecret) {
      req['clientSecret'] = this.settingsForm.clientSecret;
    }
    this.kvService.saveSettings(req).subscribe({
      next: () => {
        this.showStatus('Đã lưu cấu hình Azure Key Vault', 'success');
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi lưu cấu hình', 'error');
        this.loading.set(false);
      },
    });
  }

  testConnection() {
    this.testingConnection.set(true);
    this.connectionResult.set(null);
    this.kvService.testConnection().subscribe({
      next: (r) => {
        this.connectionResult.set(r);
        this.showStatus(r.message, r.connected ? 'success' : 'error');
        this.testingConnection.set(false);
      },
      error: () => {
        this.connectionResult.set({ connected: false, message: 'Lỗi kết nối' });
        this.showStatus('Lỗi test kết nối', 'error');
        this.testingConnection.set(false);
      },
    });
  }

  // ─── Database Secrets ────────────────────────────
  loadDatabaseSecrets() {
    this.kvService.listSecrets('database').subscribe({
      next: (list) => {
        const secrets = list
          .filter((e) => e.type === 'secret')
          .map(
            (e) =>
              ({ name: e.name }) as {
                name: string;
                host?: string;
                database?: string;
                port?: number;
              },
          );
        this.databaseSecrets.set(secrets);
        // Load details for each
        secrets.forEach((s) => {
          this.kvService.getSecret(`database/${s.name}`).subscribe({
            next: (detail) => {
              try {
                const parsed = JSON.parse(detail.value);
                this.databaseSecrets.update((list) =>
                  list.map((d) =>
                    d.name === s.name
                      ? { ...d, host: parsed.host, database: parsed.database, port: parsed.port }
                      : d,
                  ),
                );
              } catch {}
            },
          });
        });
      },
      error: () => {},
    });
  }

  createDatabaseSecret() {
    const path = `database/${this.newDbForm.name}`;
    const data = JSON.stringify({
      host: this.newDbForm.host,
      port: this.newDbForm.port,
      username: this.newDbForm.username,
      password: this.newDbForm.password,
      database: this.newDbForm.database,
    });
    this.loading.set(true);
    this.kvService.createSecret({ name: path, value: data }).subscribe({
      next: () => {
        this.showStatus(`Database "${this.newDbForm.name}" đã được lưu`, 'success');
        this.showDbDialog.set(false);
        this.newDbForm = {
          name: '',
          host: '',
          port: 5432,
          username: '',
          password: '',
          database: '',
        };
        this.loadDatabaseSecrets();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo database secret', 'error');
        this.loading.set(false);
      },
    });
  }

  deleteDbSecret(name: string) {
    this.kvService.deleteSecret(`database/${name}`).subscribe({
      next: () => {
        this.showStatus('Đã xóa database credential', 'success');
        this.loadDatabaseSecrets();
      },
      error: () => this.showStatus('Lỗi xóa credential', 'error'),
    });
  }

  // ─── Policies ────────────────────────────────────
  loadPolicies() {
    this.kvService.getPolicies().subscribe({
      next: (list) => this.policies.set(list),
      error: () => this.policies.set([]),
    });
  }

  createPolicy() {
    this.loading.set(true);
    this.kvService
      .createPolicy({
        name: this.newPolicyForm.name,
        pathPattern: this.newPolicyForm.pathPattern,
        capabilities: this.newPolicyForm.capabilities,
        description: this.newPolicyForm.description,
      })
      .subscribe({
        next: () => {
          this.showStatus('Policy đã được tạo', 'success');
          this.showPolicyDialog.set(false);
          this.newPolicyForm = {
            name: '',
            description: '',
            pathPattern: '*',
            capabilities: ['read', 'list'],
          };
          this.loadPolicies();
          this.loading.set(false);
        },
        error: () => {
          this.showStatus('Lỗi tạo policy', 'error');
          this.loading.set(false);
        },
      });
  }

  deletePolicy(id: string) {
    this.kvService.deletePolicy(id).subscribe({
      next: () => {
        this.showStatus('Policy đã được xóa', 'success');
        this.loadPolicies();
      },
      error: () => this.showStatus('Lỗi xóa policy', 'error'),
    });
  }

  toggleCapability(cap: string) {
    const idx = this.newPolicyForm.capabilities.indexOf(cap);
    if (idx >= 0) {
      this.newPolicyForm.capabilities.splice(idx, 1);
    } else {
      this.newPolicyForm.capabilities.push(cap);
    }
  }

  // ─── User Policies ──────────────────────────────
  loadUserPolicies() {
    this.kvService.getUserPolicies().subscribe({
      next: (list) => this.userPolicies.set(list),
      error: () => this.userPolicies.set([]),
    });
  }

  assignUserPolicy() {
    this.loading.set(true);
    this.kvService
      .assignUserPolicy({
        userId: this.newUserPolicyForm.userId,
        policyId: this.newUserPolicyForm.policyId,
      })
      .subscribe({
        next: () => {
          this.showStatus('User policy đã được gán', 'success');
          this.showUserPolicyDialog.set(false);
          this.newUserPolicyForm = { userId: '', policyId: '' };
          this.loadUserPolicies();
          this.loading.set(false);
        },
        error: () => {
          this.showStatus('Lỗi gán user policy', 'error');
          this.loading.set(false);
        },
      });
  }

  removeUserPolicy(id: string) {
    this.kvService.removeUserPolicy(id).subscribe({
      next: () => {
        this.showStatus('Đã gỡ user policy', 'success');
        this.loadUserPolicies();
      },
      error: () => this.showStatus('Lỗi gỡ user policy', 'error'),
    });
  }

  // ─── Leases ──────────────────────────────────────
  loadLeases() {
    this.kvService.getLeases(true).subscribe({
      next: (list) => this.leases.set(list),
      error: () => this.leases.set([]),
    });
  }

  revokeLease(leaseId: string) {
    this.kvService.revokeLease(leaseId).subscribe({
      next: () => {
        this.showStatus('Lease đã được thu hồi', 'success');
        this.loadLeases();
      },
      error: () => this.showStatus('Lỗi thu hồi lease', 'error'),
    });
  }

  createLease() {
    this.loading.set(true);
    this.kvService.createLease(this.newLeaseForm).subscribe({
      next: () => {
        this.showStatus('Lease đã được tạo', 'success');
        this.showLeaseDialog.set(false);
        this.newLeaseForm = { secretPath: '', ttlSeconds: 3600, renewable: true };
        this.loadLeases();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo lease', 'error');
        this.loading.set(false);
      },
    });
  }

  renewLease(leaseId: string) {
    this.kvService.renewLease(leaseId, 3600).subscribe({
      next: () => {
        this.showStatus('Lease đã được gia hạn', 'success');
        this.loadLeases();
      },
      error: () => this.showStatus('Lỗi gia hạn lease', 'error'),
    });
  }

  // ─── Dynamic Credentials ─────────────────────────
  loadDynamicCredentials() {
    this.kvService.getDynamicCredentials().subscribe({
      next: (list) => this.dynamicCredentials.set(list),
      error: () => this.dynamicCredentials.set([]),
    });
  }

  createDynamicCredential() {
    this.loading.set(true);
    this.kvService.createDynamicCredential(this.newDynamicForm).subscribe({
      next: () => {
        this.showStatus('Dynamic credential đã được tạo', 'success');
        this.showDynamicDialog.set(false);
        this.newDynamicForm = {
          backend: 'postgres',
          username: '',
          dbHost: 'localhost',
          dbPort: 5432,
          dbName: '',
          adminUsername: '',
          adminPassword: '',
          ttlSeconds: 3600,
        };
        this.loadDynamicCredentials();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo dynamic credential', 'error');
        this.loading.set(false);
      },
    });
  }

  deleteDynamicCredential(id: string) {
    this.kvService.revokeDynamicCredential(id).subscribe({
      next: () => {
        this.showStatus('Đã xóa dynamic credential', 'success');
        this.loadDynamicCredentials();
      },
      error: () => this.showStatus('Lỗi xóa credential', 'error'),
    });
  }

  // ─── Tokens ──────────────────────────────────────
  loadTokens() {
    this.kvService.getTokens().subscribe({
      next: (list) => this.tokens.set(list),
      error: () => this.tokens.set([]),
    });
  }

  createToken() {
    const policiesArr = this.newTokenForm.policies
      .split(',')
      .map((p) => p.trim())
      .filter(Boolean);
    this.loading.set(true);
    this.kvService
      .createToken({
        displayName: this.newTokenForm.displayName,
        policies: policiesArr,
        tokenType: this.newTokenForm.tokenType,
        ttl: this.newTokenForm.ttl,
        numUses: this.newTokenForm.numUses || undefined,
      })
      .subscribe({
        next: (r) => {
          this.createdToken.set(r);
          this.showStatus('Token đã được tạo — hãy copy ngay!', 'success');
          this.showTokenDialog.set(false);
          this.newTokenForm = {
            displayName: '',
            policies: '',
            tokenType: 'service',
            ttl: 3600,
            numUses: 0,
          };
          this.loadTokens();
          this.loading.set(false);
        },
        error: () => {
          this.showStatus('Lỗi tạo token', 'error');
          this.loading.set(false);
        },
      });
  }

  revokeToken(id: string) {
    this.kvService.revokeToken(id).subscribe({
      next: () => {
        this.showStatus('Token đã bị thu hồi', 'success');
        this.loadTokens();
      },
      error: () => this.showStatus('Lỗi thu hồi token', 'error'),
    });
  }

  // ─── Audit Logs ──────────────────────────────────
  loadAuditLogs() {
    this.kvService.getAuditLogs(this.auditLogPage(), 20).subscribe({
      next: (r) => {
        this.auditLogs.set(r.items);
        this.auditLogTotalCount.set(r.totalCount);
      },
      error: () => this.auditLogs.set([]),
    });
  }

  // ─── Import ──────────────────────────────────────
  handleImportFile(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const reader = new FileReader();
      reader.onload = () => {
        this.importData.set(reader.result as string);
      };
      reader.readAsText(file);
    }
  }

  executeImport() {
    const data = this.importData();
    const prefix = this.importPrefix();
    const format = this.importFormat();
    if (!data) {
      this.showStatus('Vui lòng nhập dữ liệu hoặc chọn file', 'error');
      return;
    }

    let secrets: Record<string, string> = {};
    try {
      if (format === 'json') {
        const parsed = JSON.parse(data);
        for (const [key, value] of Object.entries(parsed)) {
          secrets[key] = typeof value === 'string' ? value : JSON.stringify(value);
        }
      } else {
        // .env format
        const lines = data.split('\n').filter((l) => l.trim() && !l.startsWith('#'));
        for (const line of lines) {
          const eqIdx = line.indexOf('=');
          if (eqIdx > 0) {
            const key = line.substring(0, eqIdx).trim();
            const value = line
              .substring(eqIdx + 1)
              .trim()
              .replace(/^["']|["']$/g, '');
            secrets[key] = value;
          }
        }
      }
    } catch {
      this.showStatus('Dữ liệu không hợp lệ', 'error');
      return;
    }

    this.loading.set(true);
    this.kvService.importSecrets({ secrets, prefix: prefix || undefined }).subscribe({
      next: (r) => {
        this.importResult.set({ success: r.imported, failed: r.failed });
        this.showStatus(
          `Import hoàn thành: ${r.imported} thành công, ${r.failed} lỗi`,
          r.imported > 0 ? 'success' : 'error',
        );
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi import secrets', 'error');
        this.loading.set(false);
      },
    });
  }

  // ─── Encryption Configs ───────────────────────────
  loadEncryptionConfigs() {
    this.kvService.getEncryptionConfigs().subscribe({
      next: (configs) => this.encryptionConfigs.set(configs),
      error: () => this.showStatus('Không thể tải cấu hình encryption', 'error'),
    });
  }

  loadDbSchema() {
    if (this.dbTables().length > 0) return; // already loaded
    this.kvService.getDbSchema().subscribe({
      next: (tables) => this.dbTables.set(tables),
      error: () => this.showStatus('Không thể tải schema database', 'error'),
    });
  }

  loadRoles() {
    if (this.faRoles().length > 0) return; // already loaded
    this.http.get<string[]>(`${environment.apiUrl}/users/roles`).subscribe({
      next: (roles) => this.faRoles.set(roles),
      error: () =>
        this.faRoles.set([
          'Admin',
          'Doctor',
          'Nurse',
          'LabTech',
          'Embryologist',
          'Receptionist',
          'Cashier',
          'Pharmacist',
        ]),
    });
  }

  openAddEncConfig() {
    this.editingEncConfig.set(null);
    this.newEncConfigForm = {
      tableName: '',
      encryptedFields: [],
      dekPurpose: 'data',
      description: '',
      isDefault: false,
    };
    this.availableFields = [];
    this.showEncConfigDialog.set(true);
  }

  openEditEncConfig(config: EncryptionConfigResponse) {
    this.editingEncConfig.set(config);
    this.newEncConfigForm = {
      tableName: config.tableName,
      encryptedFields: [...config.encryptedFields],
      dekPurpose: config.dekPurpose,
      description: config.description || '',
      isDefault: config.isDefault,
    };
    this.availableFields =
      this.dbTables()
        .find((t) => t.tableName === config.tableName)
        ?.columns.map((c) => c.name) || [];
    this.showEncConfigDialog.set(true);
  }

  onEncTableChange() {
    const table = this.newEncConfigForm.tableName;
    const schema = this.dbTables().find((t) => t.tableName === table);
    this.availableFields = schema ? schema.columns.map((c) => c.name) : [];
    this.newEncConfigForm.encryptedFields = [];
  }

  toggleEncField(field: string) {
    const idx = this.newEncConfigForm.encryptedFields.indexOf(field);
    if (idx >= 0) {
      this.newEncConfigForm.encryptedFields.splice(idx, 1);
    } else {
      this.newEncConfigForm.encryptedFields.push(field);
    }
  }

  saveEncryptionConfig() {
    this.loading.set(true);
    const editing = this.editingEncConfig();
    if (editing) {
      this.kvService
        .updateEncryptionConfig(editing.id, {
          encryptedFields: this.newEncConfigForm.encryptedFields,
          dekPurpose: this.newEncConfigForm.dekPurpose,
          description: this.newEncConfigForm.description || undefined,
        })
        .subscribe({
          next: () => {
            this.showStatus('Đã cập nhật cấu hình', 'success');
            this.showEncConfigDialog.set(false);
            this.loadEncryptionConfigs();
            this.loading.set(false);
          },
          error: () => {
            this.showStatus('Lỗi cập nhật cấu hình', 'error');
            this.loading.set(false);
          },
        });
    } else {
      this.kvService
        .createEncryptionConfig({
          tableName: this.newEncConfigForm.tableName,
          encryptedFields: this.newEncConfigForm.encryptedFields,
          dekPurpose: this.newEncConfigForm.dekPurpose,
          description: this.newEncConfigForm.description || undefined,
          isDefault: this.newEncConfigForm.isDefault,
        })
        .subscribe({
          next: () => {
            this.showStatus('Đã thêm cấu hình encryption', 'success');
            this.showEncConfigDialog.set(false);
            this.loadEncryptionConfigs();
            this.loading.set(false);
          },
          error: () => {
            this.showStatus('Lỗi tạo cấu hình', 'error');
            this.loading.set(false);
          },
        });
    }
  }

  toggleEncConfig(config: EncryptionConfigResponse) {
    this.kvService.toggleEncryptionConfig(config.id).subscribe({
      next: () => {
        this.loadEncryptionConfigs();
      },
      error: () => this.showStatus('Lỗi thay đổi trạng thái', 'error'),
    });
  }

  deleteEncConfig(config: EncryptionConfigResponse) {
    if (config.isDefault) {
      this.showStatus('Không thể xóa cấu hình mặc định', 'error');
      return;
    }
    this.kvService.deleteEncryptionConfig(config.id).subscribe({
      next: () => {
        this.showStatus('Đã xóa cấu hình', 'success');
        this.loadEncryptionConfigs();
      },
      error: () => this.showStatus('Lỗi xóa cấu hình', 'error'),
    });
  }

  getDekPurposeLabel(purpose: string): string {
    const labels: Record<string, string> = {
      data: 'Data (PHI)',
      session: 'Session',
      api: 'API',
      backup: 'Backup',
    };
    return labels[purpose] || purpose;
  }

  // ─── Field Access Policies ───────────────────────
  loadFieldAccessPolicies() {
    this.kvService.getFieldAccessPolicies().subscribe({
      next: (policies) => this.fieldAccessPolicies.set(policies),
      error: () => this.showStatus('Không thể tải chính sách truy cập', 'error'),
    });
  }

  loadFAAuditLogs() {
    this.kvService.getAuditLogs(1, 50, 'field_access').subscribe({
      next: (r) => this.faAuditLogs.set(r.items),
      error: () => this.faAuditLogs.set([]),
    });
  }

  toggleFAGroup(tableName: string) {
    this.faExpandedGroups.update((g) => ({ ...g, [tableName]: !g[tableName] }));
  }

  openAddFAPolicy() {
    this.editingFAPolicy.set(null);
    this.newFAPolicyForm = {
      tableName: '',
      fieldNames: [],
      role: '',
      accessLevel: 'none',
      maskPattern: '********',
      partialLength: 5,
      description: '',
    };
    this.faAvailableFields = [];
    this.showFAPolicyDialog.set(true);
  }

  openEditFAPolicy(policy: FieldAccessPolicyResponse) {
    this.editingFAPolicy.set(policy);
    this.newFAPolicyForm = {
      tableName: policy.tableName,
      fieldNames: [policy.fieldName],
      role: policy.role,
      accessLevel: policy.accessLevel,
      maskPattern: policy.maskPattern,
      partialLength: policy.partialLength,
      description: policy.description || '',
    };
    this.faAvailableFields =
      this.dbTables()
        .find((t) => t.tableName === policy.tableName)
        ?.columns.map((c) => c.name) || [];
    this.showFAPolicyDialog.set(true);
  }

  onFATableChange() {
    const table = this.newFAPolicyForm.tableName;
    const schema = this.dbTables().find((t) => t.tableName === table);
    this.faAvailableFields = schema ? schema.columns.map((c) => c.name) : [];
    this.newFAPolicyForm.fieldNames = [];
  }

  toggleFAField(field: string) {
    const idx = this.newFAPolicyForm.fieldNames.indexOf(field);
    if (idx >= 0) {
      this.newFAPolicyForm.fieldNames.splice(idx, 1);
    } else {
      this.newFAPolicyForm.fieldNames.push(field);
    }
  }

  saveFAPolicy() {
    this.loading.set(true);
    const editing = this.editingFAPolicy();
    if (editing) {
      this.kvService
        .updateFieldAccessPolicy(editing.id, {
          accessLevel: this.newFAPolicyForm.accessLevel,
          maskPattern: this.newFAPolicyForm.maskPattern,
          partialLength: this.newFAPolicyForm.partialLength,
          description: this.newFAPolicyForm.description || undefined,
        })
        .subscribe({
          next: () => {
            this.showStatus('Đã cập nhật policy', 'success');
            this.showFAPolicyDialog.set(false);
            this.loadFieldAccessPolicies();
            this.loading.set(false);
          },
          error: () => {
            this.showStatus('Lỗi cập nhật policy', 'error');
            this.loading.set(false);
          },
        });
    } else {
      const requests = this.newFAPolicyForm.fieldNames.map((fieldName) =>
        this.kvService.createFieldAccessPolicy({
          tableName: this.newFAPolicyForm.tableName,
          fieldName,
          role: this.newFAPolicyForm.role,
          accessLevel: this.newFAPolicyForm.accessLevel,
          maskPattern: this.newFAPolicyForm.maskPattern,
          partialLength: this.newFAPolicyForm.partialLength,
          description: this.newFAPolicyForm.description || undefined,
        }),
      );
      forkJoin(requests).subscribe({
        next: () => {
          this.showStatus(`Đã thêm ${requests.length} policy`, 'success');
          this.showFAPolicyDialog.set(false);
          this.loadFieldAccessPolicies();
          this.loading.set(false);
        },
        error: () => {
          this.showStatus('Lỗi tạo policy', 'error');
          this.loading.set(false);
        },
      });
    }
  }

  deleteFAPolicy(policy: FieldAccessPolicyResponse) {
    this.kvService.deleteFieldAccessPolicy(policy.id).subscribe({
      next: () => {
        this.showStatus('Đã xóa policy', 'success');
        this.loadFieldAccessPolicies();
      },
      error: () => this.showStatus('Lỗi xóa policy', 'error'),
    });
  }

  getAccessLevelBadgeClass(level: string): string {
    const map: Record<string, string> = {
      full: 'badge-success',
      partial: 'badge-warning',
      masked: 'badge-info',
      none: 'badge-danger',
    };
    return map[level] || 'badge-secondary';
  }

  getAccessLevelLabel(level: string): string {
    const labels: Record<string, string> = {
      full: 'Toàn quyền',
      partial: 'Một phần',
      masked: 'Che dấu',
      none: 'Không truy cập',
    };
    return labels[level] || level;
  }

  // ─── Security Dashboard ──────────────────────────
  loadSecurityDashboard() {
    this.kvService.getSecurityDashboard().subscribe({
      next: (d) => this.securityDashboard.set(d),
      error: () => {},
    });
  }

  // ─── Helpers ────────────────────────────────────
  formatDate(dateStr: string | null): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  refresh() {
    this.loadVaultStatus();
    this.loadHealth();
    const tab = this.activeTab();
    if (tab === 'secrets') this.loadSecrets();
    if (tab === 'zero-trust') this.loadZTPolicies();
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMessage.set({ text, type });
    setTimeout(() => this.statusMessage.set(null), 4000);
  }
}
