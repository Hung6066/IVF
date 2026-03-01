import { Component, OnInit, signal, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import {
  DrReadinessStatus,
  VaultBackupResponse,
  VaultRestoreResult,
} from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-vault-dr-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>🛡️ Vault Disaster Recovery</h3>
      </div>

      <!-- DR Readiness -->
      @if (readiness()) {
        <div class="readiness-card">
          <div class="readiness-header">
            <h4>DR Readiness</h4>
            <span
              class="grade-badge"
              [class]="'grade-' + readiness()!.readinessGrade.toLowerCase()"
            >
              {{ readiness()!.readinessGrade }}
            </span>
          </div>
          <div class="readiness-checks">
            <div class="check-item">
              <span>{{ readiness()!.autoUnsealConfigured ? '✅' : '❌' }}</span>
              <span>Auto-Unseal configured</span>
            </div>
            <div class="check-item">
              <span>{{ readiness()!.encryptionActive ? '✅' : '❌' }}</span>
              <span>Encryption active</span>
            </div>
            <div class="check-item">
              <span>{{ readiness()!.activeSecrets > 0 ? '✅' : '⚠️' }}</span>
              <span>{{ readiness()!.activeSecrets }} active secrets</span>
            </div>
            <div class="check-item">
              <span>{{ readiness()!.activePolicies > 0 ? '✅' : '⚠️' }}</span>
              <span>{{ readiness()!.activePolicies }} active policies</span>
            </div>
            <div class="check-item">
              <span>{{ readiness()!.lastBackupAt ? '✅' : '❌' }}</span>
              <span
                >Last backup:
                {{
                  readiness()!.lastBackupAt
                    ? (readiness()!.lastBackupAt | date: 'dd/MM/yy HH:mm')
                    : 'Chưa bao giờ'
                }}</span
              >
            </div>
          </div>
        </div>
      }

      <!-- Backup Section -->
      <div class="section-header" style="margin-top: 2rem;">
        <h3>💾 Backup</h3>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Backup Key (mã hóa)</label>
          <input type="password" [(ngModel)]="backupKey" placeholder="Nhập key mã hóa backup..." />
        </div>
        <div class="form-group" style="align-self: flex-end;">
          <button
            class="btn btn-primary"
            (click)="createBackup()"
            [disabled]="loading() || !backupKey"
          >
            💾 Tạo Backup
          </button>
        </div>
      </div>

      @if (lastBackup()) {
        <div class="alert alert-success">
          <strong>✅ Backup thành công</strong>
          <div class="backup-info">
            <span>ID: {{ lastBackup()!.backupId }}</span>
            <span>Secrets: {{ lastBackup()!.secretsCount }}</span>
            <span>Policies: {{ lastBackup()!.policiesCount }}</span>
            <span>Settings: {{ lastBackup()!.settingsCount }}</span>
            <span>Hash: {{ lastBackup()!.integrityHash.substring(0, 16) }}...</span>
          </div>
          <button class="btn btn-sm btn-info" (click)="downloadBackup()">⬇️ Tải xuống</button>
        </div>
      }

      <!-- Restore Section -->
      <div class="section-header" style="margin-top: 2rem;">
        <h3>♻️ Restore</h3>
      </div>
      <div class="form-group">
        <label>File backup (.json base64)</label>
        <textarea
          [(ngModel)]="restoreData"
          rows="4"
          placeholder="Dán backup data base64 vào đây..."
        ></textarea>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Backup Key</label>
          <input type="password" [(ngModel)]="restoreKey" placeholder="Nhập key giải mã..." />
        </div>
        <div class="form-group" style="align-self: flex-end;">
          <button
            class="btn btn-warning"
            (click)="validateBackup()"
            [disabled]="loading() || !restoreData || !restoreKey"
          >
            🔍 Kiểm tra
          </button>
          <button
            class="btn btn-danger"
            (click)="restoreBackup()"
            [disabled]="loading() || !restoreData || !restoreKey"
          >
            ♻️ Restore
          </button>
        </div>
      </div>

      @if (restoreResult()) {
        <div [class]="'alert ' + (restoreResult()!.success ? 'alert-success' : 'alert-danger')">
          <strong>{{
            restoreResult()!.success ? '✅ Restore thành công' : '❌ Restore thất bại'
          }}</strong>
          @if (restoreResult()!.success) {
            <div>
              Secrets: {{ restoreResult()!.secretsRestored }}, Policies:
              {{ restoreResult()!.policiesRestored }}, Settings:
              {{ restoreResult()!.settingsRestored }}, Configs:
              {{ restoreResult()!.encryptionConfigsRestored }}
            </div>
          }
          @if (restoreResult()!.error) {
            <div>{{ restoreResult()!.error }}</div>
          }
        </div>
      }

      @if (statusMsg()) {
        <div [class]="'status-msg status-' + statusMsg()!.type">{{ statusMsg()!.text }}</div>
      }
    </div>
  `,
})
export class VaultDrTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  readiness = signal<DrReadinessStatus | null>(null);
  lastBackup = signal<VaultBackupResponse | null>(null);
  restoreResult = signal<VaultRestoreResult | null>(null);
  loading = signal(false);
  statusMsg = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  backupKey = '';
  restoreData = '';
  restoreKey = '';

  ngOnInit() {
    this.loadReadiness();
  }

  loadReadiness() {
    this.kv.getDrReadiness().subscribe({
      next: (r) => this.readiness.set(r),
      error: () => {},
    });
  }

  createBackup() {
    this.loading.set(true);
    this.kv.createVaultBackup(this.backupKey).subscribe({
      next: (r) => {
        this.lastBackup.set(r);
        this.showStatus('Backup thành công', 'success');
        this.loadReadiness();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo backup', 'error');
        this.loading.set(false);
      },
    });
  }

  downloadBackup() {
    const backup = this.lastBackup();
    if (!backup) return;
    const blob = new Blob([backup.backupDataBase64], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `vault-backup-${backup.backupId}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  }

  validateBackup() {
    this.loading.set(true);
    this.kv.validateVaultBackup(this.restoreData, this.restoreKey).subscribe({
      next: (r) => {
        this.showStatus(
          r.valid
            ? `✅ Backup hợp lệ (ID: ${r.backupId}, Hash: ${r.integrityHash.substring(0, 16)}...)`
            : `❌ Backup không hợp lệ: ${r.error}`,
          r.valid ? 'success' : 'error',
        );
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi kiểm tra backup', 'error');
        this.loading.set(false);
      },
    });
  }

  restoreBackup() {
    this.loading.set(true);
    this.kv.restoreVaultBackup(this.restoreData, this.restoreKey).subscribe({
      next: (r) => {
        this.restoreResult.set(r);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi restore backup', 'error');
        this.loading.set(false);
      },
    });
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMsg.set({ text, type });
    setTimeout(() => this.statusMsg.set(null), 4000);
  }
}
