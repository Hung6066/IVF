import { Component, Input, OnInit, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  FormsService,
  FormTemplate,
  FormResponse,
  ResponseStatus,
} from '../../../forms/forms.service';
import { CoupleService } from '../../../../core/services/couple.service';

interface FormPhaseGroup {
  phase: string;
  phaseName: string;
  phaseIcon: string;
  forms: CycleFormItem[];
}

interface CycleFormItem {
  template: FormTemplate;
  responses: FormResponse[];
  latestResponse?: FormResponse;
  status: 'completed' | 'in-progress' | 'pending';
  statusLabel: string;
}

@Component({
  selector: 'app-forms-tab',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="forms-tab">
      <!-- Quick Stats -->
      <div class="forms-stats">
        <div class="stat-card">
          <span class="stat-icon">📋</span>
          <div class="stat-info">
            <span class="stat-value">{{ totalForms() }}</span>
            <span class="stat-label">Tổng biểu mẫu</span>
          </div>
        </div>
        <div class="stat-card completed">
          <span class="stat-icon">✅</span>
          <div class="stat-info">
            <span class="stat-value">{{ completedForms() }}</span>
            <span class="stat-label">Đã hoàn thành</span>
          </div>
        </div>
        <div class="stat-card in-progress">
          <span class="stat-icon">✏️</span>
          <div class="stat-info">
            <span class="stat-value">{{ inProgressForms() }}</span>
            <span class="stat-label">Đang điền</span>
          </div>
        </div>
        <div class="stat-card pending">
          <span class="stat-icon">⏳</span>
          <div class="stat-info">
            <span class="stat-value">{{ pendingForms() }}</span>
            <span class="stat-label">Chờ điền</span>
          </div>
        </div>
      </div>

      <!-- Progress Bar -->
      <div class="forms-progress">
        <div class="progress-header">
          <span>Tiến độ hoàn thành</span>
          <span class="progress-pct">{{ progressPct() }}%</span>
        </div>
        <div class="progress-bar">
          <div class="progress-fill" [style.width.%]="progressPct()"></div>
        </div>
      </div>

      <!-- Form Groups by Phase -->
      @for (group of formGroups(); track group.phase) {
        <div class="phase-group">
          <div class="phase-group-header">
            <span class="phase-icon">{{ group.phaseIcon }}</span>
            <h3>{{ group.phaseName }}</h3>
            <span class="phase-count">{{ group.forms.length }} biểu mẫu</span>
          </div>
          <div class="form-cards-grid">
            @for (item of group.forms; track item.template.id) {
              <div class="form-card" [class]="item.status">
                <div class="form-card-header">
                  <div class="form-status-badge" [class]="item.status">
                    @switch (item.status) {
                      @case ('completed') {
                        ✅
                      }
                      @case ('in-progress') {
                        ✏️
                      }
                      @case ('pending') {
                        ⏳
                      }
                    }
                    {{ item.statusLabel }}
                  </div>
                  @if (item.responses.length > 1) {
                    <span class="response-count">{{ item.responses.length }} lần</span>
                  }
                </div>
                <h4 class="form-card-title">{{ item.template.name }}</h4>
                @if (item.template.description) {
                  <p class="form-card-desc">{{ item.template.description }}</p>
                }
                @if (item.latestResponse) {
                  <div class="form-card-meta">
                    <span class="meta-date"
                      >📅
                      {{
                        formatDate(item.latestResponse.submittedAt || item.latestResponse.createdAt)
                      }}</span
                    >
                  </div>
                }
                <div class="form-card-actions">
                  @if (item.status === 'completed') {
                    <button class="btn btn-view" (click)="viewResponse(item)">👁️ Xem</button>
                    <button
                      class="btn btn-pdf"
                      (click)="previewPdf(item)"
                      [disabled]="exportingId() === item.latestResponse?.id"
                    >
                      @if (exportingId() === item.latestResponse?.id) {
                        <span class="btn-spinner"></span>
                      } @else {
                        📄
                      }
                      PDF
                    </button>
                    <button class="btn btn-edit" (click)="editResponse(item)">✏️ Sửa</button>
                  } @else if (item.status === 'in-progress') {
                    <button class="btn btn-continue" (click)="continueForm(item)">
                      ▶️ Tiếp tục
                    </button>
                  } @else {
                    <button class="btn btn-fill" (click)="fillForm(item)">📝 Điền biểu mẫu</button>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      }

      @if (formGroups().length === 0 && !loading()) {
        <div class="empty-state">
          <span class="empty-icon">📋</span>
          <h3>Chưa có biểu mẫu nào</h3>
          <p>Chưa có biểu mẫu nào được gán cho chu kỳ điều trị này.</p>
        </div>
      }

      @if (loading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Đang tải biểu mẫu...</p>
        </div>
      }

      <!-- PDF Preview Modal -->
      @if (pdfPreviewUrl()) {
        <div class="pdf-overlay" (click)="closePdfPreview()">
          <div class="pdf-modal" (click)="$event.stopPropagation()">
            <div class="pdf-modal-header">
              <h3>📄 {{ pdfPreviewTitle() }}</h3>
              <div class="pdf-modal-actions">
                <button class="btn btn-pdf-download" (click)="downloadCurrentPdf()">
                  ⬇️ Tải xuống
                </button>
                <button class="btn btn-pdf-close" (click)="closePdfPreview()">✕</button>
              </div>
            </div>
            <div class="pdf-modal-body">
              <iframe [src]="pdfPreviewUrl()" class="pdf-iframe"></iframe>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .forms-tab {
        padding: 4px 0;
      }

      // ===== Stats =====
      .forms-stats {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 16px;
        margin-bottom: 24px;
      }

      .stat-card {
        display: flex;
        align-items: center;
        gap: 14px;
        padding: 18px 20px;
        background: white;
        border: 1px solid #e2e8f0;
        border-radius: 12px;
        transition: all 0.2s;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);

        &:hover {
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
          transform: translateY(-2px);
        }

        &.completed {
          border-left: 4px solid #22c55e;
        }
        &.in-progress {
          border-left: 4px solid #f59e0b;
        }
        &.pending {
          border-left: 4px solid #94a3b8;
        }

        .stat-icon {
          font-size: 28px;
        }
        .stat-info {
          display: flex;
          flex-direction: column;
        }
        .stat-value {
          font-size: 24px;
          font-weight: 700;
          color: #1e293b;
          line-height: 1;
        }
        .stat-label {
          font-size: 13px;
          color: #64748b;
          margin-top: 4px;
        }
      }

      // ===== Progress =====
      .forms-progress {
        margin-bottom: 28px;
        padding: 18px 22px;
        background: white;
        border-radius: 12px;
        border: 1px solid #e2e8f0;
      }
      .progress-header {
        display: flex;
        justify-content: space-between;
        margin-bottom: 10px;
        font-size: 14px;
        font-weight: 600;
        color: #334155;
      }
      .progress-pct {
        color: #667eea;
        font-weight: 700;
      }
      .progress-bar {
        height: 10px;
        background: #e2e8f0;
        border-radius: 5px;
        overflow: hidden;
      }
      .progress-fill {
        height: 100%;
        background: linear-gradient(90deg, #667eea, #22c55e);
        border-radius: 5px;
        transition: width 0.5s ease;
      }

      // ===== Phase Groups =====
      .phase-group {
        margin-bottom: 28px;
      }
      .phase-group-header {
        display: flex;
        align-items: center;
        gap: 10px;
        margin-bottom: 16px;
        padding-bottom: 10px;
        border-bottom: 2px solid #e2e8f0;

        .phase-icon {
          font-size: 22px;
        }
        h3 {
          margin: 0;
          font-size: 17px;
          font-weight: 700;
          color: #1e293b;
        }
        .phase-count {
          margin-left: auto;
          font-size: 13px;
          color: #64748b;
          background: #f1f5f9;
          padding: 4px 12px;
          border-radius: 20px;
        }
      }

      // ===== Form Cards =====
      .form-cards-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
        gap: 16px;
      }

      .form-card {
        padding: 20px;
        background: white;
        border: 1px solid #e2e8f0;
        border-radius: 12px;
        transition: all 0.2s;
        display: flex;
        flex-direction: column;
        gap: 10px;

        &:hover {
          box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
          transform: translateY(-2px);
        }

        &.completed {
          border-left: 4px solid #22c55e;
        }
        &.in-progress {
          border-left: 4px solid #f59e0b;
        }
        &.pending {
          border-left: 4px solid #cbd5e1;
          opacity: 0.85;
        }
      }

      .form-card-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }

      .form-status-badge {
        display: inline-flex;
        align-items: center;
        gap: 5px;
        padding: 4px 12px;
        border-radius: 20px;
        font-size: 12px;
        font-weight: 600;

        &.completed {
          background: #dcfce7;
          color: #166534;
        }
        &.in-progress {
          background: #fef3c7;
          color: #92400e;
        }
        &.pending {
          background: #f1f5f9;
          color: #64748b;
        }
      }

      .response-count {
        font-size: 12px;
        color: #667eea;
        background: #eff3ff;
        padding: 2px 10px;
        border-radius: 12px;
        font-weight: 600;
      }

      .form-card-title {
        margin: 0;
        font-size: 16px;
        font-weight: 600;
        color: #1e293b;
        line-height: 1.4;
      }

      .form-card-desc {
        margin: 0;
        font-size: 13px;
        color: #64748b;
        line-height: 1.5;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
        overflow: hidden;
      }

      .form-card-meta {
        font-size: 12px;
        color: #94a3b8;
        .meta-date {
          display: inline-flex;
          align-items: center;
          gap: 4px;
        }
      }

      .form-card-actions {
        display: flex;
        gap: 8px;
        margin-top: auto;
        padding-top: 12px;
        border-top: 1px solid #f1f5f9;
      }

      .btn {
        padding: 8px 16px;
        border: none;
        border-radius: 8px;
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.2s;
        display: inline-flex;
        align-items: center;
        gap: 4px;
      }

      .btn-fill {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
        flex: 1;
        justify-content: center;
        &:hover {
          opacity: 0.9;
          transform: translateY(-1px);
        }
      }

      .btn-continue {
        background: linear-gradient(135deg, #f59e0b 0%, #f97316 100%);
        color: white;
        flex: 1;
        justify-content: center;
        &:hover {
          opacity: 0.9;
        }
      }

      .btn-view {
        background: #f0f4ff;
        color: #667eea;
        flex: 1;
        justify-content: center;
        &:hover {
          background: #e0e7ff;
        }
      }

      .btn-pdf {
        background: #fff7ed;
        color: #c2410c;
        display: inline-flex;
        align-items: center;
        gap: 4px;
        &:hover {
          background: #ffedd5;
        }
        &:disabled {
          opacity: 0.6;
          cursor: wait;
        }
      }

      .btn-spinner {
        width: 14px;
        height: 14px;
        border: 2px solid #fed7aa;
        border-top-color: #c2410c;
        border-radius: 50%;
        animation: spin 0.7s linear infinite;
        display: inline-block;
      }

      .btn-edit {
        background: #f1f5f9;
        color: #475569;
        &:hover {
          background: #e2e8f0;
        }
      }

      // ===== PDF Preview Modal =====
      .pdf-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.6);
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        backdrop-filter: blur(4px);
      }

      .pdf-modal {
        background: white;
        border-radius: 16px;
        width: 90vw;
        max-width: 900px;
        height: 85vh;
        display: flex;
        flex-direction: column;
        box-shadow: 0 25px 60px rgba(0, 0, 0, 0.3);
        overflow: hidden;
      }

      .pdf-modal-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 16px 24px;
        border-bottom: 1px solid #e2e8f0;
        background: #f8fafc;

        h3 {
          margin: 0;
          font-size: 16px;
          color: #1e293b;
          font-weight: 600;
        }
      }

      .pdf-modal-actions {
        display: flex;
        gap: 8px;
      }

      .btn-pdf-download {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
        padding: 8px 16px;
        border: none;
        border-radius: 8px;
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        &:hover {
          opacity: 0.9;
        }
      }

      .btn-pdf-close {
        background: #f1f5f9;
        color: #475569;
        padding: 8px 14px;
        border: none;
        border-radius: 8px;
        font-size: 16px;
        font-weight: 700;
        cursor: pointer;
        &:hover {
          background: #e2e8f0;
        }
      }

      .pdf-modal-body {
        flex: 1;
        overflow: hidden;
      }

      .pdf-iframe {
        width: 100%;
        height: 100%;
        border: none;
      }

      // ===== Empty & Loading =====
      .empty-state {
        text-align: center;
        padding: 60px 20px;
        color: #64748b;
        .empty-icon {
          font-size: 48px;
          display: block;
          margin-bottom: 12px;
        }
        h3 {
          margin: 0 0 8px;
          color: #334155;
        }
        p {
          margin: 0;
          font-size: 14px;
        }
      }

      .loading-state {
        text-align: center;
        padding: 40px;
        color: #64748b;
      }

      .spinner {
        width: 36px;
        height: 36px;
        border: 3px solid #e2e8f0;
        border-top-color: #667eea;
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
        margin: 0 auto 12px;
      }

      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }

      // ===== Responsive =====
      @media (max-width: 1024px) {
        .forms-stats {
          grid-template-columns: repeat(2, 1fr);
        }
      }

      @media (max-width: 640px) {
        .forms-stats {
          grid-template-columns: 1fr;
        }
        .form-cards-grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class FormsTabComponent implements OnInit, OnChanges {
  @Input() cycleId = '';
  @Input() coupleId = '';

  private formsService = inject(FormsService);
  private coupleService = inject(CoupleService);
  private router = inject(Router);
  private sanitizer = inject(DomSanitizer);

  loading = signal(true);
  formGroups = signal<FormPhaseGroup[]>([]);
  totalForms = signal(0);
  completedForms = signal(0);
  inProgressForms = signal(0);
  pendingForms = signal(0);
  progressPct = signal(0);
  exportingId = signal<string | null>(null);
  pdfPreviewUrl = signal<SafeResourceUrl | null>(null);
  pdfPreviewTitle = signal('');
  private currentPdfBlob: Blob | null = null;
  private currentPdfFileName = '';
  private resolvedPatientId = '';

  // Map treatment phases to form categories / templates
  private phaseFormMap: {
    phase: string;
    phaseName: string;
    phaseIcon: string;
    keywords: string[];
  }[] = [
    {
      phase: 'consultation',
      phaseName: 'Tư vấn & Đồng ý',
      phaseIcon: '📋',
      keywords: ['đồng ý', 'consent', 'khám ban đầu', 'tư vấn'],
    },
    {
      phase: 'examination',
      phaseName: 'Xét nghiệm',
      phaseIcon: '🔬',
      keywords: ['xét nghiệm', 'tinh dịch', 'máu', 'hormone'],
    },
    {
      phase: 'stimulation',
      phaseName: 'Kích thích buồng trứng',
      phaseIcon: '💉',
      keywords: ['siêu âm', 'nang noãn', 'kích thích', 'follicle'],
    },
    {
      phase: 'retrieval',
      phaseName: 'Chọc hút & Phôi',
      phaseIcon: '🧬',
      keywords: ['chọc hút', 'noãn', 'phôi', 'egg retrieval'],
    },
    {
      phase: 'transfer',
      phaseName: 'Chuyển phôi',
      phaseIcon: '🎯',
      keywords: ['chuyển phôi', 'transfer', 'embryo'],
    },
    { phase: 'other', phaseName: 'Biểu mẫu khác', phaseIcon: '📑', keywords: [] },
  ];

  ngOnInit(): void {
    this.loadForms();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['cycleId'] && !changes['cycleId'].firstChange) {
      this.loadForms();
    }
  }

  loadForms(): void {
    if (!this.cycleId && !this.coupleId) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);

    // Resolve couple to get wife's patient ID
    if (this.coupleId && !this.resolvedPatientId) {
      this.coupleService.getCouple(this.coupleId).subscribe({
        next: (couple) => {
          this.resolvedPatientId = couple.wife?.id || '';
          this.loadTemplatesAndResponses();
        },
        error: () => {
          this.loadTemplatesAndResponses();
        },
      });
    } else {
      this.loadTemplatesAndResponses();
    }
  }

  private loadTemplatesAndResponses(): void {
    // Load all published templates
    this.formsService.getTemplates(undefined, true).subscribe({
      next: (templates) => {
        const publishedTemplates = templates || [];

        // Load responses for this patient/cycle
        if (this.resolvedPatientId) {
          this.formsService
            .getResponses(undefined, this.resolvedPatientId, undefined, undefined, 1, 200)
            .subscribe({
              next: (responsesResult) => {
                const responses = responsesResult.items || [];
                this.buildFormGroups(publishedTemplates, responses);
                this.loading.set(false);
              },
              error: () => {
                this.buildFormGroups(publishedTemplates, []);
                this.loading.set(false);
              },
            });
        } else {
          this.buildFormGroups(publishedTemplates, []);
          this.loading.set(false);
        }
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private buildFormGroups(templates: FormTemplate[], responses: FormResponse[]): void {
    // Map responses to template ID
    const responsesByTemplate: { [templateId: string]: FormResponse[] } = {};
    for (const resp of responses) {
      if (!responsesByTemplate[resp.formTemplateId]) {
        responsesByTemplate[resp.formTemplateId] = [];
      }
      responsesByTemplate[resp.formTemplateId].push(resp);
    }

    // Build items
    const allItems: { item: CycleFormItem; phase: string }[] = [];

    for (const template of templates) {
      const templateResponses = responsesByTemplate[template.id] || [];
      const latestResponse =
        templateResponses.length > 0
          ? templateResponses.sort(
              (a, b) =>
                new Date(b.submittedAt || b.createdAt).getTime() -
                new Date(a.submittedAt || a.createdAt).getTime(),
            )[0]
          : undefined;

      let status: CycleFormItem['status'] = 'pending';
      let statusLabel = 'Chờ điền';

      if (latestResponse) {
        if (latestResponse.status === ResponseStatus.Submitted) {
          status = 'completed';
          statusLabel = 'Đã hoàn thành';
        } else {
          status = 'in-progress';
          statusLabel = 'Đang điền';
        }
      }

      const item: CycleFormItem = {
        template,
        responses: templateResponses,
        latestResponse,
        status,
        statusLabel,
      };

      // Determine which phase group this template belongs to
      const phase = this.matchPhase(template);
      allItems.push({ item, phase });
    }

    // Group by phase
    const groups: FormPhaseGroup[] = [];
    for (const phaseInfo of this.phaseFormMap) {
      const phaseForms = allItems.filter((a) => a.phase === phaseInfo.phase).map((a) => a.item);

      if (phaseForms.length > 0) {
        groups.push({
          phase: phaseInfo.phase,
          phaseName: phaseInfo.phaseName,
          phaseIcon: phaseInfo.phaseIcon,
          forms: phaseForms,
        });
      }
    }

    this.formGroups.set(groups);

    // Calculate stats
    const total = allItems.length;
    const completed = allItems.filter((a) => a.item.status === 'completed').length;
    const inProg = allItems.filter((a) => a.item.status === 'in-progress').length;
    const pending = allItems.filter((a) => a.item.status === 'pending').length;

    this.totalForms.set(total);
    this.completedForms.set(completed);
    this.inProgressForms.set(inProg);
    this.pendingForms.set(pending);
    this.progressPct.set(total > 0 ? Math.round((completed / total) * 100) : 0);
  }

  private matchPhase(template: FormTemplate): string {
    const name = (template.name || '').toLowerCase();
    const desc = (template.description || '').toLowerCase();
    const combined = name + ' ' + desc;

    for (const phaseInfo of this.phaseFormMap) {
      if (phaseInfo.keywords.length === 0) continue;
      for (const keyword of phaseInfo.keywords) {
        if (combined.includes(keyword.toLowerCase())) {
          return phaseInfo.phase;
        }
      }
    }
    return 'other';
  }

  fillForm(item: CycleFormItem): void {
    this.router.navigate(['/forms/fill', item.template.id], {
      queryParams: {
        patientId: this.resolvedPatientId || undefined,
        cycleId: this.cycleId || undefined,
      },
    });
  }

  continueForm(item: CycleFormItem): void {
    if (item.latestResponse) {
      this.router.navigate(['/forms/edit', item.latestResponse.id]);
    } else {
      this.fillForm(item);
    }
  }

  viewResponse(item: CycleFormItem): void {
    if (item.latestResponse) {
      this.router.navigate(['/forms/responses', item.latestResponse.id]);
    }
  }

  editResponse(item: CycleFormItem): void {
    if (item.latestResponse) {
      this.router.navigate(['/forms/edit', item.latestResponse.id]);
    }
  }

  previewPdf(item: CycleFormItem): void {
    if (!item.latestResponse) return;
    const responseId = item.latestResponse.id;
    this.exportingId.set(responseId);

    this.formsService.exportResponsePdf(responseId).subscribe({
      next: (blob) => {
        // Ensure correct MIME type for PDF rendering in iframe
        const pdfBlob = new Blob([blob], { type: 'application/pdf' });
        this.currentPdfBlob = pdfBlob;
        this.currentPdfFileName = `${item.template.name.replace(/\s+/g, '_')}_${new Date().toISOString().slice(0, 10)}.pdf`;
        const url = URL.createObjectURL(pdfBlob);
        this.pdfPreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
        this.pdfPreviewTitle.set(item.template.name);
        this.exportingId.set(null);
      },
      error: () => {
        this.exportingId.set(null);
        alert('Có lỗi khi tải PDF');
      },
    });
  }

  closePdfPreview(): void {
    this.pdfPreviewUrl.set(null);
    this.pdfPreviewTitle.set('');
    this.currentPdfBlob = null;
  }

  downloadCurrentPdf(): void {
    if (!this.currentPdfBlob) return;
    const url = URL.createObjectURL(this.currentPdfBlob);
    const a = document.createElement('a');
    a.href = url;
    a.download = this.currentPdfFileName;
    a.click();
    URL.revokeObjectURL(url);
  }

  formatDate(dateStr: string | Date): string {
    if (!dateStr) return '';
    return new Date(dateStr).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }
}
