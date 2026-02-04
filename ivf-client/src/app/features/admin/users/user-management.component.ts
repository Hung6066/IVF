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
        <div class="header-content">
          <h1>👥 Quản lý nhân sự</h1>
          <p class="text-muted">Quản lý tài khoản, phân quyền và trạng thái hoạt động</p>
        </div>
        <button class="btn-primary" (click)="openModal()">
          <span class="icon">+</span> Thêm tài khoản
        </button>
      </header>
      
      <!-- Filters -->
      <div class="filters-card">
        <div class="filter-group">
          <span class="search-icon">🔍</span>
          <input type="text" [(ngModel)]="search" (keyup.enter)="loadUsers()" placeholder="Tìm kiếm theo tên, username..." class="search-input">
        </div>
        
        <select [(ngModel)]="roleFilter" (change)="loadUsers()" class="filter-select">
          <option value="">Tất cả vai trò</option>
          @for (role of roles(); track role) {
            <option [value]="role">{{ role }}</option>
          }
        </select>
        
        <select [(ngModel)]="statusFilter" (change)="loadUsers()" class="filter-select">
          <option [ngValue]="undefined">Tất cả trạng thái</option>
          <option [ngValue]="true">🟢 Hoạt động</option>
          <option [ngValue]="false">🔴 Đã khóa</option>
        </select>

        <button class="btn-secondary" (click)="loadUsers()">Áp dụng</button>
      </div>

      <!-- Table -->
      <div class="table-container">
        <table class="premium-table">
          <thead>
            <tr>
              <th width="30%">Nhân viên</th>
              <th>Username</th>
              <th>Vai trò</th>
              <th>Khoa/Phòng</th>
              <th>Trạng thái</th>
              <th class="text-end">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (user of users(); track user.id) {
              <tr class="hover-row">
                <td>
                  <div class="user-cell">
                    <div class="avatar" [style.background-color]="getAvatarColor(user.fullName)">
                      {{ getInitials(user.fullName) }}
                    </div>
                    <div class="user-info">
                      <div class="fullname">{{ user.fullName }}</div>
                      <div class="email-muted">ID: {{ user.id.substring(0, 8) }}...</div>
                    </div>
                  </div>
                </td>
                <td><span class="username-tag">@ {{ user.username }}</span></td>
                <td>
                  <span class="role-badge" [class]="getRoleClass(user.role)">
                    {{ user.role }}
                  </span>
                </td>
                <td>{{ user.department || '—' }}</td>
                <td>
                  <span class="status-indicator" [class.active]="user.isActive">
                    <span class="dot"></span>
                    {{ user.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="text-end">
                  <div class="action-buttons">
                    <!-- Add Doctor Info Button -->
                    @if (user.role === 'Doctor') {
                      <button class="btn-icon doctor" (click)="openDoctorModal(user)" title="Cập nhật thông tin bác sĩ">
                        🩺
                      </button>
                    }
                    <button class="btn-icon edit" (click)="openModal(user)" title="Chỉnh sửa">
                      ✏️
                    </button>
                    <button class="btn-icon delete" [class.restore]="!user.isActive" (click)="deleteUser(user)" 
                            [title]="user.isActive ? 'Khóa tài khoản' : 'Khôi phục tài khoản'">
                      {{ user.isActive ? '🔒' : '🔓' }}
                    </button>
                  </div>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="empty-state">
                  <div class="empty-content">
                    <div class="empty-icon">👥</div>
                    <h3>Không tìm thấy dữ liệu</h3>
                    <p>Thử thay đổi bộ lọc hoặc tìm kiếm từ khóa khác</p>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      
      <!-- Pagination -->
      <div class="pagination-footer">
         <span class="text-muted">Hiển thị {{ users().length }} kết quả</span>
         <div class="pagination-buttons">
           <button class="btn-page" [disabled]="page === 1" (click)="changePage(-1)">← Trước</button>
           <span class="page-number">{{ page }}</span>
           <button class="btn-page" [disabled]="users().length < pageSize" (click)="changePage(1)">Sau →</button>
         </div>
      </div>
    </div>

    <!-- Modal -->
    @if (showModal) {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h2>{{ editingUser ? 'Cập nhật thông tin' : 'Thêm nhân sự mới' }}</h2>
            <button class="close-btn" (click)="closeModal()">×</button>
          </div>
          
          <form (ngSubmit)="saveUser()" class="modal-body">
            <div class="form-grid">
              <div class="form-group full-width">
                <label>Họ và tên <span class="required">*</span></label>
                <input class="modern-input" [(ngModel)]="formData.fullName" name="fullName" placeholder="Nhập họ tên đầy đủ" required>
              </div>

              <div class="form-group">
                <label>Tên đăng nhập <span class="required">*</span></label>
                <input class="modern-input" [(ngModel)]="formData.username" name="username" [disabled]="!!editingUser" placeholder="VD: nguyenvana" required>
              </div>

              <div class="form-group">
                <label>Mật khẩu {{ editingUser ? '(Optional)' : '*' }}</label>
                <input class="modern-input" type="password" [(ngModel)]="formData.password" name="password" [required]="!editingUser" [placeholder]="editingUser ? '••••••' : 'Nhập mật khẩu'">
              </div>

              @if (editingUser) {
                <div class="form-check full-width">
                  <label class="checkbox-label">
                    <input type="checkbox" [(ngModel)]="changePassword" name="changePass">
                    <span>Đổi mật khẩu mới</span>
                  </label>
                </div>
              }

              <div class="form-group">
                <label>Vai trò <span class="required">*</span></label>
                <select class="modern-select" [(ngModel)]="formData.role" name="role" required>
                  @for (role of roles(); track role) {
                    <option [value]="role">{{ role }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label>Khoa / Phòng ban</label>
                <input class="modern-input" [(ngModel)]="formData.department" name="department" placeholder="VD: Khoa Hiếm Muộn">
              </div>
            </div>

            <div class="modal-footer">
              <button type="button" class="btn-ghost" (click)="closeModal()">Hủy bỏ</button>
              <button type="submit" class="btn-primary" [disabled]="loading()">
                {{ loading() ? 'Đang lưu...' : 'Lưu thay đổi' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    }

    <!-- Doctor Info Modal -->
    @if (showDoctorModal) {
      <div class="modal-backdrop" (click)="closeDoctorModal()">
        <div class="modal-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h2>🩺 Cập nhật thông tin bác sĩ</h2>
            <button class="close-btn" (click)="closeDoctorModal()">×</button>
          </div>
          
          <div class="modal-body">
            <p class="text-muted mb-4">Cập nhật thông tin chuyên môn cho tài khoản <strong>{{ selectedDoctorUser?.fullName }}</strong></p>
            
            <form (ngSubmit)="saveDoctorProfile()">
              <div class="form-grid">
                <div class="form-group">
                  <label>Chuyên khoa <span class="required">*</span></label>
                  <select class="modern-select" [(ngModel)]="doctorFormData.specialty" name="specialty" required>
                    <option value="IVF">IVF (Hiếm muộn)</option>
                    <option value="Andrology">Nam khoa</option>
                    <option value="Gynecology">Phụ sản</option>
                    <option value="Embryology">Phôi học</option>
                  </select>
                </div>

                <div class="form-group">
                  <label>Số chứng chỉ hành nghề</label>
                  <input class="modern-input" [(ngModel)]="doctorFormData.licenseNumber" name="license" placeholder="VD: 00123/BYT-CCHN">
                </div>

                <div class="form-group">
                  <label>Phòng khám mặc định</label>
                  <input class="modern-input" [(ngModel)]="doctorFormData.roomNumber" name="room" placeholder="VD: P.201">
                </div>

                <div class="form-group">
                  <label>Số ca tối đa/ngày</label>
                  <input class="modern-input" type="number" [(ngModel)]="doctorFormData.maxPatientsPerDay" name="maxPatients" min="1">
                </div>
              </div>

              <div class="modal-footer">
                <button type="button" class="btn-ghost" (click)="closeDoctorModal()">Hủy bỏ</button>
                <button type="submit" class="btn-primary" [disabled]="loading()">
                   {{ loading() ? 'Đang lưu...' : 'Lưu thông tin' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    :host { display: block; background: #f8fafc; min-height: 100vh; --primary: #6366f1; --primary-hover: #4f46e5; --text-dark: #1e293b; --text-muted: #64748b; --border: #e2e8f0; }
    
    .page-container { padding: 32px; max-width: 1400px; margin: 0 auto; }
    
    .page-header { display: flex; justify-content: space-between; align-items: flex-end; margin-bottom: 32px; }
    h1 { font-size: 1.8rem; font-weight: 700; color: var(--text-dark); margin: 0; letter-spacing: -0.5px; }
    .text-muted { color: var(--text-muted); font-size: 0.95rem; margin-top: 4px; }
    
    .btn-primary { background: var(--primary); color: white; border: none; padding: 10px 20px; border-radius: 8px; font-weight: 600; cursor: pointer; display: flex; align-items: center; gap: 8px; transition: all 0.2s; box-shadow: 0 4px 6px -1px rgba(99, 102, 241, 0.3); }
    .btn-primary:hover { background: var(--primary-hover); transform: translateY(-1px); }
    .btn-secondary { background: white; border: 1px solid var(--border); color: var(--text-dark); padding: 8px 16px; border-radius: 8px; cursor: pointer; font-weight: 500; }
    .btn-secondary:hover { background: #f1f5f9; }
    .btn-ghost { background: transparent; border: none; color: var(--text-muted); padding: 8px 16px; cursor: pointer; font-weight: 500; }
    .btn-ghost:hover { color: var(--text-dark); background: #f1f5f9; border-radius: 6px; }

    /* Filters */
    .filters-card { background: white; padding: 16px; border-radius: 12px; display: flex; gap: 16px; align-items: center; border: 1px solid var(--border); margin-bottom: 24px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); }
    .filter-group { position: relative; flex: 1; display: flex; align-items: center; }
    .search-input { width: 100%; padding: 10px 10px 10px 36px; border: 1px solid var(--border); border-radius: 8px; font-size: 0.95rem; transition: border 0.2s; }
    .search-input:focus { outline: none; border-color: var(--primary); box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1); }
    .search-icon { position: absolute; left: 10px; color: var(--text-muted); font-size: 1rem; }
    .filter-select { padding: 10px 32px 10px 12px; border: 1px solid var(--border); border-radius: 8px; background: white; cursor: pointer; font-size: 0.95rem; min-width: 160px; }
    
    /* Table */
    .table-container { background: white; border-radius: 16px; border: 1px solid var(--border); overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }
    .premium-table { width: 100%; border-collapse: collapse; }
    th { text-align: left; padding: 16px 24px; background: #f8fafc; color: var(--text-muted); font-weight: 600; font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 1px solid var(--border); }
    td { padding: 16px 24px; border-bottom: 1px solid #f1f5f9; color: var(--text-dark); vertical-align: middle; }
    .hover-row:hover { background: #f8fafc; transition: background 0.15s; }
    .hover-row:last-child td { border-bottom: none; }
    
    .user-cell { display: flex; align-items: center; gap: 12px; }
    .avatar { width: 40px; height: 40px; border-radius: 10px; color: white; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 1rem; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
    .user-info { display: flex; flex-direction: column; }
    .fullname { font-weight: 600; color: var(--text-dark); }
    .email-muted { font-size: 0.8rem; color: var(--text-muted); }
    
    .username-tag { background: #f1f5f9; padding: 4px 8px; border-radius: 6px; font-family: monospace; font-size: 0.9rem; color: var(--text-muted); }
    
    /* Badges */
    .role-badge { padding: 6px 12px; border-radius: 20px; font-size: 0.85rem; font-weight: 600; }
    .role-doctor { background: #e0e7ff; color: #4338ca; }
    .role-nurse { background: #fce7f3; color: #db2777; }
    .role-admin { background: #f3e8ff; color: #7e22ce; }
    .role-default { background: #f1f5f9; color: #64748b; }
    
    .status-indicator { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 20px; font-size: 0.85rem; font-weight: 500; background: #f1f5f9; color: #64748b; }
    .status-indicator.active { background: #dcfce7; color: #166534; }
    .dot { width: 8px; height: 8px; border-radius: 50%; background: currentColor; }
    
    /* Actions */
    .action-buttons { display: flex; gap: 8px; justify-content: flex-end; }
    .btn-icon { width: 32px; height: 32px; border-radius: 8px; border: none; background: transparent; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.2s; font-size: 1.1rem; }
    .btn-icon:hover { background: #f1f5f9; transform: scale(1.1); }
    .btn-icon.delete:hover { background: #fee2e2; }
    .btn-icon.restore:hover { background: #dcfce7; }
    
    /* Empty State */
    .empty-state { padding: 48px; text-align: center; }
    .empty-icon { font-size: 3rem; margin-bottom: 16px; opacity: 0.5; }
    
    /* Pagination */
    .pagination-footer { display: flex; justify-content: space-between; align-items: center; padding: 16px; margin-top: 16px; }
    .pagination-buttons { display: flex; gap: 8px; align-items: center; }
    .btn-page { background: white; border: 1px solid var(--border); padding: 6px 12px; border-radius: 6px; cursor: pointer; }
    .btn-page:disabled { opacity: 0.5; cursor: not-allowed; }
    .page-number { font-weight: 600; color: var(--text-dark); }

    /* Modal */
    .modal-backdrop { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.6); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 1000; animation: fadeIn 0.2s; }
    .modal-card { background: white; padding: 0; border-radius: 16px; width: 100%; max-width: 550px; box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25); animation: slideUp 0.3s; overflow: hidden; }
    .modal-header { padding: 24px; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: center; background: #f8fafc; }
    .modal-header h2 { margin: 0; font-size: 1.25rem; color: var(--text-dark); }
    .close-btn { background: none; border: none; font-size: 1.5rem; cursor: pointer; color: var(--text-muted); }
    .modal-body { padding: 24px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
    .full-width { grid-column: span 2; }
    .modern-input, .modern-select { width: 100%; padding: 10px 12px; border: 1px solid var(--border); border-radius: 8px; font-size: 0.95rem; transition: all 0.2s; }
    .modern-input:focus, .modern-select:focus { border-color: var(--primary); outline: none; box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1); }
    .checkbox-label { display: flex; align-items: center; gap: 8px; cursor: pointer; user-select: none; }
    .modal-footer { padding: 16px 24px; background: #f8fafc; border-top: 1px solid var(--border); display: flex; justify-content: flex-end; gap: 12px; }
    .required { color: #ef4444; }

    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes slideUp { from { transform: translateY(20px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }
  `]
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
    isActive: true
  };

  doctorFormData: any = {
    specialty: 'IVF',
    licenseNumber: '',
    roomNumber: '',
    maxPatientsPerDay: 20
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
      const updatedStatus = !user.isActive;
      this.api.updateUser(user.id, { ...user, isActive: updatedStatus }).subscribe(() => {
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
      maxPatientsPerDay: 20
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
      ...this.doctorFormData
    };

    this.api.createDoctor(payload).subscribe({
      next: () => {
        alert('Đã cập nhật thông tin bác sĩ thành công!');
        this.closeDoctorModal();
        this.loading.set(false);
      },
      error: (err) => {
        alert('Lỗi: ' + (err.error?.detail || 'Không thể tạo thông tin bác sĩ. Có thể đã tồn tại.'));
        this.loading.set(false);
      }
    });
  }

  // --- UI Helpers ---
  getInitials(name: string): string {
    if (!name) return 'U';
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }

  getAvatarColor(name: string): string {
    const colors = ['#ef4444', '#f97316', '#f59e0b', '#84cc16', '#10b981', '#06b6d4', '#3b82f6', '#6366f1', '#8b5cf6', '#d946ef', '#f43f5e'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return colors[Math.abs(hash) % colors.length];
  }

  getRoleClass(role: string): string {
    switch (role?.toLowerCase()) {
      case 'doctor': return 'role-doctor';
      case 'nurse': return 'role-nurse';
      case 'admin': return 'role-admin';
      default: return 'role-default';
    }
  }
}
