import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import { VaultToken, VaultAuditLog, SecretDetail } from '../../../../core/models/keyvault.model';
import { environment } from '../../../../../environments/environment';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-webhook-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="tab-content">
      <div class="section-header">
        <div>
          <h3>🔔 Webhook Alert Management</h3>
          <p class="text-muted">
            Quản lý webhook token cho programmatic clients (script, CI/CD, hệ thống ngoài)
          </p>
        </div>
        <button class="btn btn-primary" (click)="loadAll()" [disabled]="loading()">
          🔄 Làm mới
        </button>
      </div>

      <!-- Status Cards -->
      <div class="card-grid">
        <div class="metric-card" [class.highlight]="webhookToken()?.isValid">
          <span class="metric-icon">🎟️</span>
          <span class="metric-value">
            @if (webhookToken()) {
              @if (webhookToken()!.isValid) {
                ✅ Active
              } @else if (webhookToken()!.revoked) {
                🚫 Revoked
              } @else {
                ⏰ Expired
              }
            } @else {
              ❌ Không có
            }
          </span>
          <span class="metric-label">Token Status</span>
        </div>

        <div class="metric-card">
          <span class="metric-icon">⏱️</span>
          <span class="metric-value">{{ ttlRemaining() }}</span>
          <span class="metric-label">TTL còn lại</span>
        </div>

        <div class="metric-card">
          <span class="metric-icon">📊</span>
          <span class="metric-value">{{ webhookToken()?.usesCount ?? 0 }}</span>
          <span class="metric-label">Lần sử dụng</span>
        </div>

        <div class="metric-card">
          <span class="metric-icon">🔄</span>
          <span class="metric-value">{{ rotationCount() }}</span>
          <span class="metric-label">Lần xoay token</span>
        </div>
      </div>

      <!-- Token Details -->
      @if (webhookToken()) {
        <div class="card" style="margin-top: 1.5rem;">
          <div class="card-header">
            <h4>🎟️ Chi tiết Webhook Token</h4>
          </div>
          <div class="card-body">
            <div class="detail-grid">
              <div class="detail-row">
                <span class="detail-label">Accessor:</span>
                <code>{{ webhookToken()!.accessor }}</code>
              </div>
              <div class="detail-row">
                <span class="detail-label">Display Name:</span>
                <span>{{ webhookToken()!.displayName }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Policies:</span>
                @for (p of webhookToken()!.policies; track p) {
                  <span class="badge badge-outline">{{ p }}</span>
                }
              </div>
              <div class="detail-row">
                <span class="detail-label">Loại:</span>
                <span class="badge badge-outline">{{ webhookToken()!.tokenType }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Hết hạn:</span>
                <span>{{ webhookToken()!.expiresAt | date: 'dd/MM/yyyy HH:mm:ss' }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Sử dụng gần nhất:</span>
                <span>{{
                  webhookToken()!.lastUsedAt
                    ? (webhookToken()!.lastUsedAt | date: 'dd/MM/yyyy HH:mm:ss')
                    : 'Chưa sử dụng'
                }}</span>
              </div>
              <div class="detail-row">
                <span class="detail-label">Tạo lúc:</span>
                <span>{{ webhookToken()!.createdAt | date: 'dd/MM/yyyy HH:mm:ss' }}</span>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Actions -->
      <div class="card" style="margin-top: 1.5rem;">
        <div class="card-header">
          <h4>⚡ Thao tác</h4>
        </div>
        <div class="card-body">
          <div class="action-buttons">
            <!-- Copy Token -->
            <button
              class="btn btn-primary"
              (click)="copyToken()"
              [disabled]="loadingToken() || !webhookToken()?.isValid"
            >
              📋 Sao chép Token
            </button>

            <!-- Test Webhook -->
            <button
              class="btn btn-outline"
              (click)="testWebhook()"
              [disabled]="testingWebhook() || !webhookToken()?.isValid"
            >
              @if (testingWebhook()) {
                ⏳ Đang test...
              } @else {
                🧪 Test Webhook
              }
            </button>

            <!-- Manual Rotate -->
            <button class="btn btn-warning" (click)="manualRotate()" [disabled]="rotating()">
              @if (rotating()) {
                ⏳ Đang xoay...
              } @else {
                🔄 Xoay Token Ngay
              }
            </button>
          </div>

          <!-- Token Value (shown after copy) -->
          @if (tokenValue()) {
            <div class="alert alert-success" style="margin-top: 1rem;">
              <div class="alert-title">🔑 Token hiện tại</div>
              <div class="result-row">
                <code class="result-mono" style="word-break: break-all;">{{ tokenValue() }}</code>
                <button class="btn btn-xs btn-outline" (click)="copyToClipboard(tokenValue()!)">
                  📋 Copy
                </button>
              </div>
              <div style="margin-top: 0.5rem; font-size: 0.85rem; color: var(--text-muted);">
                Sử dụng trong header: <code>X-Webhook-Token: {{ tokenValue() }}</code>
              </div>
              <button
                class="btn btn-xs btn-outline"
                style="margin-top: 0.5rem;"
                (click)="tokenValue.set(null)"
              >
                ✕ Ẩn
              </button>
            </div>
          }

          <!-- Test Result -->
          @if (testResult()) {
            <div
              class="alert"
              [class.alert-success]="testResult()!.success"
              [class.alert-error]="!testResult()!.success"
              style="margin-top: 1rem;"
            >
              <div class="alert-title">
                {{ testResult()!.success ? '✅ Test thành công' : '❌ Test thất bại' }}
              </div>
              <div>{{ testResult()!.message }}</div>
              <button
                class="btn btn-xs btn-outline"
                style="margin-top: 0.5rem;"
                (click)="testResult.set(null)"
              >
                ✕ Ẩn
              </button>
            </div>
          }

          <!-- Status message -->
          @if (statusMsg()) {
            <div
              class="alert"
              [class.alert-success]="statusMsg()!.type === 'success'"
              [class.alert-error]="statusMsg()!.type === 'error'"
              style="margin-top: 1rem;"
            >
              {{ statusMsg()!.text }}
            </div>
          }
        </div>
      </div>

      <!-- Usage Guide -->
      <div class="card" style="margin-top: 1.5rem;">
        <div class="card-header">
          <h4>📖 Hướng dẫn sử dụng (Pull-then-Send)</h4>
        </div>
        <div class="card-body">
          <div class="guide-steps">
            <div class="guide-step">
              <span class="step-number">1</span>
              <div class="step-content">
                <strong>Pull token</strong> — Lấy token mới nhất từ API
                <pre class="code-block" [textContent]="pullTokenExample"></pre>
              </div>
            </div>
            <div class="guide-step">
              <span class="step-number">2</span>
              <div class="step-content">
                <strong>Send alert</strong> — Gửi alert với token vừa lấy
                <pre class="code-block" [textContent]="sendAlertExample"></pre>
              </div>
            </div>
          </div>
          <div class="guide-note">
            <strong>⚠️ Lưu ý:</strong> Webhook dành cho <em>programmatic clients</em> có khả năng tự
            pull token (script, CI/CD, hệ thống ngoài). KHÔNG dùng cho Grafana/Prometheus (static
            config). Token tự động xoay mỗi 24h — client luôn pull token mới trước khi gửi.
          </div>
        </div>
      </div>

      <!-- Audit History -->
      <div class="card" style="margin-top: 1.5rem;">
        <div class="card-header">
          <h4>📜 Lịch sử Webhook Token</h4>
        </div>
        <div class="card-body">
          @if (auditLogs().length > 0) {
            <table class="data-table">
              <thead>
                <tr>
                  <th>Thời gian</th>
                  <th>Hành động</th>
                  <th>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                @for (log of auditLogs(); track log.id) {
                  <tr>
                    <td>{{ log.createdAt | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
                    <td>
                      <span
                        class="badge"
                        [class.badge-success]="log.action === 'webhook-token.create'"
                        [class.badge-outline]="log.action === 'webhook-token.rotate'"
                        [class.badge-warning]="log.action === 'webhook-token.test'"
                      >
                        @switch (log.action) {
                          @case ('webhook-token.create') {
                            🆕 Tạo mới
                          }
                          @case ('webhook-token.rotate') {
                            🔄 Xoay
                          }
                          @case ('webhook-token.test') {
                            🧪 Test
                          }
                          @default {
                            {{ log.action }}
                          }
                        }
                      </span>
                    </td>
                    <td>
                      <code>{{ log.details }}</code>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <div class="empty-state">📜 Chưa có lịch sử webhook token</div>
          }
        </div>
      </div>

      <!-- Webhook Health -->
      <div class="card" style="margin-top: 1.5rem;">
        <div class="card-header">
          <h4>💚 Webhook Health</h4>
        </div>
        <div class="card-body">
          @if (healthStatus()) {
            <div class="detail-row">
              <span class="detail-label">Trạng thái:</span>
              <span class="badge badge-success">{{ healthStatus()!.status }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Timestamp:</span>
              <span>{{ healthStatus()!.timestamp | date: 'dd/MM/yyyy HH:mm:ss' }}</span>
            </div>
          } @else {
            <span class="text-muted">Nhấn "Làm mới" để kiểm tra</span>
          }
        </div>
      </div>
    </div>
  `,
  styles: `
    .card-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 1rem;
    }

    .metric-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 1.25rem;
      border-radius: 12px;
      background: var(--card-bg, #f8fafc);
      border: 1px solid var(--border-color, #e2e8f0);
      text-align: center;
      transition: all 0.2s ease;
    }
    .metric-card.highlight {
      border-color: var(--success-color, #22c55e);
      background: var(--success-bg, #f0fdf4);
    }
    .metric-icon {
      font-size: 1.5rem;
      margin-bottom: 0.5rem;
    }
    .metric-value {
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--text-primary);
    }
    .metric-label {
      font-size: 0.8rem;
      color: var(--text-muted, #64748b);
      margin-top: 0.25rem;
    }

    .detail-grid {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .detail-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid var(--border-color, #e2e8f0);
    }
    .detail-row:last-child {
      border-bottom: none;
    }
    .detail-label {
      font-weight: 600;
      min-width: 150px;
      color: var(--text-muted, #64748b);
    }

    .action-buttons {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .guide-steps {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .guide-step {
      display: flex;
      gap: 1rem;
      align-items: flex-start;
    }
    .step-number {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: var(--primary-color, #3b82f6);
      color: white;
      font-weight: 700;
      flex-shrink: 0;
    }
    .step-content {
      flex: 1;
    }
    .code-block {
      display: block;
      margin-top: 0.5rem;
      padding: 0.75rem;
      background: var(--code-bg, #1e293b);
      color: var(--code-text, #e2e8f0);
      border-radius: 8px;
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.8rem;
      overflow-x: auto;
      white-space: pre;
    }
    .guide-note {
      margin-top: 1rem;
      padding: 0.75rem;
      background: var(--warning-bg, #fffbeb);
      border: 1px solid var(--warning-border, #fbbf24);
      border-radius: 8px;
      font-size: 0.85rem;
    }

    .result-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .result-mono {
      font-family: monospace;
      font-size: 0.85rem;
    }
    .alert {
      padding: 1rem;
      border-radius: 8px;
    }
    .alert-success {
      background: var(--success-bg, #f0fdf4);
      border: 1px solid var(--success-color, #22c55e);
    }
    .alert-error {
      background: var(--error-bg, #fef2f2);
      border: 1px solid var(--error-color, #ef4444);
    }
    .alert-title {
      font-weight: 700;
      margin-bottom: 0.25rem;
    }
    .empty-state {
      padding: 2rem;
      text-align: center;
      color: var(--text-muted, #64748b);
    }

    .btn-warning {
      background: var(--warning-color, #f59e0b);
      color: white;
      border: none;
      padding: 0.5rem 1rem;
      border-radius: 8px;
      cursor: pointer;
      font-weight: 600;
    }
    .btn-warning:hover {
      opacity: 0.9;
    }
    .btn-warning:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .badge-warning {
      background: var(--warning-bg, #fffbeb);
      color: var(--warning-color, #f59e0b);
    }
  `,
})
export class WebhookTabComponent implements OnInit {
  private kv = inject(KeyVaultService);
  private http = inject(HttpClient);

  readonly apiUrl = environment.apiUrl;

  readonly pullTokenExample = `curl -s ${environment.apiUrl}/keyvault/secrets/webhooks%2Falert-token \\
  -H "Authorization: Bearer <JWT>" | jq -r '.value'`;

  readonly sendAlertExample = `curl -X POST ${environment.apiUrl}/webhooks/alerts/ \\
  -H "X-Webhook-Token: <TOKEN>" \\
  -H "Content-Type: application/json" \\
  -d '{"source":"Custom","message":"Alert message","level":"warning"}'`;

  // State
  loading = signal(false);
  loadingToken = signal(false);
  testingWebhook = signal(false);
  rotating = signal(false);

  // Data
  webhookToken = signal<VaultToken | null>(null);
  tokenValue = signal<string | null>(null);
  auditLogs = signal<VaultAuditLog[]>([]);
  healthStatus = signal<{ status: string; timestamp: string } | null>(null);
  testResult = signal<{ success: boolean; message: string } | null>(null);
  statusMsg = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  // Computed
  rotationCount = computed(() => {
    return this.auditLogs().filter((l) => l.action === 'webhook-token.rotate').length;
  });

  ttlRemaining = computed(() => {
    const token = this.webhookToken();
    if (!token?.expiresAt) return '—';
    const diff = new Date(token.expiresAt).getTime() - Date.now();
    if (diff <= 0) return 'Hết hạn';
    const hours = Math.floor(diff / 3600000);
    const minutes = Math.floor((diff % 3600000) / 60000);
    return `${hours}h ${minutes}m`;
  });

  ngOnInit() {
    this.loadAll();
  }

  loadAll() {
    this.loading.set(true);
    this.loadWebhookToken();
    this.loadAuditLogs();
    this.loadHealth();
  }

  private loadWebhookToken() {
    this.kv.getTokens().subscribe({
      next: (tokens) => {
        const webhookTokens = tokens.filter(
          (t) => t.displayName === 'webhook-alert-token' && t.isValid,
        );
        this.webhookToken.set(webhookTokens.length > 0 ? webhookTokens[0] : null);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.showStatus('Không thể tải thông tin token', 'error');
      },
    });
  }

  private loadAuditLogs() {
    this.kv.getAuditLogs(1, 50, 'webhook-token').subscribe({
      next: (res) => this.auditLogs.set(res.items),
      error: () => {},
    });
  }

  private loadHealth() {
    this.http
      .get<{ status: string; timestamp: string }>(`${environment.apiUrl}/webhooks/alerts/health`)
      .subscribe({
        next: (h) => this.healthStatus.set(h),
        error: () => this.healthStatus.set(null),
      });
  }

  copyToken() {
    this.loadingToken.set(true);
    this.kv.getSecret('webhooks/alert-token').subscribe({
      next: (secret) => {
        this.tokenValue.set(secret.value);
        this.copyToClipboard(secret.value);
        this.loadingToken.set(false);
      },
      error: () => {
        this.loadingToken.set(false);
        this.showStatus('Không thể lấy token. Vault chưa khởi tạo?', 'error');
      },
    });
  }

  testWebhook() {
    this.testingWebhook.set(true);
    this.testResult.set(null);

    // First pull token, then send test alert
    this.kv.getSecret('webhooks/alert-token').subscribe({
      next: (secret) => {
        this.http
          .post<{ success: boolean; processed: number }>(
            `${environment.apiUrl}/webhooks/alerts/`,
            {
              source: 'UI Test',
              message: 'Test alert từ Vault Manager UI',
              level: 'warning',
            },
            { headers: { 'X-Webhook-Token': secret.value } },
          )
          .subscribe({
            next: (res) => {
              this.testingWebhook.set(false);
              this.testResult.set({
                success: res.success,
                message: `Alert đã gửi thành công (processed: ${res.processed}). Kiểm tra Discord.`,
              });
            },
            error: (err) => {
              this.testingWebhook.set(false);
              this.testResult.set({
                success: false,
                message: `Lỗi: ${err.status} — ${err.statusText || 'Không thể gửi alert'}`,
              });
            },
          });
      },
      error: () => {
        this.testingWebhook.set(false);
        this.testResult.set({
          success: false,
          message: 'Không thể lấy webhook token từ vault',
        });
      },
    });
  }

  manualRotate() {
    this.rotating.set(true);
    this.http
      .post<{
        success: boolean;
        message: string;
      }>(`${environment.apiUrl}/webhooks/alerts/rotate`, {})
      .subscribe({
        next: (res) => {
          this.rotating.set(false);
          this.showStatus(res.message || 'Token đã được xoay thành công', 'success');
          this.tokenValue.set(null); // Clear cached token
          this.loadAll(); // Reload everything
        },
        error: (err) => {
          this.rotating.set(false);
          this.showStatus(
            `Lỗi xoay token: ${err.status} — ${err.error?.error || err.statusText}`,
            'error',
          );
        },
      });
  }

  copyToClipboard(value: string) {
    navigator.clipboard.writeText(value).then(
      () => this.showStatus('Đã sao chép!', 'success'),
      () => this.showStatus('Không thể sao chép', 'error'),
    );
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMsg.set({ text, type });
    setTimeout(() => this.statusMsg.set(null), 4000);
  }
}
