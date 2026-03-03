import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EnterpriseUserService } from '../../../core/services/enterprise-user.service';
import { UserService } from '../../../core/services/user.service';
import { PermissionDefinitionService } from '../../../core/services/permission-definition.service';
import {
  UserAnalytics,
  UserDetail,
  UserSession,
  UserGroup,
  UserGroupDetail,
  UserLoginHistory,
  UserConsent,
  GroupConsentStatus,
  GROUP_TYPES,
  CONSENT_TYPES,
  LOGIN_METHOD_LABELS,
  DEVICE_TYPE_ICONS,
  MEMBER_ROLES,
} from '../../../core/models/enterprise-user.model';

type TabType = 'analytics' | 'users' | 'groups' | 'sessions' | 'login-history' | 'consent';

@Component({
  selector: 'app-enterprise-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './enterprise-users.component.html',
  styleUrls: ['./enterprise-users.component.scss'],
})
export class EnterpriseUsersComponent implements OnInit {
  // ─── State ───
  activeTab = signal<TabType>('analytics');
  loading = signal(false);

  // Analytics
  analytics = signal<UserAnalytics | null>(null);

  // Users (extended from existing)
  users = signal<any[]>([]);
  roles = signal<string[]>([]);
  userSearch = '';
  userRoleFilter = '';
  userStatusFilter: boolean | undefined = undefined;
  userPage = 1;
  userPageSize = 20;
  userTotal = 0;

  // User Detail Modal
  showUserDetail = false;
  selectedUserDetail = signal<UserDetail | null>(null);
  selectedUserSessions = signal<UserSession[]>([]);
  selectedUserConsents = signal<UserConsent[]>([]);
  userDetailTab = signal<'overview' | 'sessions' | 'permissions' | 'login-history' | 'consent'>(
    'overview',
  );
  userDetailLoginHistory = signal<UserLoginHistory[]>([]);

  // Groups
  groups = signal<UserGroup[]>([]);
  groupSearch = '';
  groupTypeFilter = '';
  groupPage = 1;
  groupTotal = 0;
  showGroupModal = false;
  editingGroup: UserGroup | null = null;
  groupFormData: {
    name: string;
    displayName: string;
    description: string;
    groupType: string;
    parentGroupId: string | null;
  } = {
    name: '',
    displayName: '',
    description: '',
    groupType: 'team',
    parentGroupId: null,
  };
  showGroupDetail = false;
  selectedGroupDetail = signal<UserGroupDetail | null>(null);
  showAddMemberModal = false;
  newMemberUserId = '';
  newMemberRole = 'member';
  memberSearchQuery = '';
  memberSearchResults: any[] = [];
  selectedMemberUser: any = null;

  // Group Permissions Modal
  showGroupPermissionsModal = false;
  groupPermissions: string[] = [];
  permissionGroups: { name: string; permissions: string[] }[] = [];
  private permissionDisplayNames: Record<string, string> = {};

  showGroupConsentModal = false;

  // Group Consent
  groupConsentStatus = signal<GroupConsentStatus | null>(null);
  groupConsentForm = { consentType: '', consentVersion: '' };
  consentGroupId = '';

  // Consent Sub-tab
  consentSubTab = signal<'user' | 'group'>('user');

  // Login History
  loginHistory = signal<UserLoginHistory[]>([]);
  loginSearch = '';
  loginSuccessFilter: boolean | undefined = undefined;
  loginSuspiciousFilter: boolean | undefined = undefined;
  loginPage = 1;
  loginTotal = 0;

  // Sessions (global)
  allSessions = signal<UserSession[]>([]);

  // Consent
  consents = signal<UserConsent[]>([]);
  consentUserId = '';
  consentForm = { consentType: '', consentVersion: '' };

  // User Create/Edit Modal
  showUserModal = false;
  editingUser: any = null;
  changePassword = false;
  userFormData: any = {
    username: '',
    password: '',
    fullName: '',
    role: 'Doctor',
    department: '',
    isActive: true,
  };

  // Doctor Modal
  showDoctorModal = false;
  selectedDoctorUser: any = null;
  doctorFormData: any = {
    specialty: 'IVF',
    licenseNumber: '',
    roomNumber: '',
    maxPatientsPerDay: 20,
  };

  // User Permissions Modal
  showPermissionsModal = false;
  selectedPermissionUser: any = null;
  userPermissions: string[] = [];

