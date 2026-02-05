import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../../core/services/notification.service';
import { Notification } from '../../../core/models/api.models';
import { interval, Subject } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="notification-bell" (click)="toggleDropdown()">
      <div class="bell-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
        </svg>
        @if (unreadCount() > 0) {
          <span class="badge">{{ unreadCount() > 99 ? '99+' : unreadCount() }}</span>
        }
      </div>
      
      @if (isOpen()) {
        <div class="dropdown" (click)="$event.stopPropagation()">
          <div class="dropdown-header">
            <span>Thông báo</span>
            @if (unreadCount() > 0) {
              <button class="mark-all" (click)="markAllAsRead()">Đánh dấu đã đọc</button>
            }
          </div>
          
          <div class="notification-list">
            @for (notification of notifications().slice(0, 10); track notification.id) {
              <div class="notification-item" [class.unread]="!notification.isRead" (click)="markAsRead(notification)">
                <div class="notification-icon" [class]="notification.type.toLowerCase()">
                  @switch (notification.type) {
                    @case ('Success') { ✓ }
                    @case ('Warning') { ⚠ }
                    @case ('Error') { ✕ }
                    @case ('AppointmentReminder') { 📅 }
                    @case ('QueueCalled') { 📢 }
                    @default { ℹ }
                  }
                </div>
                <div class="notification-content">
                  <div class="notification-title">{{ notification.title }}</div>
                  <div class="notification-message">{{ notification.message }}</div>
                  <div class="notification-time">{{ formatTime(notification.createdAt) }}</div>
                </div>
              </div>
            } @empty {
              <div class="empty-state">Không có thông báo</div>
            }
          </div>
          
          <div class="dropdown-footer">
            <a href="/admin/notifications" class="view-all" (click)="closeDropdown()">Xem tất cả thông báo →</a>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .notification-bell {
      position: relative;
      cursor: pointer;
    }
    
    .bell-icon {
      position: relative;
      padding: 8px;
      border-radius: 50%;
      transition: background 0.2s;
      
      &:hover {
        background: rgba(255, 255, 255, 0.1);
      }
      
      svg {
        color: #e0e0e0;
      }
    }
    
    .badge {
      position: absolute;
      top: 2px;
      right: 2px;
      background: #ef4444;
      color: white;
      font-size: 10px;
      font-weight: 600;
      padding: 2px 5px;
      border-radius: 10px;
      min-width: 16px;
      text-align: center;
    }
    
    .dropdown {
      position: absolute;
      top: 100%;
      right: 0;
      width: 360px;
      max-height: 480px;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 12px;
      box-shadow: 0 10px 40px rgba(0, 0, 0, 0.4);
      z-index: 1000;
      overflow: hidden;
    }
    
    .dropdown-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px;
      border-bottom: 1px solid #334155;
      font-weight: 600;
      color: #f1f5f9;
    }
    
    .mark-all {
      background: none;
      border: none;
      color: #60a5fa;
      cursor: pointer;
      font-size: 12px;
      
      &:hover {
        text-decoration: underline;
      }
    }
    
    .notification-list {
      max-height: 400px;
      overflow-y: auto;
    }
    
    .notification-item {
      display: flex;
      gap: 12px;
      padding: 12px 16px;
      border-bottom: 1px solid #334155;
      cursor: pointer;
      transition: background 0.2s;
      
      &:hover {
        background: #334155;
      }
      
      &.unread {
        background: rgba(96, 165, 250, 0.1);
      }
    }
    
    .notification-icon {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 16px;
      flex-shrink: 0;
      
      &.info { background: rgba(96, 165, 250, 0.2); }
      &.success { background: rgba(34, 197, 94, 0.2); }
      &.warning { background: rgba(234, 179, 8, 0.2); }
      &.error { background: rgba(239, 68, 68, 0.2); }
      &.appointmentreminder { background: rgba(168, 85, 247, 0.2); }
      &.queuecalled { background: rgba(20, 184, 166, 0.2); }
    }
    
    .notification-content {
      flex: 1;
      min-width: 0;
    }
    
    .notification-title {
      font-weight: 500;
      color: #f1f5f9;
      margin-bottom: 4px;
    }
    
    .notification-message {
      font-size: 13px;
      color: #94a3b8;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    
    .notification-time {
      font-size: 11px;
      color: #64748b;
      margin-top: 4px;
    }
    
    .empty-state {
      padding: 40px;
      text-align: center;
      color: #64748b;
    }
    
    .dropdown-footer {
      padding: 12px 16px;
      border-top: 1px solid #334155;
      text-align: center;
    }
    
    .view-all {
      color: #60a5fa;
      text-decoration: none;
      font-size: 13px;
      font-weight: 500;
    }
    
    .view-all:hover {
      text-decoration: underline;
    }
  `]
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  notifications = signal<Notification[]>([]);
  unreadCount = signal(0);
  isOpen = signal(false);

  constructor(private notificationService: NotificationService) { }

  ngOnInit() {
    this.loadNotifications();
    this.loadUnreadCount();

    // Poll for new notifications every 30 seconds
    interval(30000).pipe(
      takeUntil(this.destroy$),
      switchMap(() => this.notificationService.getUnreadCount())
    ).subscribe(res => this.unreadCount.set(res.count));
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadNotifications() {
    this.notificationService.getNotifications().subscribe(notifications => {
      this.notifications.set(notifications);
    });
  }

  loadUnreadCount() {
    this.notificationService.getUnreadCount().subscribe(res => {
      this.unreadCount.set(res.count);
    });
  }

  toggleDropdown() {
    this.isOpen.update(v => !v);
    if (this.isOpen()) {
      this.loadNotifications();
    }
  }

  markAsRead(notification: Notification) {
    if (!notification.isRead) {
      this.notificationService.markNotificationAsRead(notification.id).subscribe(() => {
        this.notifications.update(list =>
          list.map(n => n.id === notification.id ? { ...n, isRead: true } : n)
        );
        this.unreadCount.update(c => Math.max(0, c - 1));
      });
    }
  }

  markAllAsRead() {
    this.notificationService.markAllNotificationsAsRead().subscribe(() => {
      this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
      this.unreadCount.set(0);
    });
  }

  closeDropdown() {
    this.isOpen.set(false);
  }

  formatTime(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (minutes < 1) return 'Vừa xong';
    if (minutes < 60) return `${minutes} phút trước`;
    if (hours < 24) return `${hours} giờ trước`;
    if (days < 7) return `${days} ngày trước`;
    return date.toLocaleDateString('vi-VN');
  }
}
