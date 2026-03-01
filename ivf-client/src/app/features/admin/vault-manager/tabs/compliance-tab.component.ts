import { Component, OnInit, signal, inject, computed, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KeyVaultService } from '../../../../core/services/keyvault.service';
import {
  ComplianceReport,
  FrameworkScore,
  ControlResult,
} from '../../../../core/models/keyvault.model';

@Component({
  selector: 'app-compliance-tab',
  standalone: true,
  imports: [CommonModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>📊 Compliance Scoring Engine</h3>
        <button class="btn btn-primary" (click)="loadReport()" [disabled]="loading()">
          🔄 Đánh giá lại
        </button>
      </div>

      @if (report()) {
        <!-- Overall Score -->
        <div class="score-overview">
          <div class="score-circle" [class]="'grade-' + report()!.grade.replace('+', 'plus')">
            <span class="grade">{{ report()!.grade }}</span>
            <span class="percentage">{{ report()!.percentage | number: '1.0-0' }}%</span>
          </div>
          <div class="score-details">
            <h4>Tổng điểm: {{ report()!.overallScore }}/{{ report()!.maxScore }}</h4>
            <p>Đánh giá lúc: {{ report()!.evaluatedAt | date: 'dd/MM/yy HH:mm:ss' }}</p>
          </div>
        </div>

        <!-- Framework Cards -->
        <div class="card-grid three-col">
          @for (fw of report()!.frameworks; track fw.framework) {
            <div class="card" [class]="'framework-card framework-' + fw.framework.toLowerCase()">
              <div class="card-header">
                <h4>{{ getFrameworkIcon(fw.framework) }} {{ fw.name }}</h4>
                <span class="badge" [class]="getScoreBadgeClass(fw.percentage)">
                  {{ fw.percentage | number: '1.0-0' }}%
                </span>
              </div>
              <div class="card-body">
                <div class="progress-bar">
                  <div
                    class="progress-fill"
                    [class]="getProgressClass(fw.percentage)"
                    [style.width.%]="fw.percentage"
                  ></div>
                </div>
                <p class="score-text">{{ fw.score }}/{{ fw.maxScore }} điểm</p>

                <!-- Controls -->
                <div class="controls-list">
                  @for (ctrl of fw.controls; track ctrl.controlId) {
                    <div class="control-item" [class]="'control-' + ctrl.status.toLowerCase()">
                      <span class="control-status">{{ getStatusIcon(ctrl.status) }}</span>
                      <div class="control-info">
                        <span class="control-name">{{ ctrl.name }}</span>
                        <span class="control-score">{{ ctrl.score }}/{{ ctrl.maxScore }}</span>
                      </div>
                      @if (ctrl.finding) {
                        <span class="control-finding">{{ ctrl.finding }}</span>
                      }
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        </div>

        <!-- Remediation Summary -->
        @if (failedControls().length > 0) {
          <div class="remediation-section">
            <h3>⚠️ Cần khắc phục ({{ failedControls().length }} controls)</h3>
            <div class="remediation-list">
              @for (ctrl of failedControls(); track ctrl.controlId) {
                <div class="remediation-item">
                  <span class="control-id">{{ ctrl.controlId }}</span>
                  <span class="control-name">{{ ctrl.name }}</span>
                  <span class="control-desc">{{ ctrl.description }}</span>
                  @if (ctrl.finding) {
                    <span class="control-finding">💡 {{ ctrl.finding }}</span>
                  }
                </div>
              }
            </div>
          </div>
        }
      }

      @if (!report() && !loading()) {
        <div class="empty-state">
          <p>Nhấn "Đánh giá lại" để chạy compliance scoring engine</p>
        </div>
      }
    </div>
  `,
})
export class ComplianceTabComponent implements OnInit {
  private kv = inject(KeyVaultService);

  report = signal<ComplianceReport | null>(null);
  loading = signal(false);

  failedControls = computed(() => {
    const r = this.report();
    if (!r) return [];
    return r.frameworks.flatMap((fw) => fw.controls.filter((c) => c.status === 'Fail'));
  });

  ngOnInit() {
    this.loadReport();
  }

  loadReport() {
    this.loading.set(true);
    this.kv.getComplianceReport().subscribe({
      next: (r) => {
        this.report.set(r);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  getFrameworkIcon(framework: string): string {
    const icons: Record<string, string> = { Hipaa: '🏥', Soc2: '🔒', Gdpr: '🇪🇺' };
    return icons[framework] || '📋';
  }

  getStatusIcon(status: string): string {
    const icons: Record<string, string> = {
      Pass: '✅',
      Fail: '❌',
      Partial: '⚠️',
      NotApplicable: '➖',
    };
    return icons[status] || '❓';
  }

  getScoreBadgeClass(pct: number): string {
    if (pct >= 90) return 'badge badge-success';
    if (pct >= 70) return 'badge badge-warning';
    return 'badge badge-danger';
  }

  getProgressClass(pct: number): string {
    if (pct >= 90) return 'progress-success';
    if (pct >= 70) return 'progress-warning';
    return 'progress-danger';
  }
}
