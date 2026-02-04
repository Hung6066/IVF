import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';
import { ToastNotificationComponent } from '../../shared/components/toast-notification/toast-notification.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent, ToastNotificationComponent],
  template: `
    <div class="layout">
      <aside class="sidebar">
        <div class="sidebar-header">
          <span class="logo">🏥</span>
          <h2 style="color: #ffffff;">IVF System</h2>
        </div>

        <nav class="sidebar-nav">
          <a routerLink="/dashboard" routerLinkActive="active" class="nav-item">
            <span class="icon">📊</span> Dashboard
          </a>
          <a routerLink="/reception" routerLinkActive="active" class="nav-item">
            <span class="icon">🏥</span> Tiếp đón
          </a>
          <a routerLink="/patients" routerLinkActive="active" class="nav-item">
            <span class="icon">👥</span> Bệnh nhân
          </a>
          <a routerLink="/couples" routerLinkActive="active" class="nav-item">
            <span class="icon">💑</span> Cặp đôi
          </a>
          <a routerLink="/queue/US" routerLinkActive="active" class="nav-item">
            <span class="icon">🎫</span> Hàng đợi
          </a>
          <a routerLink="/consultation" routerLinkActive="active" class="nav-item">
            <span class="icon">🗣️</span> Tư vấn
          </a>
          <a routerLink="/ultrasound" routerLinkActive="active" class="nav-item">
            <span class="icon">🔬</span> Siêu âm
          </a>
          <a routerLink="/lab" routerLinkActive="active" class="nav-item">
            <span class="icon">🧫</span> Phòng Lab
          </a>
          <a routerLink="/andrology" routerLinkActive="active" class="nav-item">
            <span class="icon">🔬</span> Nam khoa
          </a>
          <a routerLink="/injection" routerLinkActive="active" class="nav-item">
            <span class="icon">💉</span> Tiêm
          </a>
          <a routerLink="/sperm-bank" routerLinkActive="active" class="nav-item">
            <span class="icon">🏦</span> NHTT
          </a>
          <a routerLink="/pharmacy" routerLinkActive="active" class="nav-item">
            <span class="icon">💊</span> Nhà thuốc
          </a>
          <a routerLink="/billing" routerLinkActive="active" class="nav-item">
            <span class="icon">💰</span> Hoá đơn
          </a>
          <a routerLink="/appointments" routerLinkActive="active" class="nav-item">
            <span class="icon">📅</span> Lịch hẹn
          </a>
          <a routerLink="/reports" routerLinkActive="active" class="nav-item">
            <span class="icon">📈</span> Báo cáo
          </a>
          
          <div style="margin-top: 16px; padding: 0 16px; font-size: 0.75rem; color: #94a3b8; font-weight: 600; text-transform: uppercase; margin-bottom: 8px;">
            Quản trị
          </div>

          <a routerLink="/admin/users" routerLinkActive="active" class="nav-item">
            <span class="icon">👥</span> Người dùng
          </a>
          <a routerLink="/admin/audit-logs" routerLinkActive="active" class="nav-item">
            <span class="icon">📋</span> Nhật ký
          </a>
          <a routerLink="/admin/notifications" routerLinkActive="active" class="nav-item">
            <span class="icon">🔔</span> Thông báo
          </a>
        </nav>

        <div class="sidebar-footer">
          <div class="user-info">
            <span class="avatar">{{ getUserInitials() }}</span>
            <div class="user-details">
              <strong>{{ authService.user()?.fullName }}</strong>
              <small>{{ authService.user()?.role }}</small>
            </div>
          </div>
          <button class="btn-logout" (click)="logout()">🚪 Đăng xuất</button>
        </div>
      </aside>

      <div class="main-wrapper">
        <header class="top-header">
          <div class="header-left">
            <h1 class="page-title">{{ getPageTitle() }}</h1>
          </div>
          <div class="header-right">
            <app-notification-bell></app-notification-bell>
            <div class="user-avatar">{{ getUserInitials() }}</div>
          </div>
        </header>
        <main class="main-content">
          <router-outlet></router-outlet>
        </main>
      </div>

      <!-- Toast Notifications -->
      <app-toast-notification
        [show]="showToast"
        [title]="toastTitle"
        [message]="toastMessage"
        [type]="toastType"
        (dismissed)="dismissToast()"
        (clicked)="handleToastClick()"
      ></app-toast-notification>
    </div>
  `,
  styles: [`
    .layout {
      display: flex;
      min-height: 100vh;
      background: #f1f5f9;
    }

    .sidebar {
      width: 260px;
      background: linear-gradient(180deg, #1e1e2f 0%, #2d2d44 100%);
      color: white;
      display: flex;
      flex-direction: column;
      position: fixed;
      height: 100vh;
      z-index: 100;
    }

    .sidebar-header {
      padding: 1.5rem;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      border-bottom: 1px solid rgba(255,255,255,0.1);
    }

    .logo { font-size: 1.75rem; }

    .sidebar-header h2 {
      font-size: 1.25rem;
      font-weight: 600;
      margin: 0;
    }

    .sidebar-nav {
      flex: 1;
      padding: 1rem 0;
      overflow-y: auto;
    }

    .nav-item {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.875rem 1.5rem;
      color: rgba(255,255,255,0.7);
      text-decoration: none;
      transition: all 0.2s;
      font-size: 0.9375rem;
    }

    .nav-item:hover {
      background: rgba(255,255,255,0.1);
      color: white;
    }

    .nav-item.active {
      background: linear-gradient(90deg, #667eea, #764ba2);
      color: white;
      border-left: 3px solid #a78bfa;
    }

    .icon { font-size: 1.125rem; }

    .sidebar-footer {
      padding: 1rem 1.5rem;
      border-top: 1px solid rgba(255,255,255,0.1);
    }

    .user-info {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .avatar {
      width: 40px;
      height: 40px;
      background: linear-gradient(135deg, #667eea, #764ba2);
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 0.875rem;
    }

    .user-details {
      display: flex;
      flex-direction: column;
    }

    .user-details strong {
      font-size: 0.875rem;
    }

    .user-details small {
      color: rgba(255,255,255,0.6);
      font-size: 0.75rem;
    }

    .btn-logout {
      width: 100%;
      padding: 0.625rem;
      background: rgba(255,255,255,0.1);
      border: 1px solid rgba(255,255,255,0.2);
      border-radius: 8px;
      color: white;
      font-size: 0.875rem;
      cursor: pointer;
      transition: background 0.2s;
    }

    .btn-logout:hover {
      background: rgba(255,255,255,0.2);
    }

    .main-wrapper {
      flex: 1;
      margin-left: 260px;
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }

    .top-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 2rem;
      background: #1e293b;
      border-bottom: 1px solid #334155;
      position: sticky;
      top: 0;
      z-index: 50;
    }

    .header-left .page-title {
      font-size: 1.5rem;
      font-weight: 600;
      color: #f1f5f9;
      margin: 0;
    }

    .header-right {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .user-avatar {
      width: 36px;
      height: 36px;
      background: linear-gradient(135deg, #667eea, #764ba2);
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 0.75rem;
      color: white;
    }

    .main-content {
      flex: 1;
      padding: 2rem;
      overflow-y: auto;
      background: #fffff;
    }
  `]
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
    private router: Router
  ) { }

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

      this.signalRService.notification$.subscribe(notification => {
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
        'Appointment': '/appointments',
        'Invoice': '/billing',
        'TreatmentCycle': '/couples',
        'QueueTicket': '/queue/US'
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
    return name.split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
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
      '/admin/users': 'Quản lý người dùng'
    };

    for (const [key, value] of Object.entries(titles)) {
      if (path.startsWith(key)) return value;
    }
    return 'IVF System';
  }

  logout(): void {
    this.authService.logout();
  }
}

