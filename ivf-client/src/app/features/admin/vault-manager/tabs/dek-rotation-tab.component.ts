import { Component, OnInit, signal, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import {
  DekVersionInfo,
  DekRotationResult,
  ReEncryptionProgress,
  EncryptionConfigResponse,
} from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-dek-rotation-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>🔐 DEK (Data Encryption Key) Rotation</h3>
      </div>

      <!-- DEK Status Cards -->
      <div class="card-grid">
        @for (dek of dekVersions(); track dek.dekPurpose) {
          <div class="card">
            <div class="card-header">
              <span class="card-icon">🔑</span>
              <h4>{{ dek.dekPurpose | uppercase }}</h4>
            </div>
            <div class="card-body">
              <div class="stat-row">
                <span class="label">Version hiện tại:</span>
                <span class="value">v{{ dek.currentVersion }}</span>
              </div>
              <div class="stat-row">
                <span class="label">Lần rotate cuối:</span>
                <span class="value">{{
                  dek.lastRotatedAt ? (dek.lastRotatedAt | date: 'dd/MM/yy HH:mm') : 'Chưa'
                }}</span>
              </div>
              <div class="stat-row">
                <span class="label">Versions cũ giữ lại:</span>
                <span class="value">{{ dek.oldVersionsKept }}</span>
              </div>
            </div>
            <div class="card-actions">
              <button
                class="btn btn-warning btn-sm"
                (click)="rotateDek(dek.dekPurpose)"
                [disabled]="loading()"
              >
                🔄 Rotate DEK
              </button>
            </div>
          </div>
        }
        @if (dekVersions().length === 0) {
          <div class="card empty-card">
            <p>Chưa có DEK nào được khởi tạo</p>
          </div>
        }
      </div>

      <!-- Rotation Result -->
      @if (rotationResult()) {
        <div [class]="'alert ' + (rotationResult()!.success ? 'alert-success' : 'alert-danger')">
          <strong>{{ rotationResult()!.success ? '✅' : '❌' }}</strong>
          DEK "{{ rotationResult()!.dekPurpose }}" →
          {{
            rotationResult()!.success ? 'v' + rotationResult()!.newVersion : rotationResult()!.error
          }}
        </div>
      }

      <!-- Re-Encryption Section -->
      <div class="section-header" style="margin-top: 2rem;">
        <h3>🔄 Re-Encryption</h3>
      </div>

      <div class="re-encrypt-form">
        <div class="form-row">
          <div class="form-group">
            <label>DEK Purpose</label>
            <select [(ngModel)]="reEncryptPurpose">
              <option value="data">data</option>
              <option value="session">session</option>
              <option value="api">api</option>
              <option value="backup">backup</option>
            </select>
          </div>
          <div class="form-group">
            <label>Bảng</label>
            <select [(ngModel)]="reEncryptTable">
              @for (cfg of encConfigs(); track cfg.id) {
                <option [value]="cfg.tableName">{{ cfg.tableName }}</option>
              }
            </select>
          </div>
          <div class="form-group">
            <button class="btn btn-primary" (click)="reEncrypt()" [disabled]="loading()">
              🔄 Re-encrypt
            </button>
          </div>
        </div>
      </div>

      <!-- Re-Encryption Progress -->
      @if (progress().length > 0) {
        <table class="data-table" style="margin-top: 1rem;">
          <thead>
            <tr>
              <th>Bảng</th>
              <th>DEK Purpose</th>
              <th>Tiến trình</th>
              <th>Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            @for (p of progress(); track p.tableName) {
              <tr>
                <td>{{ p.tableName }}</td>
                <td>{{ p.dekPurpose }}</td>
                <td>
                  <div class="progress-bar">
                    <div
                      class="progress-fill"
                      [style.width.%]="p.totalRows > 0 ? (p.processedRows / p.totalRows) * 100 : 0"
                    ></div>
                    <span class="progress-text">{{ p.processedRows }}/{{ p.totalRows }}</span>
                  </div>
                </td>
                <td>
                  <span [class]="p.isComplete ? 'badge badge-success' : 'badge badge-warning'">
                    {{ p.isComplete ? 'Hoàn tất' : 'Đang xử lý' }}
                  </span>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (statusMsg()) {
        <div [class]="'status-msg status-' + statusMsg()!.type">{{ statusMsg()!.text }}</div>
      }
    </div>
  `,
})
export class DekRotationTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  dekVersions = signal<DekVersionInfo[]>([]);
  encConfigs = signal<EncryptionConfigResponse[]>([]);
  progress = signal<ReEncryptionProgress[]>([]);
  rotationResult = signal<DekRotationResult | null>(null);
  loading = signal(false);
  statusMsg = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  reEncryptPurpose = 'data';
  reEncryptTable = '';

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.kv.getDekStatus().subscribe({
      next: (d) => this.dekVersions.set(d),
      error: () => this.showStatus('Không thể tải DEK status', 'error'),
    });
    this.kv.getEncryptionConfigs().subscribe({
      next: (c) => {
        this.encConfigs.set(c);
        if (c.length > 0 && !this.reEncryptTable) this.reEncryptTable = c[0].tableName;
      },
    });
    this.kv.getReEncryptionProgress().subscribe({
      next: (p) => this.progress.set(p),
    });
  }

  rotateDek(purpose: string) {
    this.loading.set(true);
    this.kv.rotateDek(purpose).subscribe({
      next: (r) => {
        this.rotationResult.set(r);
        this.showStatus(
          r.success ? `DEK "${purpose}" rotated → v${r.newVersion}` : `Lỗi: ${r.error}`,
          r.success ? 'success' : 'error',
        );
        this.loadData();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi rotate DEK', 'error');
        this.loading.set(false);
      },
    });
  }

  reEncrypt() {
    if (!this.reEncryptTable) return;
    this.loading.set(true);
    this.kv.reEncryptTable(this.reEncryptPurpose, this.reEncryptTable).subscribe({
      next: (r) => {
        this.showStatus(
          `Re-encrypt ${r.tableName}: ${r.reEncrypted}/${r.totalRows} rows`,
          'success',
        );
        this.loadData();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi re-encrypt', 'error');
        this.loading.set(false);
      },
    });
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMsg.set({ text, type });
    setTimeout(() => this.statusMsg.set(null), 4000);
  }
}