  // Constants
  readonly groupTypes = GROUP_TYPES;
  readonly consentTypes = CONSENT_TYPES;
  readonly loginMethodLabels = LOGIN_METHOD_LABELS;
  readonly deviceTypeIcons = DEVICE_TYPE_ICONS;
  readonly memberRoles = MEMBER_ROLES;

  // Computed
  mfaRate = computed(() => {
    const a = this.analytics();
    if (!a || a.totalUsers === 0) return 0;
    return Math.round((a.mfaEnabledCount / a.activeUsers) * 100);
  });

  loginSuccessRate = computed(() => {
    const a = this.analytics();
    if (!a || a.totalLogins24h === 0) return 100;
    return Math.round(((a.totalLogins24h - a.failedLogins24h) / a.totalLogins24h) * 100);
  });

  constructor(
    private enterpriseService: EnterpriseUserService,
    private userService: UserService,
    private permDefService: PermissionDefinitionService,
  ) {}

  ngOnInit() {
    this.loadAnalytics();
    this.loadPermissionDefinitions();
  }

  switchTab(tab: TabType) {
    this.activeTab.set(tab);
    switch (tab) {
      case 'analytics':
        this.loadAnalytics();
        break;
      case 'users':
        this.loadUsers();
        break;
      case 'groups':
        this.loadGroups();
        break;
      case 'login-history':
        this.loadLoginHistory();
        break;
      case 'consent':
        this.loadUsers();
        this.loadGroups();
        break;
    }
  }

  // ═══════════════════════════════════════════════════
  // ANALYTICS
  // ═══════════════════════════════════════════════════

