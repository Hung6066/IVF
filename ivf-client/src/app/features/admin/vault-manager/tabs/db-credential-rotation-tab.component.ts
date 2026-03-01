import { Component, OnInit, signal, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import { DualCredentialStatus } from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-db-credential-rotation-tab',
  standalone: true,
  imports: [CommonModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>🗄️ DB Credential Rotation (A/B Slot Pattern)</h3>
        <button class="btn btn-warning" (click)="rotate()" [disabled]="loading()">
          🔄 Rotate Credential
        </button>
      </div>

      @if (status()) {
        <div class="slot-cards">
          <!-- Slot A -->
          <div class="card" [class.active-slot]="status()!.slotAActive">
            <div class="card-header">
              <h4>Slot A {{ status()!.slotAActive ? '(ACTIVE)' : '' }}</h4>
              <span [class]="status()!.slotAActive ? 'badge badge-success' : 'badge badge-muted'">
                {{ status()!.slotAActive ? '🟢 Active' : '⚪ Standby' }}
              </span>
            </div>
            <div class="card-body">
              <div class="stat-row">
                <span class="label">Username:</span>
                <span class="value mono">{{ status()!.slotAUsername || '—' }}</span>
              </div>
              <div class="stat-row">
                <span class="label">Hết hạn:</span>
                <span class="value">{{
                  status()!.slotAExpiresAt
                    ? (status()!.slotAExpiresAt | date: 'dd/MM/yy HH:mm')
                    : '—'
                }}</span>
              </div>
            </div>
          </div>

          <!-- Slot B -->
          <div class="card" [class.active-slot]="status()!.slotBActive">
            <div class="card-header">
              <h4>Slot B {{ status()!.slotBActive ? '(ACTIVE)' : '' }}</h4>
              <span [class]="status()!.slotBActive ? 'badge badge-success' : 'badge badge-muted'">
                {{ status()!.slotBActive ? '🟢 Active' : '⚪ Standby' }}
              </span>
            </div>
            <div class="card-body">
              <div class="stat-row">
                <span class="label">Username:</span>
                <span class="value mono">{{ status()!.slotBUsername || '—' }}</span>
              </div>
              <div class="stat-row">
                <span class="label">Hết hạn:</span>
                <span class="value">{{
                  status()!.slotBExpiresAt
                    ? (status()!.slotBExpiresAt | date: 'dd/MM/yy HH:mm')
                    : '—'
                }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Summary -->
        <div class="summary-card">
          <div class="stat-row">
            <span class="label">Active Slot:</span>
            <span class="value badge badge-info">{{ status()!.activeSlot }}</span>
          </div>
          <div class="stat-row">
            <span class="label">Lần rotate cuối:</span>
            <span class="value">{{
              status()!.lastRotatedAt ? (status()!.lastRotatedAt | date: 'dd/MM/yy HH:mm') : 'Chưa'
            }}</span>
          </div>
          <div class="stat-row">
            <span class="label">Tổng số lần rotate:</span>
            <span class="value">{{ status()!.rotationCount }}</span>
          </div>
        </div>
      }

      @if (!status() && !loading()) {
        <div class="empty-state">
          <p>Chưa cấu hình DB credential rotation. Thực hiện rotate lần đầu để khởi tạo.</p>
        </div>
      }

      @if (statusMsg()) {
        <div [class]="'status-msg status-' + statusMsg()!.type">{{ statusMsg()!.text }}</div>
      }
    </div>
  `,
})
export class DbCredentialRotationTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  status = signal<DualCredentialStatus | null>(null);
  loading = signal(false);
  statusMsg = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  ngOnInit() {
    this.loadStatus();
  }

  loadStatus() {
    this.kv.getDbCredentialStatus().subscribe({
      next: (s) => this.status.set(s),
      error: () => {},
    });
  }

  rotate() {
    this.loading.set(true);
    this.kv.rotateDbCredential().subscribe({
      next: (r) => {
        this.showStatus(
          r.success ? `Rotate thành công → ${r.newUsername} (${r.activeSlot})` : `Lỗi: ${r.error}`,
          r.success ? 'success' : 'error',
        );
        this.loadStatus();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi rotate DB credential', 'error');
        this.loading.set(false);
      },
    });
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMsg.set({ text, type });
    setTimeout(() => this.statusMsg.set(null), 4000);
  }
}
