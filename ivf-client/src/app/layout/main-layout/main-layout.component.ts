import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="layout">
      <aside class="sidebar">
        <div class="sidebar-header">
          <span class="logo">🏥</span>
          <h2>IVF System</h2>
        </div>

        <nav class="sidebar-nav">
          <a routerLink="/dashboard" routerLinkActive="active" class="nav-item">
            <span class="icon">📊</span> Dashboard
          </a>
          <a routerLink="/patients" routerLinkActive="active" class="nav-item">
            <span class="icon">👥</span> Bệnh nhân
          </a>
          <a routerLink="/couples" routerLinkActive="active" class="nav-item">
            <span class="icon">💑</span> Cặp đôi
          </a>
          <a routerLink="/queue/REC" routerLinkActive="active" class="nav-item">
            <span class="icon">🎫</span> Tiếp đón
          </a>
          <a routerLink="/queue/US" routerLinkActive="active" class="nav-item">
            <span class="icon">🔬</span> Siêu âm
          </a>
          <a routerLink="/queue/LAB" routerLinkActive="active" class="nav-item">
            <span class="icon">🧪</span> Xét nghiệm
          </a>
          <a routerLink="/queue/AND" routerLinkActive="active" class="nav-item">
            <span class="icon">🔬</span> Nam khoa
          </a>
          <a routerLink="/billing" routerLinkActive="active" class="nav-item">
            <span class="icon">💰</span> Hoá đơn
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

      <main class="main-content">
        <router-outlet></router-outlet>
      </main>
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

    .main-content {
      flex: 1;
      margin-left: 260px;
      padding: 2rem;
      overflow-y: auto;
    }
  `]
})
export class MainLayoutComponent {
  constructor(public authService: AuthService) { }

  getUserInitials(): string {
    const name = this.authService.user()?.fullName || '';
    return name.split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
  }

  logout(): void {
    this.authService.logout();
  }
}
