import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <div class="login-header">
          <div class="logo">🏥</div>
          <h1>IVF Information System</h1>
          <p>Hệ thống quản lý thụ tinh trong ống nghiệm</p>
        </div>

        <form (ngSubmit)="onSubmit()" class="login-form">
          @if (error()) {
            <div class="error-message">{{ error() }}</div>
          }

          <div class="form-group">
            <label for="username">Tên đăng nhập</label>
            <input
              type="text"
              id="username"
              [(ngModel)]="username"
              name="username"
              placeholder="Nhập tên đăng nhập"
              required
              [disabled]="loading()"
            />
          </div>

          <div class="form-group">
            <label for="password">Mật khẩu</label>
            <input
              type="password"
              id="password"
              [(ngModel)]="password"
              name="password"
              placeholder="Nhập mật khẩu"
              required
              [disabled]="loading()"
            />
          </div>

          <button type="submit" class="btn-login" [disabled]="loading()">
            @if (loading()) {
              <span class="spinner"></span> Đang đăng nhập...
            } @else {
              Đăng nhập
            }
          </button>
        </form>

        <div class="login-footer">
          <p>© 2026 IVF System - Version 1.0</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 1rem;
      position: relative;
      overflow: hidden;
    }

    /* Abstract Background Shapes */
    .login-container::before {
        content: '';
        position: absolute;
        width: 300px;
        height: 300px;
        background: rgba(255, 255, 255, 0.1);
        border-radius: 50%;
        top: -50px;
        left: -50px;
        z-index: 0;
    }
    
    .login-container::after {
        content: '';
        position: absolute;
        width: 250px;
        height: 250px;
        background: rgba(255, 255, 255, 0.15);
        border-radius: 50%;
        bottom: -50px;
        right: -50px;
        z-index: 0;
    }

    .login-card {
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(20px);
      -webkit-backdrop-filter: blur(20px);
      border-radius: 24px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
      padding: 3rem;
      width: 100%;
      max-width: 420px;
      position: relative;
      z-index: 1;
      border: 1px solid rgba(255, 255, 255, 0.5);
      animation: slideUp 0.5s ease-out;
    }

    @keyframes slideUp {
        from { transform: translateY(20px); opacity: 0; }
        to { transform: translateY(0); opacity: 1; }
    }

    .login-header {
      text-align: center;
      margin-bottom: 2.5rem;
    }

    .logo {
      font-size: 3.5rem;
      margin-bottom: 0.5rem;
      filter: drop-shadow(0 4px 6px rgba(0,0,0,0.1));
    }

    .login-header h1 {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1a1a2e;
      margin: 0 0 0.5rem;
      letter-spacing: -0.5px;
    }

    .login-header p {
      color: #6b7280;
      font-size: 0.875rem;
      margin: 0;
    }

    .login-form {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .form-group label {
      font-size: 0.875rem;
      font-weight: 500;
      color: #374151;
      margin-left: 0.25rem;
    }

    .form-group input {
      padding: 0.875rem 1rem;
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      font-size: 1rem;
      transition: all 0.2s;
      background: rgba(255, 255, 255, 0.8);
    }

    .form-group input:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 4px rgba(102, 126, 234, 0.15);
      background: white;
    }

    .btn-login {
      padding: 1rem;
      margin-top: 1rem;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border: none;
      border-radius: 12px;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.3s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      box-shadow: 0 4px 6px -1px rgba(102, 126, 234, 0.4);
    }

    .btn-login:hover:not(:disabled) {
      transform: translateY(-2px);
      box-shadow: 0 10px 15px -3px rgba(102, 126, 234, 0.5);
    }

    .btn-login:disabled {
      opacity: 0.7;
      cursor: not-allowed;
    }

    .spinner {
      width: 20px;
      height: 20px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .error-message {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #dc2626;
      padding: 0.75rem 1rem;
      border-radius: 12px;
      font-size: 0.875rem;
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .login-footer {
      margin-top: 2.5rem;
      text-align: center;
    }

    .login-footer p {
      color: #9ca3af;
      font-size: 0.75rem;
      margin: 0;
    }
  `]
})
export class LoginComponent {
  username = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  constructor(private authService: AuthService, private router: Router) { }

  onSubmit(): void {
    if (!this.username || !this.password) {
      this.error.set('Vui lòng nhập đầy đủ thông tin');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.status === 401 ? 'Sai tên đăng nhập hoặc mật khẩu' : 'Lỗi kết nối máy chủ');
      }
    });
  }
}