  loadAnalytics() {
    this.loading.set(true);
    this.enterpriseService.getAnalytics().subscribe({
      next: (data) => {
        this.analytics.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  getRoleEntries(): [string, number][] {
    const a = this.analytics();
    return a ? Object.entries(a.usersByRole) : [];
  }

  getMaxLoginTrend(): number {
    const a = this.analytics();
    if (!a?.loginTrend7Days?.length) return 1;
    return Math.max(...a.loginTrend7Days.map((t) => t.successCount + t.failedCount)) || 1;
  }

  // ═══════════════════════════════════════════════════
  // USERS
  // ═══════════════════════════════════════════════════

  loadUsers() {
    this.loading.set(true);
    this.userService
      .getUsers(
        this.userSearch,
        this.userRoleFilter,
        this.userStatusFilter,
        this.userPage,
        this.userPageSize,
      )
      .subscribe({
        next: (res) => {
          this.users.set(res.items);
          this.userTotal = res.total;
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    this.userService.getRoles().subscribe((roles) => this.roles.set(roles));
  }

  openUserDetail(user: any) {
    this.showUserDetail = true;
    this.userDetailTab.set('overview');
    this.loading.set(true);
    this.enterpriseService.getUserDetail(user.id).subscribe({
      next: (detail) => {
        this.selectedUserDetail.set(detail);
        this.loading.set(false);
        this.loadUserSessions(user.id);
        this.loadUserConsents(user.id);
        this.loadUserDetailLoginHistory(user.id);
      },
      error: () => this.loading.set(false),
    });
  }

  closeUserDetail() {
    this.showUserDetail = false;
    this.selectedUserDetail.set(null);
  }

  loadUserSessions(userId: string) {
    this.enterpriseService.getUserSessions(userId, false).subscribe({
      next: (sessions) => this.selectedUserSessions.set(sessions),
    });
  }

  loadUserConsents(userId: string) {
    this.enterpriseService.getUserConsents(userId).subscribe({
      next: (consents) => this.selectedUserConsents.set(consents),
    });
  }

  grantConsent() {
    const detail = this.selectedUserDetail();
    if (!detail || !this.consentForm.consentType) return;
    this.enterpriseService
      .grantConsent({
        userId: detail.id,
        consentType: this.consentForm.consentType,
        consentVersion: this.consentForm.consentVersion || undefined,
        ipAddress: window.location.hostname,
        userAgent: navigator.userAgent,
      })
      .subscribe({
        next: () => {
          this.loadUserConsents(detail.id);
          this.consentForm = { consentType: '', consentVersion: '' };
        },
      });
  }

  revokeConsent(consentId: string) {
    if (!confirm('Xác nhận thu hồi đồng ý này?')) return;
    const detail = this.selectedUserDetail();
    this.enterpriseService.revokeConsent(consentId, 'Admin revoked').subscribe({
      next: () => {
        if (detail) this.loadUserConsents(detail.id);
      },
    });
  }

  // ─── Consent Tab (main level) ───

  loadConsentForUser() {
    if (!this.consentUserId) {
      this.consents.set([]);
      return;
    }
    this.enterpriseService.getUserConsents(this.consentUserId).subscribe({
      next: (data) => this.consents.set(data),
      error: () => this.consents.set([]),
    });
  }

  grantConsentFromTab() {
    if (!this.consentUserId || !this.consentForm.consentType) return;
    this.enterpriseService
      .grantConsent({
        userId: this.consentUserId,
        consentType: this.consentForm.consentType,
        consentVersion: this.consentForm.consentVersion || undefined,
        ipAddress: window.location.hostname,
        userAgent: navigator.userAgent,
      })
      .subscribe({
        next: () => {
          this.loadConsentForUser();
          this.consentForm = { consentType: '', consentVersion: '' };
        },
      });
  }

  revokeConsentFromTab(consentId: string) {
    if (!confirm('Xác nhận thu hồi đồng ý này?')) return;
    this.enterpriseService.revokeConsent(consentId, 'Admin revoked').subscribe({
      next: () => this.loadConsentForUser(),
    });
  }

  revokeConsentByType(consentType: string) {
    const consent = this.consents().find((c) => c.consentType === consentType && c.isGranted);
    if (consent) this.revokeConsentFromTab(consent.id);
  }

  getConsentStatusForType(type: string): 'granted' | 'revoked' | 'none' {
    const list = this.consents().filter((c) => c.consentType === type);
    if (list.length === 0) return 'none';
    const latest = list.sort(
      (a, b) => new Date(b.consentedAt).getTime() - new Date(a.consentedAt).getTime(),
    )[0];
    return latest.isGranted ? 'granted' : 'revoked';
  }

  loadUserDetailLoginHistory(userId: string) {
    this.enterpriseService.getLoginHistory(userId, 1, 20).subscribe({
      next: (res) => this.userDetailLoginHistory.set(res.items),
    });
  }

  revokeSession(sessionId: string) {
    if (!confirm('Xác nhận thu hồi phiên đăng nhập này?')) return;
    this.enterpriseService.revokeSession(sessionId, 'Admin revoked').subscribe({
      next: () => {
        const detail = this.selectedUserDetail();
        if (detail) this.loadUserSessions(detail.id);
      },
    });
  }

  revokeAllSessions(userId: string) {
    if (!confirm('Xác nhận thu hồi TẤT CẢ phiên đăng nhập?')) return;
    this.enterpriseService.revokeAllSessions(userId, 'Admin revoked all').subscribe({
      next: (result) => {
        alert(`Đã thu hồi ${result.revokedCount} phiên`);
        this.loadUserSessions(userId);
      },
    });
  }

  openUserModal(user: any = null) {
    this.editingUser = user;
    this.changePassword = false;
    this.userFormData = user
      ? { ...user, password: '' }
      : {
          username: '',
          password: '',
          fullName: '',
          role: 'Doctor',
          department: '',
          isActive: true,
        };
    this.showUserModal = true;
  }

  closeUserModal() {
    this.showUserModal = false;
    this.editingUser = null;
  }

  saveUser() {
    this.loading.set(true);
    if (this.editingUser) {
      const data: any = {
        id: this.editingUser.id,
        fullName: this.userFormData.fullName,
        role: this.userFormData.role,
        department: this.userFormData.department,
        isActive: this.userFormData.isActive,
      };
      if (this.changePassword && this.userFormData.password?.trim()) {
        data.password = this.userFormData.password;
      }
      this.userService.updateUser(this.editingUser.id, data).subscribe({
        next: () => {
          this.loadUsers();
          this.closeUserModal();
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          alert('Lỗi: ' + (err.error?.detail || err.message));
        },
      });
    } else {
      this.userService.createUser(this.userFormData).subscribe({
        next: () => {
          this.loadUsers();
          this.closeUserModal();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  toggleUserStatus(user: any) {
    if (!confirm(`${user.isActive ? 'Khóa' : 'Mở khóa'} tài khoản ${user.username}?`)) return;
    this.userService
      .updateUser(user.id, { ...user, isActive: !user.isActive })
      .subscribe(() => this.loadUsers());
  }

  // ═══════════════════════════════════════════════════
  // DOCTOR MODAL
  // ═══════════════════════════════════════════════════

  openDoctorModal(user: any) {
    this.selectedDoctorUser = user;
    this.doctorFormData = {
      specialty: 'IVF',
      licenseNumber: '',
      roomNumber: '',
      maxPatientsPerDay: 20,
    };
    this.showDoctorModal = true;
  }

  closeDoctorModal() {
    this.showDoctorModal = false;
    this.selectedDoctorUser = null;
  }

  saveDoctorProfile() {
    if (!this.selectedDoctorUser) return;
    this.loading.set(true);
    this.userService
      .createDoctor({ userId: this.selectedDoctorUser.id, ...this.doctorFormData })
      .subscribe({
        next: () => {
          alert('Đã cập nhật thông tin bác sĩ!');
          this.closeDoctorModal();
          this.loading.set(false);
        },
        error: (err) => {
          alert('Lỗi: ' + (err.error?.detail || 'Có thể đã tồn tại.'));
          this.loading.set(false);
        },
      });
  }

  // ═══════════════════════════════════════════════════
  // USER PERMISSIONS MODAL
  // ═══════════════════════════════════════════════════

  openPermissionsModal(user: any) {
    this.selectedPermissionUser = user;
    this.userPermissions = [];
    this.showPermissionsModal = true;
    this.userService.getUserPermissions(user.id).subscribe({
      next: (perms) => (this.userPermissions = perms || []),
      error: () => (this.userPermissions = []),
    });
  }

  closePermissionsModal() {
    this.showPermissionsModal = false;
    this.selectedPermissionUser = null;
    this.userPermissions = [];
  }

  toggleUserPermission(perm: string) {
    if (this.userPermissions.includes(perm)) {
      this.userPermissions = this.userPermissions.filter((p) => p !== perm);
    } else {
      this.userPermissions = [...this.userPermissions, perm];
    }
  }

  saveUserPermissions() {
    if (!this.selectedPermissionUser) return;
    this.loading.set(true);
    this.userService
      .assignPermissions(this.selectedPermissionUser.id, this.userPermissions)
      .subscribe({
        next: () => {
          alert(
            `Đã cập nhật ${this.userPermissions.length} quyền cho ${this.selectedPermissionUser.fullName}`,
          );
          this.closePermissionsModal();
          this.loading.set(false);
        },
        error: (err) => {
          alert('Lỗi: ' + (err.error?.detail || 'Không thể cập nhật'));
          this.loading.set(false);
        },
      });
  }

  // ═══════════════════════════════════════════════════
  // GROUPS
  // ═══════════════════════════════════════════════════

  loadGroups() {
    this.loading.set(true);
    this.enterpriseService
      .getGroups(this.groupSearch, this.groupTypeFilter, this.groupPage)
      .subscribe({
        next: (res) => {
          this.groups.set(res.items);
          this.groupTotal = res.total;
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  openGroupModal(group: UserGroup | null = null) {
    this.editingGroup = group;
    this.groupFormData = group
      ? {
          name: group.name,
          displayName: group.displayName || '',
          description: group.description || '',
          groupType: group.groupType,
          parentGroupId: null,
        }
      : { name: '', displayName: '', description: '', groupType: 'team', parentGroupId: null };
    this.showGroupModal = true;
  }

  closeGroupModal() {
    this.showGroupModal = false;
    this.editingGroup = null;
  }

  saveGroup() {
    this.loading.set(true);
    if (this.editingGroup) {
      this.enterpriseService
        .updateGroup(this.editingGroup.id, { id: this.editingGroup.id, ...this.groupFormData })
        .subscribe({
          next: () => {
            this.loadGroups();
            this.closeGroupModal();
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
    } else {
      const payload = {
        ...this.groupFormData,
        parentGroupId: this.groupFormData.parentGroupId || null,
      };
      this.enterpriseService.createGroup(payload).subscribe({
        next: () => {
          this.loadGroups();
          this.closeGroupModal();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  deleteGroup(group: UserGroup) {
    if (group.isSystem) {
      alert('Không thể xóa nhóm hệ thống');
      return;
    }
    if (!confirm(`Xóa nhóm "${group.displayName || group.name}"?`)) return;
    this.enterpriseService.deleteGroup(group.id).subscribe(() => this.loadGroups());
  }

  openGroupDetail(group: UserGroup) {
    this.showGroupDetail = true;
    this.loading.set(true);
    this.enterpriseService.getGroupDetail(group.id).subscribe({
      next: (detail) => {
        this.selectedGroupDetail.set(detail);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  closeGroupDetail() {
    this.showGroupDetail = false;
    this.selectedGroupDetail.set(null);
  }

  openAddMemberModal() {
    this.newMemberUserId = '';
    this.newMemberRole = 'member';
    this.memberSearchQuery = '';
    this.memberSearchResults = [];
    this.selectedMemberUser = null;
    this.showAddMemberModal = true;
  }

  searchMemberUsers() {
    const q = this.memberSearchQuery.trim();
    if (q.length < 2) {
      this.memberSearchResults = [];
      return;
    }
    this.userService.getUsers(q, undefined, true, 1, 10).subscribe({
      next: (res: any) => (this.memberSearchResults = res.items || []),
      error: () => (this.memberSearchResults = []),
    });
  }

  selectMemberUser(user: any) {
    this.selectedMemberUser = user;
    this.newMemberUserId = user.id;
    this.memberSearchQuery = user.fullName + ' (' + user.username + ')';
    this.memberSearchResults = [];
  }

  clearSelectedMember() {
    this.selectedMemberUser = null;
    this.newMemberUserId = '';
    this.memberSearchQuery = '';
    this.memberSearchResults = [];
  }

  addMember() {
    const detail = this.selectedGroupDetail();
    if (!detail || !this.newMemberUserId) return;
    this.enterpriseService
      .addGroupMember(detail.id, this.newMemberUserId, this.newMemberRole)
      .subscribe({
        next: () => {
          this.showAddMemberModal = false;
          this.openGroupDetail({ id: detail.id } as UserGroup);
        },
        error: (err) => alert('Lỗi: ' + (err.error?.detail || 'Không thể thêm thành viên')),
      });
  }

  removeMember(userId: string) {
    const detail = this.selectedGroupDetail();
    if (!detail || !confirm('Xóa thành viên khỏi nhóm?')) return;
    this.enterpriseService
      .removeGroupMember(detail.id, userId)
      .subscribe(() => this.openGroupDetail({ id: detail.id } as UserGroup));
  }

  openGroupPermissionsModal() {
    const detail = this.selectedGroupDetail();
    if (!detail) return;
    this.groupPermissions = [...detail.permissions];
    this.showGroupPermissionsModal = true;
  }

  toggleGroupPermission(perm: string) {
    if (this.groupPermissions.includes(perm)) {
      this.groupPermissions = this.groupPermissions.filter((p) => p !== perm);
    } else {
      this.groupPermissions = [...this.groupPermissions, perm];
    }
  }

  saveGroupPermissions() {
    const detail = this.selectedGroupDetail();
    if (!detail) return;
    this.enterpriseService.assignGroupPermissions(detail.id, this.groupPermissions).subscribe({
      next: () => {
        this.showGroupPermissionsModal = false;
        this.openGroupDetail({ id: detail.id } as UserGroup);
      },
    });
  }

  openGroupConsentModal() {
    const detail = this.selectedGroupDetail();
    if (!detail) return;
    this.showGroupConsentModal = true;
    this.groupConsentForm = { consentType: '', consentVersion: '' };
    this.loadGroupConsentStatus(detail.id);
  }

  closeGroupConsentModal() {
    this.showGroupConsentModal = false;
  }

  // ─── Group Consent ───

  loadGroupConsentFromTab() {
    if (!this.consentGroupId) {
      this.groupConsentStatus.set(null);
      return;
    }
    this.loadGroupConsentStatus(this.consentGroupId);
  }

  loadGroupConsentStatus(groupId: string) {
    this.enterpriseService.getGroupConsentStatus(groupId).subscribe({
      next: (status) => this.groupConsentStatus.set(status),
    });
  }

  grantGroupConsent() {
    if (!this.consentGroupId || !this.groupConsentForm.consentType) return;
    this.loading.set(true);
    this.enterpriseService
      .grantGroupConsent(
        this.consentGroupId,
        this.groupConsentForm.consentType,
        this.groupConsentForm.consentVersion || undefined,
      )
      .subscribe({
        next: (res) => {
          alert(`Đã cấp đồng ý cho ${res.count} thành viên`);
          this.groupConsentForm = { consentType: '', consentVersion: '' };
          this.loadGroupConsentStatus(this.consentGroupId);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  revokeGroupConsent(consentType: string) {
    if (!this.consentGroupId) return;
    const groupName =
      this.groups().find((g) => g.id === this.consentGroupId)?.displayName || 'nhóm';
    if (!confirm(`Thu hồi đồng ý "${this.getConsentLabel(consentType)}" cho toàn bộ ${groupName}?`))
      return;
    this.loading.set(true);
    this.enterpriseService
      .revokeGroupConsent(this.consentGroupId, consentType, 'Admin revoked for group')
      .subscribe({
        next: (res) => {
          alert(`Đã thu hồi ${res.count} đồng ý`);
          this.loadGroupConsentStatus(this.consentGroupId);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  getGroupConsentProgress(type: string): string {
    const status = this.groupConsentStatus();
    if (!status?.consentSummary[type]) return '0/0';
    const s = status.consentSummary[type];
    return `${s.grantedCount}/${s.totalMembers}`;
  }

  getGroupConsentPercent(type: string): number {
    const status = this.groupConsentStatus();
    if (!status?.consentSummary[type]) return 0;
    const s = status.consentSummary[type];
    return s.totalMembers > 0 ? Math.round((s.grantedCount / s.totalMembers) * 100) : 0;
  }

  // ═══════════════════════════════════════════════════
  // LOGIN HISTORY
  // ═══════════════════════════════════════════════════

  loadLoginHistory() {
    this.loading.set(true);
    this.enterpriseService
      .getLoginHistory(
        this.loginSearch || undefined,
        this.loginPage,
        50,
        this.loginSuccessFilter,
        this.loginSuspiciousFilter,
      )
      .subscribe({
        next: (res) => {
          this.loginHistory.set(res.items);
          this.loginTotal = res.total;
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  // ═══════════════════════════════════════════════════
  // PERMISSIONS
  // ═══════════════════════════════════════════════════

  loadPermissionDefinitions() {
    this.permDefService.loadPermissionGroups().subscribe({
      next: (groups) => {
        this.permissionGroups = groups.map((g) => ({
          name: `${g.groupIcon} ${g.groupName}`,
          permissions: g.permissions.map((p) => p.code),
        }));
        groups.forEach((g) =>
          g.permissions.forEach((p) => (this.permissionDisplayNames[p.code] = p.displayName)),
        );
      },
    });
  }

  formatPermission(perm: string): string {
    return this.permissionDisplayNames[perm] || perm.replace(/([A-Z])/g, ' $1').trim();
  }

  // ═══════════════════════════════════════════════════
  // UI HELPERS
  // ═══════════════════════════════════════════════════

  getInitials(name: string): string {
    if (!name) return 'U';
    return name
      .split(' ')
      .map((n) => n[0])
      .join('')
      .substring(0, 2)
      .toUpperCase();
  }

  getAvatarColor(name: string): string {
    const colors = [
      '#ef4444',
      '#f97316',
      '#f59e0b',
      '#84cc16',
      '#10b981',
      '#06b6d4',
      '#3b82f6',
      '#6366f1',
      '#8b5cf6',
      '#d946ef',
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return colors[Math.abs(hash) % colors.length];
  }

  getRoleClass(role: string): string {
    const map: Record<string, string> = {
      doctor: 'role-doctor',
      nurse: 'role-nurse',
      admin: 'role-admin',
      director: 'role-director',
      embryologist: 'role-embryologist',
    };
    return map[role?.toLowerCase()] || 'role-default';
  }

  getStatusClass(isActive: boolean): string {
    return isActive ? 'status-active' : 'status-inactive';
  }

  getRiskClass(score: number | null): string {
    if (!score || score < 20) return 'risk-low';
    if (score < 50) return 'risk-medium';
    if (score < 80) return 'risk-high';
    return 'risk-critical';
  }

  getSeverityClass(isSuspicious: boolean, isSuccess: boolean): string {
    if (isSuspicious) return 'severity-critical';
    if (!isSuccess) return 'severity-warning';
    return 'severity-success';
  }

  getGroupTypeLabel(type: string): string {
    return this.groupTypes.find((t) => t.value === type)?.label || type;
  }

  getGroupTypeIcon(type: string): string {
    return this.groupTypes.find((t) => t.value === type)?.icon || '📁';
  }

  getConsentLabel(type: string): string {
    return CONSENT_TYPES.find((t) => t.value === type)?.label || type;
  }

  getConsentIcon(type: string): string {
    return CONSENT_TYPES.find((t) => t.value === type)?.icon || '📋';
  }

  formatDate(date: string | null): string {
    if (!date) return '—';
    return new Date(date).toLocaleString('vi-VN');
  }

  formatDuration(duration: string | null): string {
    if (!duration) return '—';
    const match = duration.match(/(\d+):(\d+):(\d+)/);
    if (!match) return duration;
    const [, h, m] = match;
    return `${h}h ${m}m`;
  }

  getBarWidth(value: number, max: number): number {
    return max > 0 ? Math.round((value / max) * 100) : 0;
  }
}
