import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, Notification, NotificationType, CreateNotificationRequest, BroadcastNotificationRequest } from '../../../core/services/api.service';

@Component({
    selector: 'app-notification-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="dashboard-layout">
      <header class="page-header">
        <div class="header-title">
          <h1>🔔 Quản lý thông báo</h1>
        </div>
        <div class="header-actions">
          <button class="btn-primary" (click)="showCreateModal = true">+ Tạo thông báo</button>
          <button class="btn-secondary" (click)="showBroadcastModal = true">📢 Phát thông báo</button>
        </div>
      </header>

      <!-- Stats -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon info">🔔</div>
          <div class="stat-content">
            <div class="value">{{ notifications().length }}</div>
            <div class="label">Tổng thông báo</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon warning">📨</div>
          <div class="stat-content">
            <div class="value">{{ unreadCount() }}</div>
            <div class="label">Chưa đọc</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon success">✓</div>
          <div class="stat-content">
            <div class="value">{{ notifications().length - unreadCount() }}</div>
            <div class="label">Đã đọc</div>
          </div>
        </div>
      </div>

      <!-- Filters -->
      <div class="card filter-card">
        <div class="filter-row">
          <div class="form-group">
            <label>Loại thông báo</label>
            <select [(ngModel)]="filterType" (change)="applyFilters()">
              <option value="">Tất cả</option>
              <option value="Info">Thông tin</option>
              <option value="Success">Thành công</option>
              <option value="Warning">Cảnh báo</option>
              <option value="Error">Lỗi</option>
              <option value="AppointmentReminder">Nhắc lịch hẹn</option>
              <option value="QueueCalled">Gọi số</option>
              <option value="CycleUpdate">Cập nhật chu kỳ</option>
              <option value="PaymentDue">Thanh toán</option>
            </select>
          </div>
          <div class="form-group">
            <label>Trạng thái</label>
            <select [(ngModel)]="filterRead" (change)="applyFilters()">
              <option value="">Tất cả</option>
              <option value="unread">Chưa đọc</option>
              <option value="read">Đã đọc</option>
            </select>
          </div>
          <div class="form-group search-group">
            <label>Tìm kiếm</label>
            <input type="text" [(ngModel)]="searchTerm" (input)="applyFilters()" placeholder="Tiêu đề hoặc nội dung...">
          </div>
        </div>
      </div>

      <!-- Notifications List -->
      <div class="card">
        <div class="section-header">
          <h2>Danh sách thông báo</h2>
          @if (selectedIds().length > 0) {
            <button class="btn-danger-outline" (click)="markSelectedAsRead()">
              Đánh dấu đã đọc ({{ selectedIds().length }})
            </button>
          }
        </div>

        <div class="notification-table">
          @for (notification of filteredNotifications(); track notification.id) {
            <div class="notification-row" [class.unread]="!notification.isRead">
              <div class="notification-checkbox">
                <input type="checkbox" [checked]="selectedIds().includes(notification.id)"
                       (change)="toggleSelect(notification.id)">
              </div>
              <div class="notification-icon" [class]="notification.type.toLowerCase()">
                {{ getTypeIcon(notification.type) }}
              </div>
              <div class="notification-content">
                <div class="notification-header">
                  <span class="notification-title">{{ notification.title }}</span>
                  <span class="notification-type" [class]="notification.type.toLowerCase()">
                    {{ getTypeLabel(notification.type) }}
                  </span>
                </div>
                <div class="notification-message">{{ notification.message }}</div>
                <div class="notification-meta">
                  <span class="notification-time">{{ formatTime(notification.createdAt) }}</span>
                  @if (notification.entityType) {
                    <span class="notification-entity">{{ notification.entityType }}</span>
                  }
                </div>
              </div>
              <div class="notification-actions">
                @if (!notification.isRead) {
                  <button class="btn-icon" title="Đánh dấu đã đọc" (click)="markAsRead(notification)">✓</button>
                }
              </div>
            </div>
          } @empty {
            <div class="empty-state">Không có thông báo nào</div>
          }
        </div>
      </div>

      <!-- Create Notification Modal -->
      @if (showCreateModal) {
        <div class="modal-overlay" (click)="showCreateModal = false">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h2>Tạo thông báo mới</h2>
              <button class="close-btn" (click)="showCreateModal = false">×</button>
            </div>
            
            <form (ngSubmit)="createNotification()">
              <div class="modal-body">
                <div class="form-grid">
                  <div class="form-group">
                    <label>User ID</label>
                    <input class="form-control" [(ngModel)]="newNotification.userId" name="userId" required placeholder="ID người nhận">
                  </div>
                  <div class="form-group">
                    <label>Loại thông báo</label>
                    <select class="form-control" [(ngModel)]="newNotification.type" name="type" required>
                      <option value="Info">Thông tin</option>
                      <option value="Success">Thành công</option>
                      <option value="Warning">Cảnh báo</option>
                      <option value="Error">Lỗi</option>
                      <option value="AppointmentReminder">Nhắc lịch hẹn</option>
                      <option value="QueueCalled">Gọi số</option>
                      <option value="CycleUpdate">Cập nhật chu kỳ</option>
                      <option value="PaymentDue">Thanh toán</option>
                    </select>
                  </div>
                  
                  <div class="form-group full-width">
                    <label>Tiêu đề</label>
                    <input class="form-control" [(ngModel)]="newNotification.title" name="title" required>
                  </div>
                  
                  <div class="form-group full-width">
                    <label>Nội dung</label>
                    <textarea class="form-control" [(ngModel)]="newNotification.message" name="message" required rows="3"></textarea>
                  </div>

                  <div class="form-group full-width">
                    <label>Link (tùy chọn)</label>
                    <input class="form-control" [(ngModel)]="newNotification.actionUrl" name="actionUrl" placeholder="/path/to/page">
                  </div>
                </div>
              </div>

              <div class="modal-footer">
                <button type="button" class="btn-secondary" (click)="showCreateModal = false">Hủy</button>
                <button type="submit" class="btn-primary">Tạo thông báo</button>
              </div>
            </form>
          </div>
        </div>
      }

      <!-- Broadcast Notification Modal -->
      @if (showBroadcastModal) {
        <div class="modal-overlay" (click)="showBroadcastModal = false">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h2>📢 Phát thông báo</h2>
              <button class="close-btn" (click)="showBroadcastModal = false">×</button>
            </div>

            <form (ngSubmit)="broadcastNotification()">
              <div class="modal-body">
                <div class="form-grid">
                  <div class="form-group full-width">
                    <label>Tiêu đề</label>
                    <input class="form-control" [(ngModel)]="broadcastData.title" name="title" required>
                  </div>
                  
                  <div class="form-group full-width">
                    <label>Nội dung</label>
                    <textarea class="form-control" [(ngModel)]="broadcastData.message" name="message" required rows="3"></textarea>
                  </div>

                  <div class="form-group">
                    <label>Loại thông báo</label>
                    <select class="form-control" [(ngModel)]="broadcastData.type" name="type" required>
                      <option value="Info">Thông tin</option>
                      <option value="Success">Thành công</option>
                      <option value="Warning">Cảnh báo</option>
                      <option value="Error">Lỗi</option>
                    </select>
                  </div>

                  <div class="form-group">
                    <label>Nhóm người nhận</label>
                    <select class="form-control" [(ngModel)]="broadcastData.role" name="role">
                      <option value="">Tất cả người dùng</option>
                      <option value="Admin">Admin</option>
                      <option value="Doctor">Bác sĩ</option>
                      <option value="Nurse">Y tá</option>
                      <option value="Receptionist">Lễ tân</option>
                      <option value="LabTechnician">Kỹ thuật viên</option>
                    </select>
                  </div>
                </div>
              </div>

              <div class="modal-footer">
                <button type="button" class="btn-secondary" (click)="showBroadcastModal = false">Hủy</button>
                <button type="submit" class="btn-primary">Phát</button>
              </div>
            </form>
          </div>
        </div>
      }

      <!-- Success Toast -->
      @if (toastMessage()) {
        <div class="toast" [class]="toastType()">
          {{ toastMessage() }}
        </div>
      }
    </div>
  `,
    styles: [`
    .dashboard-layout {
      padding: 1.5rem;
      max-width: 1400px;
      margin: 0 auto;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
    }

    .page-header h1 {
      font-size: 1.875rem;
      font-weight: 700;
      color: var(--text-primary);
      margin: 0;
    }

    .header-actions {
      display: flex;
      gap: 1rem;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1.5rem;
      margin-bottom: 2rem;
    }

    .stat-card {
      display: flex;
      align-items: center;
      padding: 1.5rem;
      gap: 1.5rem;
      background: white;
      border-radius: 12px;
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--border-color);
    }

    .stat-icon {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
    }

    .stat-icon.info { background: #dbeafe; }
    .stat-icon.warning { background: #fef3c7; }
    .stat-icon.success { background: #d1fae5; }

    .stat-content .value {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--text-primary);
    }

    .stat-content .label {
      font-size: 0.875rem;
      color: var(--text-secondary);
    }

    .card {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: var(--shadow-sm);
      border: 1px solid var(--border-color);
      margin-bottom: 1.5rem;
    }

    .filter-row {
      display: flex;
      gap: 1rem;
      align-items: flex-end;
    }

    .form-group {
      flex: 1;
    }

    .form-group.search-group {
      flex: 2;
    }

    .form-group label {
      display: block;
      font-size: 0.75rem;
      color: var(--text-secondary);
      margin-bottom: 0.5rem;
      font-weight: 500;
    }

    .form-group input,
    .form-group select,
    .form-group textarea {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid var(--border-color);
      border-radius: 8px;
      background: white;
      color: var(--text-primary);
      font-size: 0.875rem;
    }

    .form-group input:focus,
    .form-group select:focus,
    .form-group textarea:focus {
      outline: none;
      border-color: var(--primary);
    }

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }

    .section-header h2 {
      font-size: 1.25rem;
      margin: 0;
      color: var(--text-primary);
    }

    .notification-table {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .notification-row {
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      padding: 1rem;
      border: 1px solid var(--border-color);
      border-radius: 8px;
      transition: all 0.2s;
    }

    .notification-row:hover {
      border-color: var(--primary);
    }

    .notification-row.unread {
      background: #f0f9ff;
      border-left: 3px solid var(--primary);
    }

    .notification-checkbox {
      padding-top: 4px;
    }

    .notification-icon {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
      flex-shrink: 0;
    }

    .notification-icon.info { background: #dbeafe; }
    .notification-icon.success { background: #d1fae5; }
    .notification-icon.warning { background: #fef3c7; }
    .notification-icon.error { background: #fee2e2; }
    .notification-icon.appointmentreminder { background: #ede9fe; }
    .notification-icon.queuecalled { background: #ccfbf1; }
    .notification-icon.cycleupdate { background: #fce7f3; }
    .notification-icon.paymentdue { background: #fef3c7; }

    .notification-content {
      flex: 1;
      min-width: 0;
    }

    .notification-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.25rem;
    }

    .notification-title {
      font-weight: 600;
      color: var(--text-primary);
    }

    .notification-type {
      font-size: 0.75rem;
      padding: 0.125rem 0.5rem;
      border-radius: 999px;
      font-weight: 500;
    }

    .notification-type.info { background: #dbeafe; color: #1e40af; }
    .notification-type.success { background: #d1fae5; color: #065f46; }
    .notification-type.warning { background: #fef3c7; color: #92400e; }
    .notification-type.error { background: #fee2e2; color: #991b1b; }
    .notification-type.appointmentreminder { background: #ede9fe; color: #5b21b6; }
    .notification-type.queuecalled { background: #ccfbf1; color: #0f766e; }
    .notification-type.cycleupdate { background: #fce7f3; color: #9d174d; }
    .notification-type.paymentdue { background: #fef3c7; color: #92400e; }

    .notification-message {
      font-size: 0.875rem;
      color: var(--text-secondary);
      margin-bottom: 0.5rem;
    }

    .notification-meta {
      display: flex;
      gap: 1rem;
      font-size: 0.75rem;
      color: var(--text-secondary);
    }

    .notification-actions {
      display: flex;
      gap: 0.5rem;
    }

    .btn-icon {
      width: 32px;
      height: 32px;
      border: none;
      background: #f1f5f9;
      border-radius: 6px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .btn-icon:hover {
      background: #e2e8f0;
    }

    .btn-primary {
      background: var(--primary);
      color: white;
      border: none;
      padding: 0.75rem 1.5rem;
      border-radius: 8px;
      font-weight: 500;
      cursor: pointer;
    }

    .btn-primary:hover {
      background: var(--primary-dark);
    }

    .btn-secondary {
      background: #f1f5f9;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
      padding: 0.75rem 1.5rem;
      border-radius: 8px;
      cursor: pointer;
    }

    .btn-secondary:hover {
      background: #e2e8f0;
    }

    .btn-danger-outline {
      background: transparent;
      color: #dc2626;
      border: 1px solid #dc2626;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      font-size: 0.875rem;
      cursor: pointer;
    }

    .btn-danger-outline:hover {
      background: #fef2f2;
    }

    .empty-state {
      text-align: center;
      padding: 3rem;
      color: var(--text-secondary);
    }



    .toast {
      position: fixed;
      bottom: 2rem;
      right: 2rem;
      padding: 1rem 1.5rem;
      border-radius: 8px;
      font-weight: 500;
      animation: slideIn 0.3s ease;
      z-index: 2000;
    }

    .toast.success {
      background: #d1fae5;
      color: #065f46;
    }

    .toast.error {
      background: #fee2e2;
      color: #991b1b;
    }

    @keyframes slideIn {
      from {
        transform: translateX(100%);
        opacity: 0;
      }
      to {
        transform: translateX(0);
        opacity: 1;
      }
    }
  `]
})
export class NotificationManagementComponent implements OnInit {
    notifications = signal<Notification[]>([]);
    filteredNotifications = signal<Notification[]>([]);
    unreadCount = signal(0);
    selectedIds = signal<string[]>([]);

    filterType = '';
    filterRead = '';
    searchTerm = '';

    showCreateModal = false;
    showBroadcastModal = false;

    newNotification: Partial<CreateNotificationRequest> = {
        type: 'Info'
    };

    broadcastData: Partial<BroadcastNotificationRequest> = {
        type: 'Info'
    };

    toastMessage = signal('');
    toastType = signal('success');

    constructor(private api: ApiService) { }

    ngOnInit() {
        this.loadNotifications();
    }

    loadNotifications() {
        this.api.getNotifications().subscribe((notifications: Notification[]) => {
            this.notifications.set(notifications);
            this.filteredNotifications.set(notifications);
            this.unreadCount.set(notifications.filter(n => !n.isRead).length);
        });
    }

    applyFilters() {
        let result = this.notifications();

        if (this.filterType) {
            result = result.filter(n => n.type === this.filterType);
        }

        if (this.filterRead === 'unread') {
            result = result.filter(n => !n.isRead);
        } else if (this.filterRead === 'read') {
            result = result.filter(n => n.isRead);
        }

        if (this.searchTerm) {
            const term = this.searchTerm.toLowerCase();
            result = result.filter(n =>
                n.title.toLowerCase().includes(term) ||
                n.message.toLowerCase().includes(term)
            );
        }

        this.filteredNotifications.set(result);
    }

    toggleSelect(id: string) {
        this.selectedIds.update(ids =>
            ids.includes(id) ? ids.filter(i => i !== id) : [...ids, id]
        );
    }

    markAsRead(notification: Notification) {
        this.api.markNotificationAsRead(notification.id).subscribe(() => {
            this.notifications.update(list =>
                list.map(n => n.id === notification.id ? { ...n, isRead: true } : n)
            );
            this.applyFilters();
            this.unreadCount.update(c => Math.max(0, c - 1));
        });
    }

    markSelectedAsRead() {
        this.api.markAllNotificationsAsRead().subscribe(() => {
            this.notifications.update(list =>
                list.map(n => this.selectedIds().includes(n.id) ? { ...n, isRead: true } : n)
            );
            this.selectedIds.set([]);
            this.applyFilters();
            this.loadNotifications();
        });
    }

    createNotification() {
        if (this.newNotification.userId && this.newNotification.title && this.newNotification.message) {
            this.api.createNotification(this.newNotification as CreateNotificationRequest).subscribe({
                next: () => {
                    this.showCreateModal = false;
                    this.newNotification = { type: 'Info' };
                    this.showToast('Đã tạo thông báo thành công', 'success');
                    this.loadNotifications();
                },
                error: () => this.showToast('Lỗi khi tạo thông báo', 'error')
            });
        }
    }

    broadcastNotification() {
        if (this.broadcastData.title && this.broadcastData.message) {
            this.api.broadcastNotification(this.broadcastData as BroadcastNotificationRequest).subscribe({
                next: (res) => {
                    this.showBroadcastModal = false;
                    this.broadcastData = { type: 'Info' };
                    this.showToast(`Đã gửi thông báo đến ${res.sent} người dùng`, 'success');
                    this.loadNotifications();
                },
                error: () => this.showToast('Lỗi khi phát thông báo', 'error')
            });
        }
    }

    showToast(message: string, type: 'success' | 'error') {
        this.toastMessage.set(message);
        this.toastType.set(type);
        setTimeout(() => this.toastMessage.set(''), 3000);
    }

    formatTime(dateStr: string): string {
        const date = new Date(dateStr);
        return date.toLocaleString('vi-VN');
    }

    getTypeIcon(type: NotificationType): string {
        const icons: Record<NotificationType, string> = {
            'Info': 'ℹ',
            'Success': '✓',
            'Warning': '⚠',
            'Error': '✕',
            'AppointmentReminder': '📅',
            'QueueCalled': '📢',
            'CycleUpdate': '🔄',
            'PaymentDue': '💰'
        };
        return icons[type] || 'ℹ';
    }

    getTypeLabel(type: NotificationType): string {
        const labels: Record<NotificationType, string> = {
            'Info': 'Thông tin',
            'Success': 'Thành công',
            'Warning': 'Cảnh báo',
            'Error': 'Lỗi',
            'AppointmentReminder': 'Nhắc lịch hẹn',
            'QueueCalled': 'Gọi số',
            'CycleUpdate': 'Cập nhật chu kỳ',
            'PaymentDue': 'Thanh toán'
        };
        return labels[type] || type;
    }
}
