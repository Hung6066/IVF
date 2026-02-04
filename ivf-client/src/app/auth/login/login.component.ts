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
    }

    .login-card {
      background: white;
      border-radius: 16px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
      padding: 2.5rem;
      width: 100%;
      max-width: 400px;
    }

    .login-header {
      text-align: center;
      margin-bottom: 2rem;
    }

    .logo {
      font-size: 3rem;
      margin-bottom: 0.5rem;
    }

    .login-header h1 {
      font-size: 1.5rem;
      color: #1a1a2e;
      margin: 0 0 0.5rem;
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
    }

    .form-group input {
      padding: 0.75rem 1rem;
      border: 1px solid #d1d5db;
      border-radius: 8px;
      font-size: 1rem;
      transition: border-color 0.2s, box-shadow 0.2s;
    }

    .form-group input:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.2);
    }

    .form-group input:disabled {
      background: #f3f4f6;
      cursor: not-allowed;
    }

    .btn-login {
      padding: 0.875rem;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border: none;
      border-radius: 8px;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }

    .btn-login:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
    }

    .btn-login:disabled {
      opacity: 0.7;
      cursor: not-allowed;
    }

    .spinner {
      width: 16px;
      height: 16px;
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
      border-radius: 8px;
      font-size: 0.875rem;
    }

    .login-footer {
      margin-top: 2rem;
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
