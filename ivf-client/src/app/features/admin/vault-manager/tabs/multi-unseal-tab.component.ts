import { Component, OnInit, signal, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import {
  UnsealProviderStatus,
  UnsealProviderConfigureRequest,
} from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-multi-unseal-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>🔓 Multi-Provider Unseal</h3>
        <div class="header-actions">
          <button class="btn btn-primary" (click)="unseal()" [disabled]="loading()">
            🔓 Auto Unseal
          </button>
          <button class="btn btn-secondary" (click)="showConfigDialog.set(true)">+ Provider</button>
        </div>
      </div>

      <!-- Provider Cards -->
      <div class="card-grid">
        @for (p of providers(); track p.providerId) {
          <div
            class="card"
            [class.provider-available]="p.available"
            [class.provider-down]="!p.available"
          >
            <div class="card-header">
              <h4>{{ getProviderIcon(p.providerType) }} {{ p.providerId }}</h4>
              <span [class]="p.available ? 'badge badge-success' : 'badge badge-danger'">
                {{ p.available ? 'Available' : 'Unavailable' }}
              </span>
            </div>
            <div class="card-body">
              <div class="stat-row">
                <span class="label">Type:</span>
                <span class="value">{{ p.providerType }}</span>
              </div>
              <div class="stat-row">
                <span class="label">Priority:</span>
                <span class="value">{{ p.priority }}</span>
              </div>
              <div class="stat-row">
                <span class="label">Last Used:</span>
                <span class="value">{{
                  p.lastUsedAt ? (p.lastUsedAt | date: 'dd/MM/yy HH:mm') : 'Never'
                }}</span>
              </div>
              @if (p.error) {
                <div class="stat-row error">
                  <span class="label">Error:</span>
                  <span class="value">{{ p.error }}</span>
                </div>
              }
            </div>
          </div>
        }
        @if (providers().length === 0) {
          <div class="card empty-card">
            <p>Chưa có unseal provider nào. Thêm provider để sử dụng multi-provider unseal.</p>
          </div>
        }
      </div>

      <!-- Unseal Result -->
      @if (unsealResult()) {
        <div [class]="'alert ' + (unsealResult()!.success ? 'alert-success' : 'alert-danger')">
          <strong>{{ unsealResult()!.success ? '✅' : '❌' }}</strong>
          {{
            unsealResult()!.success
              ? 'Unseal thành công via ' + unsealResult()!.providerId
              : 'Unseal thất bại: ' + unsealResult()!.error
          }}
          ({{ unsealResult()!.attemptsTotal }} attempts)
        </div>
      }

      <!-- Configure Dialog -->
      @if (showConfigDialog()) {
        <div class="dialog-overlay" (click)="showConfigDialog.set(false)">
          <div class="dialog" (click)="$event.stopPropagation()">
            <div class="dialog-header">
              <h3>➕ Configure Unseal Provider</h3>
              <button class="btn-close" (click)="showConfigDialog.set(false)">✕</button>
            </div>
            <div class="dialog-body">
              <div class="form-group">
                <label>Provider ID</label>
                <input
                  type="text"
                  [(ngModel)]="configForm.providerId"
                  placeholder="azure-primary"
                />
              </div>
              <div class="form-group">
                <label>Type</label>
                <select [(ngModel)]="configForm.providerType">
                  <option value="Azure">Azure Key Vault</option>
                  <option value="Local">Local AES</option>
                  <option value="Shamir">Shamir Split</option>
                </select>
              </div>
              <div class="form-group">
                <label>Priority (thấp = ưu tiên cao)</label>
                <input type="number" [(ngModel)]="configForm.priority" min="1" />
              </div>
              <div class="form-group">
                <label>Key Identifier</label>
                <input
                  type="text"
                  [(ngModel)]="configForm.keyIdentifier"
                  placeholder="unseal-key"
                />
              </div>
              <div class="form-group">
                <label>Master Password</label>
                <input type="password" [(ngModel)]="configForm.masterPassword" />
              </div>
              <div class="form-actions">
                <button
                  class="btn btn-primary"
                  (click)="configureProvider()"
                  [disabled]="loading()"
                >
                  Lưu
                </button>
                <button class="btn btn-secondary" (click)="showConfigDialog.set(false)">Hủy</button>
              </div>
            </div>
          </div>
        </div>
      }

      @if (statusMsg()) {
        <div [class]="'status-msg status-' + statusMsg()!.type">{{ statusMsg()!.text }}</div>
      }
    </div>
  `,
})
export class MultiUnsealTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  providers = signal<UnsealProviderStatus[]>([]);
  unsealResult = signal<{
    success: boolean;
    providerId: string;
    error: string | null;
    attemptsTotal: number;
  } | null>(null);
  showConfigDialog = signal(false);
  loading = signal(false);
  statusMsg = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  configForm: UnsealProviderConfigureRequest = {
    providerId: '',
    providerType: 'Azure',
    priority: 1,
    keyIdentifier: 'unseal-key',
    masterPassword: '',
  };

  ngOnInit() {
    this.loadProviders();
  }

  loadProviders() {
    this.kv.getUnsealProviders().subscribe({
      next: (p) => this.providers.set(p),
      error: () => {},
    });
  }

  unseal() {
    this.loading.set(true);
    this.kv.multiProviderUnseal().subscribe({
      next: (r) => {
        this.unsealResult.set(r);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi unseal', 'error');
        this.loading.set(false);
      },
    });
  }

  configureProvider() {
    this.loading.set(true);
    this.kv.configureUnsealProvider(this.configForm).subscribe({
      next: () => {
        this.showStatus('Provider configured thành công', 'success');
        this.showConfigDialog.set(false);
        this.loadProviders();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi configure provider', 'error');
        this.loading.set(false);
      },
    });
  }

  getProviderIcon(type: string): string {
    const icons: Record<string, string> = { Azure: '☁️', Local: '💻', Shamir: '🔑' };
    return icons[type] || '🔓';
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMsg.set({ text, type });
    setTimeout(() => this.statusMsg.set(null), 4000);
  }
}
