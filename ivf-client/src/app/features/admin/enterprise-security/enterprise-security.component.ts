import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { EnterpriseSecurityService } from '../../../core/services/enterprise-security.service';
import { UserService } from '../../../core/services/user.service';
import {
  PermissionDefinitionService,
  PermissionGroupDto,
} from '../../../core/services/permission-definition.service';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import {
  ConditionalAccessPolicy,
  IncidentResponseRule,
  SecurityIncident,
  DataRetentionPolicy,
  ImpersonationRequest,
  PermissionDelegation,
  UserBehaviorProfile,
  NotificationPreference,
} from '../../../core/models/enterprise-security.model';

type TabKey =
  | 'conditional-access'
  | 'incidents'
  | 'incident-rules'
  | 'data-retention'
  | 'impersonation'
  | 'delegation'
  | 'behavior'
  | 'notifications';

@Component({
  selector: 'app-enterprise-security',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './enterprise-security.component.html',
  styleUrls: ['./enterprise-security.component.scss'],
})
export class EnterpriseSecurityComponent implements OnInit {
  activeTab = signal<TabKey>('conditional-access');

  // Data signals
  policies = signal<ConditionalAccessPolicy[]>([]);
  incidentRules = signal<IncidentResponseRule[]>([]);
  incidents = signal<SecurityIncident[]>([]);
  incidentsTotalCount = signal(0);
  incidentsPage = signal(1);
  retentionPolicies = signal<DataRetentionPolicy[]>([]);
  impersonationRequests = signal<ImpersonationRequest[]>([]);
  delegations = signal<PermissionDelegation[]>([]);
  behaviorProfiles = signal<UserBehaviorProfile[]>([]);
  notificationPrefs = signal<NotificationPreference[]>([]);

  loading = signal(false);
  error = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  tabs: { key: TabKey; label: string; icon: string }[] = [
    { key: 'conditional-access', label: 'Truy cập có ĐK', icon: '🛡️' },
    { key: 'incidents', label: 'Sự cố', icon: '🚨' },
    { key: 'incident-rules', label: 'Quy tắc ứng phó', icon: '⚙️' },
    { key: 'data-retention', label: 'Lưu giữ dữ liệu', icon: '🗄️' },
    { key: 'impersonation', label: 'Mạo danh', icon: '👤' },
    { key: 'delegation', label: 'Ủy quyền', icon: '🔑' },
    { key: 'behavior', label: 'Phân tích hành vi', icon: '📊' },
    { key: 'notifications', label: 'Thông báo', icon: '🔔' },
  ];

  // ─── Create/Edit Form State ───

  showPolicyModal = false;
  policyForm: any = this.getDefaultPolicyForm();
  editingPolicyId: string | null = null;

  showRuleModal = false;
  ruleForm: any = this.getDefaultRuleForm();
  editingRuleId: string | null = null;

  showRetentionModal = false;
  retentionForm: any = this.getDefaultRetentionForm();
  editingRetentionId: string | null = null;

  showImpersonationModal = false;
  impersonationForm: any = { targetUserId: '', reason: '' };

  showDelegationModal = false;
  delegationForm: any = this.getDefaultDelegationForm();

  showNotifModal = false;
  notifForm: any = { userId: '', channel: 'in_app', eventTypes: '' };

  // ─── User Search State ───
  userSearchResults = signal<any[]>([]);
  userSearchQuery = '';
  private userSearch$ = new Subject<string>();
  showUserDropdown: { [key: string]: boolean } = {};

  // ─── Permission Groups ───
  permissionGroups = signal<PermissionGroupDto[]>([]);
  selectedPermissions: string[] = [];

  constructor(
    private enterpriseSecurity: EnterpriseSecurityService,
    private userService: UserService,
    private permissionService: PermissionDefinitionService,
  ) {}

