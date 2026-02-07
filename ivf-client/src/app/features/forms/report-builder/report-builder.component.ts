import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FormsService, FormTemplate, ReportTemplate, ReportType } from '../forms.service';

@Component({
    selector: 'app-report-builder',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
        <div class="report-builder-container">
            <header class="page-header">
                <div class="header-content">
                    <h1>📈 Trình tạo báo cáo</h1>
                    <p>Tạo báo cáo động từ dữ liệu biểu mẫu</p>
                </div>
            </header>

            <!-- Existing Reports -->
            <section class="reports-section">
                <div class="section-header">
                    <h2>Báo cáo đã tạo</h2>
                    <button class="btn btn-primary" (click)="showCreateModal = true">
                        ➕ Tạo báo cáo mới
                    </button>
                </div>

                <div class="reports-grid">
                    @for (report of reports; track report.id) {
                        <div class="report-card" (click)="viewReport(report)">
                            <div class="report-icon">
                                {{ getReportTypeIcon(report.reportType) }}
                            </div>
                            <div class="report-info">
                                <h3>{{ report.name }}</h3>
                                <p>{{ report.formTemplateName }}</p>
                                <span class="report-type">{{ getReportTypeLabel(report.reportType) }}</span>
                            </div>
                            <div class="report-actions">
                                <button class="btn-icon" title="Xem" (click)="viewReport(report); $event.stopPropagation()">👁️</button>
                                <button class="btn-icon" title="Xóa" (click)="deleteReport(report); $event.stopPropagation()">🗑️</button>
                            </div>
                        </div>
                    }

                    @if (reports.length === 0) {
                        <div class="empty-state">
                            <span class="icon">📊</span>
                            <h3>Chưa có báo cáo nào</h3>
                            <p>Tạo báo cáo đầu tiên để phân tích dữ liệu</p>
                        </div>
                    }
                </div>
            </section>

            <!-- Create Modal -->
            @if (showCreateModal) {
                <div class="modal-overlay" (click)="showCreateModal = false">
                    <div class="modal" (click)="$event.stopPropagation()">
                        <div class="modal-header">
                            <h3>Tạo báo cáo mới</h3>
                            <button class="close-btn" (click)="showCreateModal = false">✕</button>
                        </div>
                        <div class="modal-body">
                            <div class="form-group">
                                <label>Chọn biểu mẫu *</label>
                                <select [(ngModel)]="newReport.formTemplateId" required>
                                    <option value="">-- Chọn biểu mẫu --</option>
                                    @for (t of templates; track t.id) {
                                        <option [value]="t.id">{{ t.name }}</option>
                                    }
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Tên báo cáo *</label>
                                <input type="text" [(ngModel)]="newReport.name" placeholder="VD: Thống kê tháng 1">
                            </div>
                            <div class="form-group">
                                <label>Mô tả</label>
                                <textarea [(ngModel)]="newReport.description" placeholder="Mô tả báo cáo..."></textarea>
                            </div>
                            <div class="form-group">
                                <label>Loại báo cáo</label>
                                <div class="report-type-grid">
                                    @for (type of reportTypes; track type.value) {
                                        <button 
                                            type="button"
                                            class="type-option"
                                            [class.selected]="newReport.reportType === type.value"
                                            (click)="newReport.reportType = type.value">
                                            <span class="type-icon">{{ type.icon }}</span>
                                            <span>{{ type.label }}</span>
                                        </button>
                                    }
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button class="btn btn-secondary" (click)="showCreateModal = false">Hủy</button>
                            <button 
                                class="btn btn-primary" 
                                (click)="createReport()"
                                [disabled]="!newReport.formTemplateId || !newReport.name">
                                Tạo báo cáo
                            </button>
                        </div>
                    </div>
                </div>
            }
        </div>
    `,
    styles: [`
        .report-builder-container {
            padding: 24px;
            max-width: 1200px;
            margin: 0 auto;
        }

        .page-header {
            padding: 24px;
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            border-radius: 16px;
            color: white;
            margin-bottom: 24px;
        }

        .page-header h1 {
            margin: 0 0 8px;
        }

        .page-header p {
            margin: 0;
            opacity: 0.9;
        }

        .section-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
        }

        .section-header h2 {
            margin: 0;
            font-size: 18px;
            color: #1e293b;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
            display: inline-flex;
            align-items: center;
            gap: 8px;
        }

        .btn-primary {
            background: #10b981;
            color: white;
        }

        .btn-secondary {
            background: #e2e8f0;
            color: #475569;
        }

        .reports-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
            gap: 20px;
        }

        .report-card {
            display: flex;
            align-items: center;
            gap: 16px;
            padding: 20px;
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            cursor: pointer;
            transition: all 0.2s;
        }

        .report-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 16px rgba(0,0,0,0.1);
        }

        .report-icon {
            font-size: 36px;
            width: 60px;
            height: 60px;
            display: flex;
            align-items: center;
            justify-content: center;
            background: #f0fdf4;
            border-radius: 12px;
        }

        .report-info {
            flex: 1;
        }

        .report-info h3 {
            margin: 0 0 4px;
            color: #1e293b;
        }

        .report-info p {
            margin: 0;
            color: #64748b;
            font-size: 13px;
        }

        .report-type {
            display: inline-block;
            margin-top: 8px;
            padding: 2px 8px;
            background: #e0f2fe;
            color: #0284c7;
            border-radius: 4px;
            font-size: 11px;
        }

        .report-actions {
            display: flex;
            gap: 8px;
        }

        .btn-icon {
            background: none;
            border: none;
            padding: 8px;
            cursor: pointer;
            border-radius: 6px;
        }

        .btn-icon:hover {
            background: #f1f5f9;
        }

        .empty-state {
            grid-column: 1 / -1;
            text-align: center;
            padding: 60px;
            background: #f8fafc;
            border-radius: 12px;
        }

        .empty-state .icon {
            font-size: 56px;
            display: block;
            margin-bottom: 16px;
        }

        .empty-state h3 {
            margin: 0 0 8px;
            color: #1e293b;
        }

        .empty-state p {
            margin: 0;
            color: #64748b;
        }

        /* Modal */
        .modal-overlay {
            position: fixed;
            inset: 0;
            background: rgba(0,0,0,0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1000;
        }

        .modal {
            background: white;
            border-radius: 16px;
            width: 100%;
            max-width: 500px;
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 20px 24px;
            border-bottom: 1px solid #e2e8f0;
        }

        .modal-header h3 {
            margin: 0;
        }

        .close-btn {
            background: none;
            border: none;
            font-size: 20px;
            cursor: pointer;
        }

        .modal-body {
            padding: 24px;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: #374151;
        }

        .form-group input,
        .form-group select,
        .form-group textarea {
            width: 100%;
            padding: 10px 12px;
            border: 1px solid #d1d5db;
            border-radius: 8px;
        }

        .report-type-grid {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 8px;
        }

        .type-option {
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 12px;
            background: #f8fafc;
            border: 2px solid transparent;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.2s;
        }

        .type-option:hover {
            border-color: #10b981;
        }

        .type-option.selected {
            border-color: #10b981;
            background: #f0fdf4;
        }

        .type-icon {
            font-size: 24px;
            margin-bottom: 4px;
        }

        .type-option span:last-child {
            font-size: 11px;
            color: #64748b;
        }

        .modal-footer {
            display: flex;
            justify-content: flex-end;
            gap: 12px;
            padding: 16px 24px;
            border-top: 1px solid #e2e8f0;
        }
    `]
})
export class ReportBuilderComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly router = inject(Router);

    templates: FormTemplate[] = [];
    reports: ReportTemplate[] = [];
    showCreateModal = false;

    newReport = {
        formTemplateId: '',
        name: '',
        description: '',
        reportType: ReportType.Table
    };

    reportTypes = [
        { value: ReportType.Table, icon: '📋', label: 'Bảng' },
        { value: ReportType.BarChart, icon: '📊', label: 'Cột' },
        { value: ReportType.LineChart, icon: '📈', label: 'Đường' },
        { value: ReportType.PieChart, icon: '🥧', label: 'Tròn' },
        { value: ReportType.Summary, icon: '📝', label: 'Tổng hợp' }
    ];

    ngOnInit() {
        this.formsService.getTemplates(undefined, true).subscribe(t => {
            this.templates = t;
            this.loadAllReports();
        });
    }

    loadAllReports() {
        const allReports: ReportTemplate[] = [];
        for (const template of this.templates) {
            this.formsService.getReportTemplates(template.id).subscribe(reports => {
                allReports.push(...reports);
                this.reports = [...allReports];
            });
        }
    }

    getReportTypeIcon(type: ReportType): string {
        const icons: { [key: number]: string } = {
            [ReportType.Table]: '📋',
            [ReportType.BarChart]: '📊',
            [ReportType.LineChart]: '📈',
            [ReportType.PieChart]: '🥧',
            [ReportType.Summary]: '📝'
        };
        return icons[type] || '📊';
    }

    getReportTypeLabel(type: ReportType): string {
        const labels: { [key: number]: string } = {
            [ReportType.Table]: 'Bảng',
            [ReportType.BarChart]: 'Biểu đồ cột',
            [ReportType.LineChart]: 'Biểu đồ đường',
            [ReportType.PieChart]: 'Biểu đồ tròn',
            [ReportType.Summary]: 'Báo cáo tổng hợp'
        };
        return labels[type] || '';
    }

    createReport() {
        this.formsService.createReportTemplate({
            formTemplateId: this.newReport.formTemplateId,
            name: this.newReport.name,
            description: this.newReport.description,
            reportType: this.newReport.reportType,
            configurationJson: '{}',
            createdByUserId: 'current-user'
        }).subscribe(report => {
            this.reports.push(report);
            this.showCreateModal = false;
            this.newReport = { formTemplateId: '', name: '', description: '', reportType: ReportType.Table };
        });
    }

    viewReport(report: ReportTemplate) {
        this.router.navigate(['/forms/reports', report.id]);
    }

    deleteReport(report: ReportTemplate) {
        if (confirm('Xóa báo cáo này?')) {
            this.formsService.deleteReportTemplate(report.id).subscribe(() => {
                this.reports = this.reports.filter(r => r.id !== report.id);
            });
        }
    }
}
