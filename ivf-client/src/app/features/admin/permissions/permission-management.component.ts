import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UserService } from '../../../core/services/user.service';
import {
  PermissionDefinitionService,
  PermissionGroupDto,
} from '../../../core/services/permission-definition.service';

interface PermissionGroup {
  name: string;
  icon: string;
  permissions: string[];
}

interface UserPermissionRow {
  id: string;
  username: string;
  fullName: string;
  role: string;
  department?: string;
  isActive: boolean;
  permissions: string[];
  expanded: boolean;
}

@Component({
  selector: 'app-permission-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './permission-management.component.html',
  styleUrls: ['./permission-management.component.scss'],
})
export class PermissionManagementComponent implements OnInit {
  loading = signal(false);
  users = signal<UserPermissionRow[]>([]);
  roles = signal<string[]>([]);
  allPermissions = signal<string[]>([]);

  // Filters
  search = '';
  roleFilter = '';
  page = 1;
  pageSize = 20;

  // Editing state
  editingUserId: string | null = null;
  editingPermissions: string[] = [];
  saving = signal(false);

  // Tab
  activeTab: 'users' | 'matrix' = 'users';

  /** Loaded dynamically from the API */
  permissionGroups: PermissionGroup[] = [];

  /** Permission display name map (code -> Vietnamese label) */
  permissionDisplayNames: Record<string, string> = {};

  get allPermissionsList(): string[] {
    return this.permissionGroups.flatMap((g) => g.permissions);
  }

  constructor(
    private userService: UserService,
    private permDefService: PermissionDefinitionService,
  ) {}

  ngOnInit() {
    this.loadPermissionDefinitions();
    this.loadRoles();
    this.loadUsers();
  }

  loadPermissionDefinitions() {
    this.permDefService.loadPermissionGroups().subscribe({
      next: (groups) => {
        this.permissionGroups = groups.map((g) => ({
          name: g.groupName,
          icon: g.groupIcon,
          permissions: g.permissions.map((p) => p.code),
        }));
        // Build display name map
        groups.forEach((g) =>
          g.permissions.forEach((p) => (this.permissionDisplayNames[p.code] = p.displayName)),
        );
      },
    });
  }

  loadRoles() {
    this.userService.getRoles().subscribe((roles) => this.roles.set(roles));
  }

  loadUsers() {
    this.loading.set(true);
    this.userService
      .getUsers(this.search, this.roleFilter, undefined, this.page, this.pageSize)
      .subscribe({
        next: (res) => {
          const users: UserPermissionRow[] = (res.items || []).map((u: any) => ({
            ...u,
            permissions: [],
            expanded: false,
          }));
          this.users.set(users);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  applyFilter() {
    this.page = 1;
    this.loadUsers();
  }

  changePage(delta: number) {
    this.page += delta;
    this.loadUsers();
  }

  // Load permissions for a single user row
  toggleUserExpand(user: UserPermissionRow) {
    if (user.expanded) {
      user.expanded = false;
      return;
    }

    // Collapse all others
    this.users().forEach((u) => (u.expanded = false));
    user.expanded = true;

    this.userService.getUserPermissions(user.id).subscribe({
      next: (perms) => {
        user.permissions = perms;
        this.users.update((list) => [...list]);
      },
    });
  }

  // Editing
  startEdit(user: UserPermissionRow) {
    this.editingUserId = user.id;
    this.editingPermissions = [...user.permissions];
  }

  cancelEdit() {
    this.editingUserId = null;
    this.editingPermissions = [];
  }

  isEditing(user: UserPermissionRow): boolean {
    return this.editingUserId === user.id;
  }

  togglePermission(perm: string) {
    const idx = this.editingPermissions.indexOf(perm);
    if (idx >= 0) {
      this.editingPermissions.splice(idx, 1);
    } else {
      this.editingPermissions.push(perm);
    }
  }

  hasPermission(user: UserPermissionRow, perm: string): boolean {
    if (this.isEditing(user)) {
      return this.editingPermissions.includes(perm);
    }
    return user.permissions.includes(perm);
  }

  selectAllGroup(group: PermissionGroup) {
    const allSelected = group.permissions.every((p) => this.editingPermissions.includes(p));
    if (allSelected) {
      this.editingPermissions = this.editingPermissions.filter(
        (p) => !group.permissions.includes(p),
      );
    } else {
      group.permissions.forEach((p) => {
        if (!this.editingPermissions.includes(p)) {
          this.editingPermissions.push(p);
        }
      });
    }
  }

  isGroupAllSelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.editingPermissions.includes(p));
  }

  isGroupPartialSelected(group: PermissionGroup): boolean {
    const some = group.permissions.some((p) => this.editingPermissions.includes(p));
    const all = group.permissions.every((p) => this.editingPermissions.includes(p));
    return some && !all;
  }

  selectAll() {
    this.editingPermissions = [...this.allPermissionsList];
  }

  deselectAll() {
    this.editingPermissions = [];
  }

  savePermissions(user: UserPermissionRow) {
    this.saving.set(true);
    this.userService.assignPermissions(user.id, this.editingPermissions).subscribe({
      next: () => {
        user.permissions = [...this.editingPermissions];
        this.editingUserId = null;
        this.editingPermissions = [];
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }

  formatPermission(perm: string): string {
    return this.permissionDisplayNames[perm] || perm.replace(/([A-Z])/g, ' $1').trim();
  }

  getPermissionCount(user: UserPermissionRow): number {
    return user.permissions.length;
  }

  getRoleBadgeClass(role: string): string {
    switch (role) {
      case 'Admin':
        return 'badge-admin';
      case 'Doctor':
        return 'badge-doctor';
      case 'Nurse':
        return 'badge-nurse';
      default:
        return 'badge-default';
    }
  }
}