  ngOnInit() {
    this.loadTabData('conditional-access');
    this.userSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) => this.userService.getUsers(q, undefined, true, 1, 10)),
      )
      .subscribe({
        next: (res: any) => this.userSearchResults.set(res.items || []),
        error: () => this.userSearchResults.set([]),
      });
    this.permissionService.loadPermissionGroups().subscribe({
      next: (groups) => this.permissionGroups.set(groups),
    });
  }

  switchTab(tab: TabKey) {
    this.activeTab.set(tab);
    this.loadTabData(tab);
  }

  loadTabData(tab: TabKey) {
    this.loading.set(true);
    this.error.set(null);
    this.successMsg.set(null);

    switch (tab) {
      case 'conditional-access':
        this.enterpriseSecurity.getConditionalAccessPolicies().subscribe({
          next: (data) => {
            this.policies.set(data);
            this.loading.set(false);
          },
          error: (err) => this.handleError(err),
        });
        break;
      case 'incident-rules':
        this.enterpriseSecurity.getIncidentRules().subscribe({
          next: (data) => {
            this.incidentRules.set(data);
            this.loading.set(false);
          },
          error: (err) => this.handleError(err),
        });
        break;
      case 'incidents':
        this.loadIncidents();
        break;
      case 'data-retention':
        this.enterpriseSecurity.getDataRetentionPolicies().subscribe({
          next: (data) => {
            this.retentionPolicies.set(data);
            this.loading.set(false);
          },
          error: (err) => this.handleError(err),
        });
        break;
      case 'impersonation':
        this.enterpriseSecurity.getImpersonationRequests().subscribe({
          next: (data) => {
            this.impersonationRequests.set(data.items);
            this.loading.set(false);
          },
          error: (err) => this.handleError(err),
        });
        break;
      case 'delegation':
        this.enterpriseSecurity.getActiveDelegations().subscribe({
          next: (data) => {
            this.delegations.set(data);
            this.loading.set(false);
          },
          error: (err) => this.handleError(err),
        });
        break;
      case 'behavior':
        this.enterpriseSecurity.getBehaviorProfiles().subscribe({
          next: (data) => {
            this.behaviorProfiles.set(data);
            this.loading.set(false);
          },
          error: (err) => this.handleError(err),
        });
        break;
      case 'notifications':
        this.loading.set(false);
        break;
    }
  }

  loadIncidents(page = 1) {
    this.incidentsPage.set(page);
    this.enterpriseSecurity.getIncidents(page, 20).subscribe({
      next: (data) => {
        this.incidents.set(data.items);
        this.incidentsTotalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── Default Form Data ───

  getDefaultPolicyForm() {
    return {
      name: '',
      description: '',
      priority: 100,
      action: 'RequireMfa',
      requireMfa: true,
      blockVpnTor: false,
      requireCompliantDevice: false,
      maxRiskLevel: 50,
      targetRoles: '',
      allowedCountries: '',
      blockedCountries: '',
      allowedIpRanges: '',
    };
  }

  getDefaultRuleForm() {
    return {
      name: '',
      description: '',
      priority: 100,
      triggerEventTypes: '',
      triggerSeverities: 'High,Critical',
      actions: 'notify_admin',
      incidentSeverity: 'High',
      triggerThreshold: 5,
      triggerWindowMinutes: 60,
    };
  }

  getDefaultRetentionForm() {
    return { entityType: 'SecurityEvent', retentionDays: 365, action: 'Purge' };
  }

  getDefaultDelegationForm() {
    return { toUserId: '', permissions: '', reason: '', validUntil: '' };
  }

  // ─── Conditional Access CRUD ───

  openPolicyModal() {
    this.policyForm = this.getDefaultPolicyForm();
    this.editingPolicyId = null;
    this.showPolicyModal = true;
  }

  closePolicyModal() {
    this.showPolicyModal = false;
    this.editingPolicyId = null;
  }

  editPolicy(policy: ConditionalAccessPolicy) {
    this.policyForm = {
      name: policy.name,
      description: policy.description || '',
      priority: policy.priority,
      action: policy.action,
      requireMfa: policy.requireMfa,
      blockVpnTor: policy.blockVpnTor,
      requireCompliantDevice: policy.requireCompliantDevice,
      maxRiskLevel: policy.maxRiskLevel,
      targetRoles: Array.isArray(policy.targetRoles)
        ? policy.targetRoles.join(', ')
        : policy.targetRoles || '',
      allowedCountries: Array.isArray(policy.allowedCountries)
        ? policy.allowedCountries.join(', ')
        : policy.allowedCountries || '',
      blockedCountries: Array.isArray(policy.blockedCountries)
        ? policy.blockedCountries.join(', ')
        : policy.blockedCountries || '',
      allowedIpRanges: Array.isArray(policy.allowedIpRanges)
        ? policy.allowedIpRanges.join(', ')
        : policy.allowedIpRanges || '',
    };
    this.editingPolicyId = policy.id;
    this.showPolicyModal = true;
  }

  savePolicy() {
    this.loading.set(true);
    const f = this.policyForm;
    const req = {
      name: f.name,
      description: f.description || undefined,
      priority: f.priority,
      action: f.action,
      requireMfa: f.requireMfa,
      blockVpnTor: f.blockVpnTor,
      requireCompliantDevice: f.requireCompliantDevice,
      maxRiskLevel: f.maxRiskLevel,
      targetRoles: f.targetRoles
        ? f.targetRoles
            .split(',')
            .map((s: string) => s.trim())
            .filter(Boolean)
        : undefined,
      allowedCountries: f.allowedCountries
        ? f.allowedCountries
            .split(',')
            .map((s: string) => s.trim())
            .filter(Boolean)
        : undefined,
      blockedCountries: f.blockedCountries
        ? f.blockedCountries
            .split(',')
            .map((s: string) => s.trim())
            .filter(Boolean)
        : undefined,
      allowedIpRanges: f.allowedIpRanges
        ? f.allowedIpRanges
            .split(',')
            .map((s: string) => s.trim())
            .filter(Boolean)
        : undefined,
    };
    const action$ = this.editingPolicyId
      ? this.enterpriseSecurity.updateConditionalAccessPolicy(this.editingPolicyId, req)
      : this.enterpriseSecurity.createConditionalAccessPolicy(req);
    action$.subscribe({
      next: () => {
        this.closePolicyModal();
        this.showSuccess(this.editingPolicyId ? 'Đã cập nhật chính sách' : 'Đã tạo chính sách');
        this.editingPolicyId = null;
        this.loadTabData('conditional-access');
      },
      error: (err) => this.handleError(err),
    });
  }

  togglePolicy(policy: ConditionalAccessPolicy) {
    const action = policy.isEnabled
      ? this.enterpriseSecurity.disableConditionalAccessPolicy(policy.id)
      : this.enterpriseSecurity.enableConditionalAccessPolicy(policy.id);
    action.subscribe({
      next: () => this.loadTabData('conditional-access'),
      error: (err) => this.handleError(err),
    });
  }

  deletePolicy(policy: ConditionalAccessPolicy) {
    if (!confirm(`Xóa chính sách "${policy.name}"?`)) return;
    this.enterpriseSecurity.deleteConditionalAccessPolicy(policy.id).subscribe({
      next: () => {
        this.showSuccess('Đã xóa chính sách');
        this.loadTabData('conditional-access');
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── Incident Rules CRUD ───

  openRuleModal() {
    this.ruleForm = this.getDefaultRuleForm();
    this.editingRuleId = null;
    this.showRuleModal = true;
  }

  closeRuleModal() {
    this.showRuleModal = false;
    this.editingRuleId = null;
  }

  editRule(rule: IncidentResponseRule) {
    this.ruleForm = {
      name: rule.name,
      description: rule.description || '',
      priority: rule.priority,
      triggerEventTypes: Array.isArray(rule.triggerEventTypes)
        ? rule.triggerEventTypes.join(', ')
        : rule.triggerEventTypes || '',
      triggerSeverities: Array.isArray(rule.triggerSeverities)
        ? rule.triggerSeverities.join(', ')
        : rule.triggerSeverities || '',
      actions: Array.isArray(rule.actions) ? rule.actions.join(', ') : rule.actions || '',
      incidentSeverity: rule.incidentSeverity,
      triggerThreshold: rule.triggerThreshold || 5,
      triggerWindowMinutes: rule.triggerWindowMinutes || 60,
    };
    this.editingRuleId = rule.id;
    this.showRuleModal = true;
  }

  saveRule() {
    this.loading.set(true);
    const f = this.ruleForm;
    const req = {
      name: f.name,
      description: f.description || undefined,
      priority: f.priority,
      triggerEventTypes: f.triggerEventTypes
        .split(',')
        .map((s: string) => s.trim())
        .filter(Boolean),
      triggerSeverities: f.triggerSeverities
        .split(',')
        .map((s: string) => s.trim())
        .filter(Boolean),
      actions: f.actions
        .split(',')
        .map((s: string) => s.trim())
        .filter(Boolean),
      incidentSeverity: f.incidentSeverity,
      triggerThreshold: f.triggerThreshold || undefined,
      triggerWindowMinutes: f.triggerWindowMinutes || undefined,
    };
    const action$ = this.editingRuleId
      ? this.enterpriseSecurity.updateIncidentRule(this.editingRuleId, req)
      : this.enterpriseSecurity.createIncidentRule(req);
    action$.subscribe({
      next: () => {
        this.closeRuleModal();
        this.showSuccess(this.editingRuleId ? 'Đã cập nhật quy tắc' : 'Đã tạo quy tắc');
        this.editingRuleId = null;
        this.loadTabData('incident-rules');
      },
      error: (err) => this.handleError(err),
    });
  }

  deleteIncidentRule(rule: IncidentResponseRule) {
    if (!confirm(`Xóa quy tắc "${rule.name}"?`)) return;
    this.enterpriseSecurity.deleteIncidentRule(rule.id).subscribe({
      next: () => {
        this.showSuccess('Đã xóa quy tắc');
        this.loadTabData('incident-rules');
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── Incidents ───

  investigateIncident(incident: SecurityIncident) {
    this.enterpriseSecurity.investigateIncident(incident.id).subscribe({
      next: () => this.loadIncidents(this.incidentsPage()),
      error: (err) => this.handleError(err),
    });
  }

  resolveIncident(incident: SecurityIncident) {
    const resolution = prompt('Ghi chú giải quyết:');
    if (!resolution) return;
    this.enterpriseSecurity.resolveIncident(incident.id, resolution).subscribe({
      next: () => this.loadIncidents(this.incidentsPage()),
      error: (err) => this.handleError(err),
    });
  }

  closeIncident(incident: SecurityIncident) {
    this.enterpriseSecurity.closeIncident(incident.id).subscribe({
      next: () => this.loadIncidents(this.incidentsPage()),
      error: (err) => this.handleError(err),
    });
  }

  // ─── Data Retention CRUD ───

  openRetentionModal() {
    this.retentionForm = this.getDefaultRetentionForm();
    this.editingRetentionId = null;
    this.showRetentionModal = true;
  }

  closeRetentionModal() {
    this.showRetentionModal = false;
    this.editingRetentionId = null;
  }

  editRetention(policy: DataRetentionPolicy) {
    this.retentionForm = {
      entityType: policy.entityType,
      retentionDays: policy.retentionDays,
      action: policy.action,
    };
    this.editingRetentionId = policy.id;
    this.showRetentionModal = true;
  }

  saveRetention() {
    this.loading.set(true);
    const action$ = this.editingRetentionId
      ? this.enterpriseSecurity.updateDataRetentionPolicy(this.editingRetentionId, {
          retentionDays: this.retentionForm.retentionDays,
          action: this.retentionForm.action,
        })
      : this.enterpriseSecurity.createDataRetentionPolicy({
          entityType: this.retentionForm.entityType,
          retentionDays: this.retentionForm.retentionDays,
          action: this.retentionForm.action,
        });
    action$.subscribe({
      next: () => {
        this.closeRetentionModal();
        this.showSuccess(
          this.editingRetentionId ? 'Đã cập nhật chính sách lưu giữ' : 'Đã tạo chính sách lưu giữ',
        );
        this.editingRetentionId = null;
        this.loadTabData('data-retention');
      },
      error: (err) => this.handleError(err),
    });
  }

  deleteRetentionPolicy(policy: DataRetentionPolicy) {
    if (!confirm(`Xóa chính sách lưu giữ cho "${policy.entityType}"?`)) return;
    this.enterpriseSecurity.deleteDataRetentionPolicy(policy.id).subscribe({
      next: () => {
        this.showSuccess('Đã xóa chính sách lưu giữ');
        this.loadTabData('data-retention');
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── Impersonation CRUD ───

  openImpersonationModal() {
    this.impersonationForm = { targetUserId: '', reason: '' };
    this.userSearchQuery = '';
    this.showImpersonationModal = true;
  }

  closeImpersonationModal() {
    this.showImpersonationModal = false;
  }

  saveImpersonation() {
    this.loading.set(true);
    this.enterpriseSecurity
      .createImpersonationRequest({
        targetUserId: this.impersonationForm.targetUserId,
        reason: this.impersonationForm.reason,
      })
      .subscribe({
        next: () => {
          this.closeImpersonationModal();
          this.showSuccess('Đã tạo yêu cầu');
          this.loadTabData('impersonation');
        },
        error: (err) => this.handleError(err),
      });
  }

  approveImpersonation(request: ImpersonationRequest) {
    this.enterpriseSecurity.approveImpersonation(request.id, 30).subscribe({
      next: () => {
        this.showSuccess('Đã phê duyệt yêu cầu');
        this.loadTabData('impersonation');
      },
      error: (err) => this.handleError(err),
    });
  }

  denyImpersonation(request: ImpersonationRequest) {
    const reason = prompt('Lý do từ chối:');
    this.enterpriseSecurity.denyImpersonation(request.id, reason || undefined).subscribe({
      next: () => this.loadTabData('impersonation'),
      error: (err) => this.handleError(err),
    });
  }

  endImpersonation(request: ImpersonationRequest) {
    this.enterpriseSecurity.endImpersonation(request.id).subscribe({
      next: () => this.loadTabData('impersonation'),
      error: (err) => this.handleError(err),
    });
  }

  // ─── Delegation CRUD ───

  openDelegationModal() {
    this.delegationForm = this.getDefaultDelegationForm();
    this.selectedPermissions = [];
    this.userSearchQuery = '';
    this.showDelegationModal = true;
  }

  closeDelegationModal() {
    this.showDelegationModal = false;
  }

  saveDelegation() {
    this.loading.set(true);
    const f = this.delegationForm;
    this.enterpriseSecurity
      .createDelegation({
        toUserId: f.toUserId,
        permissions: [...this.selectedPermissions],
        reason: f.reason || undefined,
        validUntil: f.validUntil,
      })
      .subscribe({
        next: () => {
          this.closeDelegationModal();
          this.showSuccess('Đã tạo ủy quyền');
          this.loadTabData('delegation');
        },
        error: (err) => this.handleError(err),
      });
  }

  revokeDelegation(delegation: PermissionDelegation) {
    if (!confirm('Thu hồi ủy quyền này?')) return;
    this.enterpriseSecurity.revokeDelegation(delegation.id).subscribe({
      next: () => {
        this.showSuccess('Đã thu hồi ủy quyền');
        this.loadTabData('delegation');
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── Notification Prefs CRUD ───

  openNotifModal() {
    this.notifForm = { userId: '', channel: 'in_app', eventTypes: '' };
    this.userSearchQuery = '';
    this.showNotifModal = true;
  }

  closeNotifModal() {
    this.showNotifModal = false;
  }

  saveNotif() {
    this.loading.set(true);
    this.enterpriseSecurity
      .createNotificationPreference({
        userId: this.notifForm.userId,
        channel: this.notifForm.channel,
        eventTypes: this.notifForm.eventTypes
          .split(',')
          .map((s: string) => s.trim())
          .filter(Boolean),
      })
      .subscribe({
        next: () => {
          this.closeNotifModal();
          this.showSuccess('Đã tạo tùy chọn thông báo');
          this.loadTabData('notifications');
        },
        error: (err) => this.handleError(err),
      });
  }

  loadNotifPrefs(userId: string) {
    if (!userId) return;
    this.loading.set(true);
    this.enterpriseSecurity.getNotificationPreferences(userId).subscribe({
      next: (data) => {
        this.notificationPrefs.set(data);
        this.loading.set(false);
      },
      error: (err) => this.handleError(err),
    });
  }

  deleteNotifPref(pref: NotificationPreference) {
    if (!confirm('Xóa tùy chọn thông báo này?')) return;
    this.enterpriseSecurity.deleteNotificationPreference(pref.id).subscribe({
      next: () => {
        this.showSuccess('Đã xóa tùy chọn thông báo');
        this.loadNotifPrefs(pref.userId);
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── Behavior ───

  refreshProfile(profile: UserBehaviorProfile) {
    this.enterpriseSecurity.refreshBehaviorProfile(profile.userId).subscribe({
      next: () => {
        this.showSuccess('Đã làm mới hồ sơ');
        this.loadTabData('behavior');
      },
      error: (err) => this.handleError(err),
    });
  }

  // ─── User Search ───

  onUserSearch(query: string, field: string) {
    this.userSearchQuery = query;
    if (query.length >= 2) {
      this.showUserDropdown[field] = true;
      this.userSearch$.next(query);
    } else {
      this.showUserDropdown[field] = false;
      this.userSearchResults.set([]);
    }
  }

  selectUser(user: any, form: any, field: string, dropdownKey: string) {
    form[field] = user.id;
    this.userSearchQuery = user.username;
    this.showUserDropdown[dropdownKey] = false;
    this.userSearchResults.set([]);
  }

  selectUserForNotifLookup(user: any, inputEl: HTMLInputElement) {
    inputEl.value = user.username;
    this.showUserDropdown['notifLookup'] = false;
    this.userSearchResults.set([]);
    this.loadNotifPrefs(user.id);
  }

  onNotifLookupSearch(query: string) {
    if (query.length >= 2) {
      this.showUserDropdown['notifLookup'] = true;
      this.userSearch$.next(query);
    } else {
      this.showUserDropdown['notifLookup'] = false;
      this.userSearchResults.set([]);
    }
  }

  getUserDisplayName(form: any, field: string): string {
    return form[field] ? this.userSearchQuery : '';
  }

  // ─── Permission Selection ───

  togglePermission(code: string) {
    const idx = this.selectedPermissions.indexOf(code);
    if (idx >= 0) {
      this.selectedPermissions.splice(idx, 1);
    } else {
      this.selectedPermissions.push(code);
    }
    this.delegationForm.permissions = this.selectedPermissions.join(',');
  }

  isPermissionSelected(code: string): boolean {
    return this.selectedPermissions.includes(code);
  }

  // ─── Helpers ───

  getSeverityClass(severity: string): string {
    switch (severity?.toLowerCase()) {
      case 'critical':
        return 'badge-critical';
      case 'high':
        return 'badge-high';
      case 'medium':
        return 'badge-medium';
      case 'low':
        return 'badge-low';
      default:
        return 'badge-info';
    }
  }

  getStatusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'open':
        return 'badge-critical';
      case 'investigating':
        return 'badge-medium';
      case 'resolved':
        return 'badge-low';
      case 'closed':
        return 'badge-info';
      case 'falsepositive':
        return 'badge-info';
      case 'pending':
        return 'badge-medium';
      case 'approved':
        return 'badge-low';
      case 'denied':
        return 'badge-critical';
      case 'active':
        return 'badge-high';
      default:
        return 'badge-info';
    }
  }

  private showSuccess(msg: string) {
    this.successMsg.set(msg);
    setTimeout(() => this.successMsg.set(null), 3000);
  }

  private handleError(err: unknown) {
    this.loading.set(false);
    if (err instanceof HttpErrorResponse) {
      const body = err.error;
      const detail = body?.message || body?.error || body?.title || JSON.stringify(body);
      this.error.set(`${err.status}: ${detail}`);
    } else if (err instanceof Error) {
      this.error.set(err.message);
    } else {
      this.error.set('An unexpected error occurred');
    }
  }
}
