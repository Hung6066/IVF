import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsService, FormResponse, FormFieldValue, ResponseStatus, FormTemplate, FormField } from '../forms.service';

@Component({
    selector: 'app-form-response-detail',
    standalone: true,
    imports: [CommonModule, RouterLink],
    template: `
        <div class="response-detail-container">
            @if (response && template) {
                <header class="detail-header">
                    <div class="header-info">
                        <a routerLink="/forms/responses" class="back-link">← Quay lại</a>
                        <h1>{{ response.formTemplateName }}</h1>
                        <div class="meta-info">
                            <span class="status-badge" [class]="getStatusClass(response.status)">
                                {{ getStatusLabel(response.status) }}
                            </span>
                            <span>📅 Gửi: {{ response.submittedAt | date:'dd/MM/yyyy HH:mm' }}</span>
                            @if (response.patientName) {
                                <span>👤 {{ response.patientName }}</span>
                            }
                        </div>
                    </div>
                    <div class="header-actions">
                        <button class="btn btn-secondary" (click)="print()">🖨️ In</button>
                    </div>
                </header>

                <div class="response-content">
                    <div class="form-preview-card">
                        <h2>Nội dung phản hồi</h2>
                        <div class="values-grid">
                            @for (field of template.fields; track field.id) {
                                <div 
                                    class="value-item"
                                    [style.grid-column]="'span ' + getFieldColSpan(field)"
                                    [class.col-1]="getFieldColSpan(field) === 1"
                                    [class.col-2]="getFieldColSpan(field) === 2"
                                    [class.col-3]="getFieldColSpan(field) === 3"
                                    [class.col-4]="getFieldColSpan(field) === 4">
                                    <label>
                                        {{ field.label }}
                                        @if (field.isRequired) {
                                            <span class="required">*</span>
                                        }
                                    </label>
                                    <div class="value">{{ getFieldDisplayValue(field) }}</div>
                                </div>
                            }

                            @if (!template.fields?.length) {
                                <p class="no-data">Không có trường nào</p>
                            }
                        </div>
                    </div>
                </div>
            } @else {
                <div class="loading">
                    <div class="spinner"></div>
                    <p>Đang tải...</p>
                </div>
            }
        </div>
    `,
    styles: [`
        .response-detail-container {
            padding: 24px;
            max-width: 1000px;
            margin: 0 auto;
        }

        .detail-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            padding: 24px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 16px;
            color: white;
            margin-bottom: 24px;
        }

        .back-link {
            color: rgba(255,255,255,0.8);
            text-decoration: none;
            font-size: 14px;
            margin-bottom: 8px;
            display: inline-block;
        }

        .detail-header h1 {
            margin: 0 0 12px;
        }

        .meta-info {
            display: flex;
            gap: 16px;
            align-items: center;
            flex-wrap: wrap;
            font-size: 14px;
        }

        .status-badge {
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
        }

        .status-approved { background: rgba(34,197,94,0.2); color: #86efac; }
        .status-rejected { background: rgba(239,68,68,0.2); color: #fca5a5; }
        .status-submitted { background: rgba(251,191,36,0.2); color: #fde047; }
        .status-draft { background: rgba(255,255,255,0.2); color: white; }

        .header-actions {
            display: flex;
            gap: 12px;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-secondary {
            background: rgba(255,255,255,0.2);
            color: white;
        }

        .form-preview-card {
            background: white;
            border-radius: 16px;
            padding: 24px;
            margin-bottom: 24px;
        }

        .form-preview-card h2 {
            margin: 0 0 24px;
            font-size: 18px;
            color: #1e293b;
            padding-bottom: 16px;
            border-bottom: 1px solid #e2e8f0;
        }

        .values-grid {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 16px;
        }

        .value-item {
            padding: 16px;
            background: #f8fafc;
            border-radius: 10px;
            border-left: 4px solid #667eea;
        }

        .value-item.col-1 { border-left-color: #3b82f6; }
        .value-item.col-2 { border-left-color: #10b981; }
        .value-item.col-3 { border-left-color: #f59e0b; }
        .value-item.col-4 { border-left-color: #667eea; }

        .value-item label {
            display: block;
            font-size: 13px;
            color: #64748b;
            margin-bottom: 8px;
            font-weight: 500;
        }

        .value-item .required {
            color: #ef4444;
        }

        .value {
            font-size: 15px;
            color: #1e293b;
            font-weight: 500;
        }

        .notes-card {
            background: #fef3c7;
            border-radius: 12px;
            padding: 20px;
        }

        .notes-card h3 {
            margin: 0 0 12px;
            font-size: 16px;
            color: #92400e;
        }

        .notes-card p {
            margin: 0;
            color: #78350f;
        }

        .no-data {
            grid-column: span 4;
            color: #94a3b8;
            text-align: center;
            padding: 40px;
        }

        .loading {
            text-align: center;
            padding: 80px;
        }

        .spinner {
            width: 48px;
            height: 48px;
            border: 4px solid #e2e8f0;
            border-top-color: #667eea;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin: 0 auto 16px;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        /* Responsive */
        @media (max-width: 768px) {
            .response-detail-container {
                padding: 12px;
            }

            .detail-header {
                flex-direction: column;
                gap: 16px;
                padding: 16px;
            }

            .detail-header h1 {
                font-size: 20px;
            }

            .meta-info {
                flex-direction: column;
                align-items: flex-start;
                gap: 8px;
            }

            .values-grid {
                grid-template-columns: repeat(2, 1fr);
            }

            .value-item {
                grid-column: span 2 !important;
            }

            .no-data {
                grid-column: span 2;
            }
        }

        @media print {
            .header-actions, .back-link {
                display: none;
            }

            .detail-header {
                background: #1e293b;
                -webkit-print-color-adjust: exact;
            }

            .form-preview-card {
                box-shadow: none;
                border: 1px solid #e2e8f0;
            }

            .value-item {
                break-inside: avoid;
            }
        }
    `]
})
export class FormResponseDetailComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly route = inject(ActivatedRoute);

    response: FormResponse | null = null;
    template: FormTemplate | null = null;
    private fieldValuesMap: Map<string, FormFieldValue> = new Map();

    ngOnInit() {
        this.route.params.subscribe(params => {
            if (params['id']) {
                this.formsService.getResponseById(params['id']).subscribe(r => {
                    this.response = r;
                    // Build field values map for quick lookup
                    if (r.fieldValues) {
                        r.fieldValues.forEach(fv => {
                            this.fieldValuesMap.set(fv.formFieldId, fv);
                        });
                    }
                    // Load the template to get field layout info
                    if (r.formTemplateId) {
                        this.formsService.getTemplateById(r.formTemplateId).subscribe(t => {
                            this.template = t;
                        });
                    }
                });
            }
        });
    }

    getFieldColSpan(field: FormField): number {
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            return rules.colSpan || 4;
        } catch {
            return 4;
        }
    }

    getFieldDisplayValue(field: FormField): string {
        const fv = this.fieldValuesMap.get(field.id);
        if (!fv) return '-';
        return this.getDisplayValue(fv);
    }

    getDisplayValue(value: FormFieldValue): string {
        if (value.textValue) return value.textValue;
        if (value.numericValue != null) return value.numericValue.toString();
        if (value.dateValue) return new Date(value.dateValue).toLocaleDateString('vi-VN');
        if (value.booleanValue != null) return value.booleanValue ? 'Có' : 'Không';
        if (value.jsonValue) {
            try {
                const arr = JSON.parse(value.jsonValue);
                return Array.isArray(arr) ? arr.join(', ') : value.jsonValue;
            } catch {
                return value.jsonValue;
            }
        }
        return '-';
    }

    getStatusLabel(status: ResponseStatus): string {
        const labels: { [key: number]: string } = {
            [ResponseStatus.Draft]: 'Bản nháp',
            [ResponseStatus.Submitted]: 'Chờ duyệt',
            [ResponseStatus.Reviewed]: 'Đã xem',
            [ResponseStatus.Approved]: 'Đã duyệt',
            [ResponseStatus.Rejected]: 'Từ chối'
        };
        return labels[status] || '';
    }

    getStatusClass(status: ResponseStatus): string {
        const classes: { [key: number]: string } = {
            [ResponseStatus.Draft]: 'status-draft',
            [ResponseStatus.Submitted]: 'status-submitted',
            [ResponseStatus.Reviewed]: 'status-reviewed',
            [ResponseStatus.Approved]: 'status-approved',
            [ResponseStatus.Rejected]: 'status-rejected'
        };
        return classes[status] || '';
    }

    print() {
        window.print();
    }
}
