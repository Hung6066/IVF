import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-user-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="page-container">
      <header class="page-header">
        <h1>👥 Quản lý người dùng</h1>
        <button class="btn btn-primary" (click)="openModal()">+ Thêm người dùng</button>
      </header>
      
      <!-- Filters -->
      <div class="filters-bar">
        <input type="text" [(ngModel)]="search" (keyup.enter)="loadUsers()" placeholder="Tìm kiếm tên, username..." class="form-control search-input">
        
        <select [(ngModel)]="roleFilter" (change)="loadUsers()" class="form-control">
          <option value="">Tất cả vai trò</option>
          @for (role of roles(); track role) {
            <option [value]="role">{{ role }}</option>
          }
        </select>
        
        <select [(ngModel)]="statusFilter" (change)="loadUsers()" class="form-control">
          <option [ngValue]="undefined">Tất cả trạng thái</option>
          <option [ngValue]="true">Hoạt động</option>
          <option [ngValue]="false">Đã khóa</option>
        </select>

        <button class="btn btn-secondary" (click)="loadUsers()">Tìm kiếm</button>
      </div>

      <!-- Table -->
      <div class="table-responsive card">
        <table class="table">
          <thead>
            <tr>
              <th>Họ và tên</th>
              <th>Username</th>
              <th>Vai trò</th>
              <th>Khoa/Phòng</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (user of users(); track user.id) {
              <tr>
                <td class="fw-bold">{{ user.fullName }}</td>
                <td>{{ user.username }}</td>
                <td><span class="badge role-badge">{{ user.role }}</span></td>
                <td>{{ user.department || '-' }}</td>
                <td>
                  <span class="badge" [class.bg-success]="user.isActive" [class.bg-danger]="!user.isActive">
                    {{ user.isActive ? 'Hoạt động' : 'Đã khóa' }}
                  </span>
                </td>
                <td class="actions">
                  <button class="btn-icon" (click)="openModal(user)" title="Sửa">✏️</button>
                  <button class="btn-icon text-danger" (click)="deleteUser(user)" title="Xóa/Khóa">🗑️</button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="text-center py-4">Không tìm thấy người dùng nào</td></tr>
            }
          </tbody>
        </table>
      </div>
      
      <!-- Pagination (Simple) -->
      <div class="pagination-controls">
         <button class="btn btn-secondary btn-sm" [disabled]="page === 1" (click)="changePage(-1)">Trước</button>
         <span>Trang {{ page }}</span>
         <button class="btn btn-secondary btn-sm" [disabled]="users().length < pageSize" (click)="changePage(1)">Sau</button>
      </div>
    </div>

    <!-- Modal -->
    @if (showModal) {
      <div class="modal-overlay" (click)="closeModal()">
        <div class="modal-content" (click)="$event.stopPropagation()">
          <h2>{{ editingUser ? 'Cập nhật người dùng' : 'Thêm người dùng mới' }}</h2>
          <form (ngSubmit)="saveUser()">
            <div class="form-group">
              <label>Tên đăng nhập *</label>
              <input class="form-control" [(ngModel)]="formData.username" name="username" [disabled]="!!editingUser" required>
            </div>
            
            <div class="form-group" *ngIf="!editingUser || changePassword">
              <label>Mật khẩu {{ editingUser ? '(Để trống nếu không đổi)' : '*' }}</label>
              <input class="form-control" type="password" [(ngModel)]="formData.password" name="password" [required]="!editingUser">
            </div>

            @if (editingUser) {
              <div class="form-check mb-3">
                <input type="checkbox" [(ngModel)]="changePassword" name="changePass" id="changePass">
                <label for="changePass">Đổi mật khẩu</label>
              </div>
            }

            <div class="form-group">
              <label>Họ và tên *</label>
              <input class="form-control" [(ngModel)]="formData.fullName" name="fullName" required>
            </div>

            <div class="form-group">
              <label>Vai trò *</label>
              <select class="form-control" [(ngModel)]="formData.role" name="role" required>
                @for (role of roles(); track role) {
                  <option [value]="role">{{ role }}</option>
                }
              </select>
            </div>

            <div class="form-group">
              <label>Khoa/Phòng</label>
              <input class="form-control" [(ngModel)]="formData.department" name="department">
            </div>

            <div class="form-actions">
              <button type="button" class="btn btn-secondary" (click)="closeModal()">Hủy</button>
              <button type="submit" class="btn btn-primary" [disabled]="loading()">Lưu</button>
            </div>
          </form>
        </div>
      </div>
    }
  `,
    styles: [`
    .page-container { padding: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .filters-bar { display: flex; gap: 12px; margin-bottom: 20px; flex-wrap: wrap; }
    .search-input { min-width: 250px; }
    .role-badge { background-color: #e0e7ff; color: #4338ca; padding: 4px 8px; border-radius: 4px; font-weight: 500; font-size: 0.85em; }
    .actions { display: flex; gap: 8px; }
    .btn-icon { background: none; border: none; cursor: pointer; font-size: 1.1em; transition: transform 0.2s; }
    .btn-icon:hover { transform: scale(1.1); }
    .bg-success { background-color: #dcfce7; color: #166534; padding: 4px 8px; border-radius: 12px; font-size: 0.8em; }
    .bg-danger { background-color: #fee2e2; color: #991b1b; padding: 4px 8px; border-radius: 12px; font-size: 0.8em; }
    
    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal-content { background: white; padding: 24px; border-radius: 12px; width: 100%; max-width: 500px; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.1); }
    .form-group { margin-bottom: 16px; }
    .form-control { width: 100%; padding: 8px 12px; border: 1px solid #cbd5e1; border-radius: 6px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 12px; margin-top: 24px; }
    .pagination-controls { display: flex; justify-content: flex-end; align-items: center; gap: 12px; margin-top: 20px; }
  `]
})
export class UserManagementComponent implements OnInit {
    users = signal<any[]>([]);
    roles = signal<string[]>([]);
    loading = signal(false);

    search = '';
    roleFilter = '';
    statusFilter: boolean | undefined = undefined;
    page = 1;
    pageSize = 20;

    showModal = false;
    editingUser: any = null;
    changePassword = false;

    formData: any = {
        username: '',
        password: '',
        fullName: '',
        role: 'Doctor',
        department: '',
        isActive: true
    };

    constructor(private api: ApiService) { }

    ngOnInit() {
        this.loadRoles();
        this.loadUsers();
    }

    loadRoles() {
        this.api.getRoles().subscribe(roles => this.roles.set(roles));
    }

    loadUsers() {
        this.loading.set(true);
        this.api.getUsers(this.search, this.roleFilter, this.statusFilter, this.page, this.pageSize)
            .subscribe({
                next: (res) => {
                    this.users.set(res.items);
                    this.loading.set(false);
                },
                error: () => this.loading.set(false)
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
                isActive: true
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
            const updateData = { ...this.formData };
            if (!this.changePassword) delete updateData.password;

            this.api.updateUser(this.editingUser.id, updateData).subscribe({
                next: () => {
                    this.loadUsers();
                    this.closeModal();
                    this.loading.set(false);
                },
                error: () => this.loading.set(false)
            });
        } else {
            this.api.createUser(this.formData).subscribe({
                next: () => {
                    this.loadUsers();
                    this.closeModal();
                    this.loading.set(false);
                },
                error: () => this.loading.set(false)
            });
        }
    }

    deleteUser(user: any) {
        if (confirm(`Bạn có chắc muốn ${user.isActive ? 'khóa' : 'khôi phục'} tài khoản ${user.username}?`)) {
            // Logic for toggle active? Or delete?
            // My DeleteUserCommand does Deactivate (Soft Delete).
            // But if user is already inactive?
            // The requirement was CRUD. Delete usually means remove or deactivate.
            // My backend DeleteUserCommand does Deactivate.
            // If I want to "Restore", I need another endpoint or use UpdateUserCommand.
            // For now, I'll use UpdateUserCommand to toggle IsActive if I want toggle behavior.
            // But Delete button usually implies Deactivate.
            // I'll implement toggle via Update.

            const updatedStatus = !user.isActive;
            this.api.updateUser(user.id, { ...user, isActive: updatedStatus }).subscribe(() => {
                this.loadUsers();
            });
        }
    }
}
