import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';
import { GlobalToastComponent } from '../../shared/components/global-toast/global-toast.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent, GlobalToastComponent],
  templateUrl: './main-layout.component.html',
  styleUrls: ['./main-layout.component.scss'],
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  showToast = false;
  toastTitle = '';
  toastMessage = '';
  toastType = '';
  private currentNotification: any = null;

  constructor(
    public authService: AuthService,
    private signalRService: SignalRService,
    private router: Router,
  ) {}

  async ngOnInit() {
    const token = localStorage.getItem('token');
    if (token) {
      await this.initializeSignalR(token);
    }
  }

  async ngOnDestroy() {
    await this.signalRService.stopConnections();
  }

  private async initializeSignalR(token: string) {
    try {
      await this.signalRService.startNotificationConnection(token);
      await this.signalRService.startQueueConnection(token);

      this.signalRService.notification$.subscribe((notification) => {
        if (notification) {
          this.displayToast(notification);
        }
      });
    } catch (error) {
      console.error('SignalR initialization error:', error);
    }
  }

  private displayToast(notification: any) {
    this.currentNotification = notification;
    this.toastTitle = notification.title;
    this.toastMessage = notification.message;
    this.toastType = notification.type;
    this.showToast = true;
    setTimeout(() => this.dismissToast(), 5000);
  }

  dismissToast() {
    this.showToast = false;
  }

  handleToastClick() {
    if (this.currentNotification?.entityType) {
      const routes: Record<string, string> = {
        Appointment: '/appointments',
        Invoice: '/billing',
        TreatmentCycle: '/couples',
        QueueTicket: '/queue/US',
      };
      const route = routes[this.currentNotification.entityType];
      if (route) {
        this.router.navigate([route]);
        this.dismissToast();
      }
    }
  }

  getUserInitials(): string {
    const name = this.authService.user()?.fullName || '';
    return name
      .split(' ')
      .map((w) => w[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  getPageTitle(): string {
    const path = window.location.pathname;
    const titles: Record<string, string> = {
      '/dashboard': 'Dashboard',
      '/reception': 'Tiếp đón',
      '/patients': 'Bệnh nhân',
      '/couples': 'Cặp đôi',
      '/queue': 'Hàng đợi',
      '/consultation': 'Tư vấn',
      '/ultrasound': 'Siêu âm',
      '/lab': 'Phòng Lab',
      '/andrology': 'Nam khoa',
      '/injection': 'Tiêm',
      '/sperm-bank': 'Ngân hàng tinh trùng',
      '/pharmacy': 'Nhà thuốc',
      '/billing': 'Hoá đơn',
      '/appointments': 'Lịch hẹn',
      '/reports': 'Báo cáo',
      '/admin/audit-logs': 'Nhật ký hoạt động',
      '/admin/notifications': 'Quản lý thông báo',
      '/admin/users': 'Quản lý người dùng',
      '/admin/digital-signing': 'Quản lý ký số',
    };

    for (const [key, value] of Object.entries(titles)) {
      if (path.startsWith(key)) return value;
    }
    return 'IVF System';
  }

  logout(): void {
    this.authService.logout();
  }

  // Permission-based menu visibility
  canView(permission: string): boolean {
    return this.authService.hasPermission(permission);
  }

  isAdmin(): boolean {
    return this.authService.hasRole('Admin');
  }
}
