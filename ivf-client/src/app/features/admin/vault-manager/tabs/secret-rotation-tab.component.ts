import { Component, OnInit, signal, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import {
  RotationSchedule,
  RotationHistoryEntry,
  RotationScheduleCreateRequest,
} from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-secret-rotation-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>🔄 Quản lý Rotation Schedule</h3>
        <button class="btn btn-primary" (click)="showCreateDialog.set(true)">+ Tạo Schedule</button>
      </div>

      <!-- Schedules Table -->
      <div class="table-container">
        <table class="data-table">
          <thead>
            <tr>
              <th>Secret Path</th>
              <th>Interval (ngày)</th>
              <th>Grace (giờ)</th>
              <th>Auto</th>
              <th>Strategy</th>
              <th>Lần cuối</th>
              <th>Tiếp theo</th>
              <th>Trạng thái</th>
              <th>Hành động</th>
            </tr>
          </thead>
          <tbody>
            @for (s of schedules(); track s.secretPath) {
              <tr [class.overdue]="isOverdue(s)">
                <td class="mono">{{ s.secretPath }}</td>
                <td>{{ s.rotationIntervalDays }}</td>
                <td>{{ s.gracePeriodHours }}</td>
                <td>
                  <span
                    [class]="s.automaticallyRotate ? 'badge badge-success' : 'badge badge-muted'"
                  >
                    {{ s.automaticallyRotate ? 'Bật' : 'Tắt' }}
                  </span>
                </td>
                <td>{{ s.rotationStrategy || 'generate' }}</td>
                <td>{{ s.lastRotatedAt ? (s.lastRotatedAt | date: 'dd/MM/yy HH:mm') : 'Chưa' }}</td>
                <td>{{ s.nextRotationAt | date: 'dd/MM/yy HH:mm' }}</td>
                <td>
                  <span [class]="isOverdue(s) ? 'badge badge-danger' : 'badge badge-success'">
                    {{ isOverdue(s) ? 'Quá hạn' : 'OK' }}
                  </span>
                </td>
                <td class="actions">
                  <button
                    class="btn btn-sm btn-warning"
                    (click)="rotateNow(s.secretPath)"
                    [disabled]="loading()"
                  >
                    🔄 Rotate
                  </button>
                  <button class="btn btn-sm btn-info" (click)="viewHistory(s.secretPath)">
                    📜
                  </button>
                  <button class="btn btn-sm btn-danger" (click)="deleteSchedule(s.secretPath)">
                    🗑️
                  </button>
                </td>
              </tr>
            }
            @if (schedules().length === 0) {
              <tr>
                <td colspan="9" class="empty">Chưa có rotation schedule</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <!-- History Dialog -->
      @if (showHistoryDialog()) {
        <div class="dialog-overlay" (click)="showHistoryDialog.set(false)">
          <div class="dialog" (click)="$event.stopPropagation()">
            <div class="dialog-header">
              <h3>📜 Lịch sử Rotation: {{ historyPath() }}</h3>
              <button class="btn-close" (click)="showHistoryDialog.set(false)">✕</button>
            </div>
            <div class="dialog-body">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Thời gian</th>
                    <th>Version cũ</th>
                    <th>Version mới</th>
                    <th>Kết quả</th>
                    <th>Lỗi</th>
                  </tr>
                </thead>
                <tbody>
                  @for (h of history(); track h.rotatedAt) {
                    <tr>
                      <td>{{ h.rotatedAt | date: 'dd/MM/yy HH:mm:ss' }}</td>
                      <td>v{{ h.oldVersion }}</td>
                      <td>v{{ h.newVersion }}</td>
                      <td>
                        <span [class]="h.success ? 'badge badge-success' : 'badge badge-danger'">
                          {{ h.success ? 'Thành công' : 'Thất bại' }}
                        </span>
                      </td>
                      <td>{{ h.error || '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Create Dialog -->
      @if (showCreateDialog()) {
        <div class="dialog-overlay" (click)="showCreateDialog.set(false)">
          <div class="dialog" (click)="$event.stopPropagation()">
            <div class="dialog-header">
              <h3>➕ Tạo Rotation Schedule</h3>
              <button class="btn-close" (click)="showCreateDialog.set(false)">✕</button>
            </div>
            <div class="dialog-body">
              <div class="form-group">
                <label>Secret Path</label>
                <input
                  type="text"
                  [(ngModel)]="createForm.secretPath"
                  placeholder="database/password"
                />
              </div>
              <div class="form-row">
                <div class="form-group">
                  <label>Interval (ngày)</label>
                  <input type="number" [(ngModel)]="createForm.rotationIntervalDays" min="1" />
                </div>
                <div class="form-group">
                  <label>Grace Period (giờ)</label>
                  <input type="number" [(ngModel)]="createForm.gracePeriodHours" min="1" />
                </div>
              </div>
              <div class="form-group">
                <label>
                  <input type="checkbox" [(ngModel)]="createForm.automaticallyRotate" />
                  Tự động rotate
                </label>
              </div>
              <div class="form-group">
                <label>Strategy</label>
                <select [(ngModel)]="createForm.rotationStrategy">
                  <option value="generate">Generate</option>
                  <option value="callback">Callback</option>
                </select>
              </div>
              <div class="form-actions">
                <button class="btn btn-primary" (click)="createSchedule()" [disabled]="loading()">
                  Tạo
                </button>
                <button class="btn btn-secondary" (click)="showCreateDialog.set(false)">Hủy</button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Status Message -->
      @if (statusMsg()) {
        <div [class]="'status-msg status-' + statusMsg()!.type">{{ statusMsg()!.text }}</div>
      }
    </div>
  `,
})
export class SecretRotationTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  schedules = signal<RotationSchedule[]>([]);
  history = signal<RotationHistoryEntry[]>([]);
  historyPath = signal('');
  loading = signal(false);
  showCreateDialog = signal(false);
  showHistoryDialog = signal(false);
  statusMsg = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  createForm: RotationScheduleCreateRequest = {
    secretPath: '',
    rotationIntervalDays: 30,
    gracePeriodHours: 24,
    automaticallyRotate: true,
    rotationStrategy: 'generate',
  };

  ngOnInit() {
    this.loadSchedules();
  }

  loadSchedules() {
    this.kv.getRotationSchedules().subscribe({
      next: (s) => this.schedules.set(s),
      error: () => this.showStatus('Không thể tải rotation schedules', 'error'),
    });
  }

  createSchedule() {
    this.loading.set(true);
    this.kv.createRotationSchedule(this.createForm).subscribe({
      next: () => {
        this.showStatus('Tạo rotation schedule thành công', 'success');
        this.showCreateDialog.set(false);
        this.loadSchedules();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi tạo rotation schedule', 'error');
        this.loading.set(false);
      },
    });
  }

  deleteSchedule(secretPath: string) {
    this.kv.deleteRotationSchedule(secretPath).subscribe({
      next: () => {
        this.showStatus('Đã xóa schedule', 'success');
        this.loadSchedules();
      },
      error: () => this.showStatus('Lỗi xóa schedule', 'error'),
    });
  }

  rotateNow(secretPath: string) {
    this.loading.set(true);
    this.kv.rotateNow(secretPath).subscribe({
      next: (r) => {
        this.showStatus(
          r.success ? `Rotate thành công → v${r.newVersion}` : `Lỗi: ${r.error}`,
          r.success ? 'success' : 'error',
        );
        this.loadSchedules();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi rotate secret', 'error');
        this.loading.set(false);
      },
    });
  }

  viewHistory(secretPath: string) {
    this.historyPath.set(secretPath);
    this.kv.getRotationHistory(secretPath).subscribe({
      next: (h) => {
        this.history.set(h);
        this.showHistoryDialog.set(true);
      },
      error: () => this.showStatus('Không thể tải lịch sử', 'error'),
    });
  }

  isOverdue(s: RotationSchedule): boolean {
    return new Date(s.nextRotationAt) < new Date();
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMsg.set({ text, type });
    setTimeout(() => this.statusMsg.set(null), 4000);
  }
}
