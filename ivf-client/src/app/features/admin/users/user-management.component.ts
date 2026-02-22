import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UserService } from '../../../core/services/user.service';
import { PermissionDefinitionService } from '../../../core/services/permission-definition.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-management.component.html',
  styleUrls: ['./user-management.component.scss'],
})
export class UserManagementComponent implements OnInit {
  // ... (Same logic as before, extended with helpers)
  users = signal<any[]>([]);
  roles = signal<string[]>([]);
  loading = signal(false);

  search = '';
  roleFilter = '';
  statusFilter: boolean | undefined = undefined;
  page = 1;
  pageSize = 20;

  showModal = false;
  showDoctorModal = false; // New modal state
  editingUser: any = null;
  selectedDoctorUser: any = null; // User selected for doctor promotion
  changePassword = false;

  formData: any = {
    username: '',
    password: '',
    fullName: '',
    role: 'Doctor',
    department: '',
    isActive: true,
  };

  doctorFormData: any = {
    specialty: 'IVF',
    licenseNumber: '',
    roomNumber: '',
    maxPatientsPerDay: 20,
  };

  // Permissions Modal
  showPermissionsModal = false;
  selectedPermissionUser: any = null;
  userPermissions: string[] = [];

  /** Loaded dynamically from the API */
  permissionGroups: { name: string; permissions: string[] }[] = [];

  /** Permission display name map */
  private permissionDisplayNames: Record<string, string> = {};

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
          name: `${g.groupIcon} ${g.groupName}`,
          permissions: g.permissions.map((p) => p.code),
        }));
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
      .getUsers(this.search, this.roleFilter, this.statusFilter, this.page, this.pageSize)
      .subscribe({
        next: (res) => {
          this.users.set(res.items);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  changePage(delta: number) {
    this.page += delta;
    this.loadUsers();
  }

  openModal(user: any = null) {
    this.editingUser = user;
    this.changePassword = false;
    if (user) {
      this.formData = { ...user, password: '' };
    } else {
      this.formData = {
        username: '',
        password: '',
        fullName: '',
        role: 'Doctor',
        department: '',
        isActive: true,
      };
    }
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
    this.editingUser = null;
  }

  saveUser() {
    this.loading.set(true);
    if (this.editingUser) {
      const updateData: any = {
        id: this.editingUser.id,
        fullName: this.formData.fullName,
        role: this.formData.role,
        department: this.formData.department,
        isActive: this.formData.isActive,
      };

      // Only include password if checkbox is checked AND password is not empty
      if (this.changePassword && this.formData.password?.trim()) {
        updateData.password = this.formData.password;
      }

      this.userService.updateUser(this.editingUser.id, updateData).subscribe({
        next: () => {
          this.loadUsers();
          this.closeModal();
          this.loading.set(false);
          if (this.changePassword) {
            alert('Đã cập nhật mật khẩu thành công!');
          }
        },
        error: (err) => {
          this.loading.set(false);
          alert('Lỗi cập nhật: ' + (err.error?.detail || err.message || 'Không xác định'));
        },
      });
    } else {
      this.userService.createUser(this.formData).subscribe({
        next: () => {
          this.loadUsers();
          this.closeModal();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  deleteUser(user: any) {
    if (
      confirm(
        `Bạn có chắc muốn ${user.isActive ? 'khóa' : 'khôi phục'} tài khoản ${user.username}?`,
      )
    ) {
      const updatedStatus = !user.isActive;
      this.userService.updateUser(user.id, { ...user, isActive: updatedStatus }).subscribe(() => {
        this.loadUsers();
      });
    }
  }

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
    const payload = {
      userId: this.selectedDoctorUser.id,
      ...this.doctorFormData,
    };

    this.userService.createDoctor(payload).subscribe({
      next: () => {
        alert('Đã cập nhật thông tin bác sĩ thành công!');
        this.closeDoctorModal();
        this.loading.set(false);
      },
      error: (err) => {
        alert(
          'Lỗi: ' + (err.error?.detail || 'Không thể tạo thông tin bác sĩ. Có thể đã tồn tại.'),
        );
        this.loading.set(false);
      },
    });
  }

  // --- Permissions Modal ---
  openPermissionsModal(user: any) {
    this.selectedPermissionUser = user;
    this.userPermissions = [];
    this.showPermissionsModal = true;

    // Load user's current permissions
    this.userService.getUserPermissions(user.id).subscribe({
      next: (permissions) => {
        this.userPermissions = permissions || [];
      },
      error: () => {
        this.userPermissions = [];
      },
    });
  }

  closePermissionsModal() {
    this.showPermissionsModal = false;
    this.selectedPermissionUser = null;
    this.userPermissions = [];
  }

  togglePermission(permission: string) {
    if (this.userPermissions.includes(permission)) {
      this.userPermissions = this.userPermissions.filter((p) => p !== permission);
    } else {
      this.userPermissions = [...this.userPermissions, permission];
    }
  }

  savePermissions() {
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
          alert('Lỗi: ' + (err.error?.detail || 'Không thể cập nhật quyền'));
          this.loading.set(false);
        },
      });
  }

  formatPermission(perm: string): string {
    return this.permissionDisplayNames[perm] || perm.replace(/([A-Z])/g, ' $1').trim();
  }

  // --- UI Helpers ---
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
      '#f43f5e',
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return colors[Math.abs(hash) % colors.length];
  }

  getRoleClass(role: string): string {
    switch (role?.toLowerCase()) {
      case 'doctor':
        return 'role-doctor';
      case 'nurse':
        return 'role-nurse';
      case 'admin':
        return 'role-admin';
      default:
        return 'role-default';
    }
  }
}
