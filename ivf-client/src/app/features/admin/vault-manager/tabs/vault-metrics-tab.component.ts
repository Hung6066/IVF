import { Component, OnInit, signal, inject, computed, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import { VaultMetrics } from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-vault-metrics-tab',
  standalone: true,
  imports: [CommonModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>📈 Vault Metrics & Observability</h3>
        <button class="btn btn-primary" (click)="loadMetrics()" [disabled]="loading()">
          🔄 Refresh
        </button>
      </div>

      @if (metrics()) {
        <!-- Totals Cards -->
        <div class="card-grid">
          <div class="metric-card">
            <span class="metric-icon">🔑</span>
            <span class="metric-value">{{ metrics()!.totals.secrets }}</span>
            <span class="metric-label">Secrets</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">⏱️</span>
            <span class="metric-value">{{ metrics()!.totals.activeLeases }}</span>
            <span class="metric-label">Active Leases</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">🎟️</span>
            <span class="metric-value">{{ metrics()!.totals.activeTokens }}</span>
            <span class="metric-label">Active Tokens</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">🚫</span>
            <span class="metric-value">{{ metrics()!.totals.revokedTokens }}</span>
            <span class="metric-label">Revoked Tokens</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">⚡</span>
            <span class="metric-value">{{ metrics()!.totals.activeDynamicCredentials }}</span>
            <span class="metric-label">Dynamic Credentials</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">🔄</span>
            <span class="metric-value">{{ metrics()!.totals.rotationSchedules }}</span>
            <span class="metric-label">Rotation Schedules</span>
          </div>
        </div>

        <!-- Last 24 Hours -->
        <div class="section-header" style="margin-top: 2rem;">
          <h3>📊 Hoạt động 24 giờ qua</h3>
        </div>

        <div class="card-grid four-col">
          <div class="metric-card highlight">
            <span class="metric-value">{{ metrics()!.last24Hours.totalOperations }}</span>
            <span class="metric-label">Tổng operations</span>
          </div>
          <div class="metric-card">
            <span class="metric-value">{{ metrics()!.last24Hours.secretOperations }}</span>
            <span class="metric-label">Secret ops</span>
          </div>
          <div class="metric-card">
            <span class="metric-value">{{ metrics()!.last24Hours.rotationOperations }}</span>
            <span class="metric-label">Rotation ops</span>
          </div>
          <div class="metric-card">
            <span class="metric-value">{{ metrics()!.last24Hours.tokenOperations }}</span>
            <span class="metric-label">Token ops</span>
          </div>
        </div>

        <!-- Operations Breakdown -->
        @if (metrics()!.last24Hours.operationsByType.length > 0) {
          <div class="operations-table" style="margin-top: 1.5rem;">
            <h4>Operations by Type</h4>
            <table class="data-table">
              <thead>
                <tr>
                  <th>Operation</th>
                  <th>Count</th>
                  <th>Tỷ lệ</th>
                </tr>
              </thead>
              <tbody>
                @for (op of metrics()!.last24Hours.operationsByType; track op.operation) {
                  <tr>
                    <td class="mono">{{ op.operation }}</td>
                    <td>{{ op.count }}</td>
                    <td>
                      <div class="progress-bar mini">
                        <div
                          class="progress-fill"
                          [style.width.%]="
                            metrics()!.last24Hours.totalOperations > 0
                              ? (op.count / metrics()!.last24Hours.totalOperations) * 100
                              : 0
                          "
                        ></div>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <div class="timestamp">
          Cập nhật lúc: {{ metrics()!.timestamp | date: 'dd/MM/yy HH:mm:ss' }}
        </div>
      }

      @if (!metrics() && !loading()) {
        <div class="empty-state">
          <p>Nhấn "Refresh" để tải metrics</p>
        </div>
      }
    </div>
  `,
})
export class VaultMetricsTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  metrics = signal<VaultMetrics | null>(null);
  loading = signal(false);

  ngOnInit() {
    this.loadMetrics();
  }

  loadMetrics() {
    this.loading.set(true);
    this.kv.getVaultMetrics().subscribe({
      next: (m) => {
        this.metrics.set(m);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
