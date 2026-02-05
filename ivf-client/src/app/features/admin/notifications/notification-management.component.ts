import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../../core/services/notification.service';
import { Notification, NotificationType, CreateNotificationRequest, BroadcastNotificationRequest } from '../../../core/models/api.models';

@Component({
  selector: 'app-notification-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './notification-management.component.html',
  styleUrls: ['./notification-management.component.scss']
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

  constructor(private notificationService: NotificationService) { }

  ngOnInit() {
    this.loadNotifications();
  }

  loadNotifications() {
    this.notificationService.getNotifications().subscribe((notifications: Notification[]) => {
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
    this.notificationService.markNotificationAsRead(notification.id).subscribe(() => {
      this.notifications.update(list =>
        list.map(n => n.id === notification.id ? { ...n, isRead: true } : n)
      );
      this.applyFilters();
      this.unreadCount.update(c => Math.max(0, c - 1));
    });
  }

  markSelectedAsRead() {
    this.notificationService.markAllNotificationsAsRead().subscribe(() => {
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
      this.notificationService.createNotification(this.newNotification as CreateNotificationRequest).subscribe({
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
      this.notificationService.broadcastNotification(this.broadcastData as BroadcastNotificationRequest).subscribe({
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
