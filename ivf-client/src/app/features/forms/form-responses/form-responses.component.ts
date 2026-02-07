import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormsService, FormResponse, FormTemplate, FormField, ResponseStatus } from '../forms.service';

@Component({
    selector: 'app-form-responses',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
        <div class="responses-container">
            <header class="page-header">
                <div class="header-content">
                    <h1>📊 Phản hồi biểu mẫu</h1>
                    <p>Xem và quản lý tất cả các phản hồi đã gửi</p>
                </div>
                <div class="header-actions">
                    <button class="btn btn-secondary" (click)="exportCSV()">📥 Xuất CSV</button>
                </div>
            </header>

            <!-- Filters -->
            <div class="filters-bar">
                <div class="filter-group">
                    <label>Biểu mẫu:</label>
                    <select [(ngModel)]="filterTemplateId" (change)="onTemplateChange()">
                        <option value="">Tất cả</option>
                        @for (t of templates; track t.id) {
                            <option [value]="t.id">{{ t.name }}</option>
                        }
                    </select>
                </div>
                <div class="filter-group">
                    <label>Từ ngày:</label>
                    <input type="date" [(ngModel)]="filterFrom" (change)="loadResponses()">
                </div>
                <div class="filter-group">
                    <label>Đến ngày:</label>
                    <input type="date" [(ngModel)]="filterTo" (change)="loadResponses()">
                </div>
                <div class="filter-group">
                    <label>Trạng thái:</label>
                    <select [(ngModel)]="filterStatus" (change)="loadResponses()">
                        <option value="">Tất cả</option>
                        <option value="1">Bản nháp</option>
                        <option value="2">Đã gửi</option>
                        <option value="3">Đã xem</option>
                        <option value="4">Đã duyệt</option>
                        <option value="5">Từ chối</option>
                    </select>
                </div>
                <div class="filter-group view-toggle">
                    <button 
                        class="toggle-btn" 
                        [class.active]="viewMode === 'table'"
                        (click)="viewMode = 'table'">
                        📋 Bảng
                    </button>
                    <button 
                        class="toggle-btn" 
                        [class.active]="viewMode === 'grid'"
                        (click)="viewMode = 'grid'">
                        📦 Thẻ
                    </button>
                </div>
            </div>

            <!-- Summary Stats -->
            @if (filterTemplateId && selectedTemplate) {
                <div class="stats-row">
                    <div class="stat-card">
                        <span class="stat-value">{{ totalResponses }}</span>
                        <span class="stat-label">Tổng phản hồi</span>
                    </div>
                    <div class="stat-card">
                        <span class="stat-value">{{ getStatusCount(ResponseStatus.Submitted) }}</span>
                        <span class="stat-label">Chờ duyệt</span>
                    </div>
                    <div class="stat-card">
                        <span class="stat-value">{{ getStatusCount(ResponseStatus.Approved) }}</span>
                        <span class="stat-label">Đã duyệt</span>
                    </div>
                    <div class="stat-card">
                        <span class="stat-value">{{ selectedTemplate.fields?.length || 0 }}</span>
                        <span class="stat-label">Số trường</span>
                    </div>
                </div>
            }

            <!-- Multi-column Table View -->
            @if (viewMode === 'table') {
                <div class="responses-table-wrapper">
                    <table class="responses-table">
                        <thead>
                            <tr>
                                <th class="sticky-col">STT</th>
                                <th class="sticky-col">Trạng thái</th>
                                @if (!filterTemplateId) {
                                    <th>Biểu mẫu</th>
                                }
                                <th>Bệnh nhân</th>
                                <!-- Dynamic field columns -->
                                @if (selectedTemplate?.fields) {
                                    @for (field of selectedTemplate!.fields!; track field.id) {
                                        <th [style.min-width.px]="getColumnWidth(field)">{{ field.label }}</th>
                                    }
                                }
                                <th>Ngày gửi</th>
                                <th class="sticky-col-right">Thao tác</th>
                            </tr>
                        </thead>
                        <tbody>
                            @for (response of responses; track response.id; let i = $index) {
                                <tr [class.even]="i % 2 === 0">
                                    <td class="sticky-col">{{ (currentPage - 1) * pageSize + i + 1 }}</td>
                                    <td class="sticky-col">
                                        <span class="status-badge" [class]="getStatusClass(response.status)">
                                            {{ getStatusLabel(response.status) }}
                                        </span>
                                    </td>
                                    @if (!filterTemplateId) {
                                        <td>{{ response.formTemplateName }}</td>
                                    }
                                    <td>{{ response.patientName || 'N/A' }}</td>
                                    <!-- Dynamic field values -->
                                    @if (selectedTemplate?.fields) {
                                        @for (field of selectedTemplate!.fields!; track field.id) {
                                            <td>{{ getFieldValue(response, field) }}</td>
                                        }
                                    }
                                    <td>{{ response.submittedAt ? (response.submittedAt | date:'dd/MM/yyyy HH:mm') : '-' }}</td>
                                    <td class="sticky-col-right">
                                        <div class="actions">
                                            <a [routerLink]="['/forms/responses', response.id]" class="btn-icon" title="Xem">👁️</a>
                                            @if (response.status === ResponseStatus.Submitted) {
                                                <button class="btn-icon success" title="Duyệt" (click)="approve(response)">✅</button>
                                                <button class="btn-icon danger" title="Từ chối" (click)="reject(response)">❌</button>
                                            }
                                        </div>
                                    </td>
                                </tr>
                            }

                            @if (responses.length === 0) {
                                <tr>
                                    <td [attr.colspan]="getColumnCount()" class="empty-row">
                                        <div class="empty-state">
                                            <span class="icon">📭</span>
                                            <p>Chưa có phản hồi nào</p>
                                        </div>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }

            <!-- Grid/Card View -->
            @if (viewMode === 'grid') {
                <div class="responses-grid">
                    @for (response of responses; track response.id) {
                        <div class="response-card" [class]="getStatusClass(response.status)">
                            <div class="card-header">
                                <span class="status-badge" [class]="getStatusClass(response.status)">
                                    {{ getStatusLabel(response.status) }}
                                </span>
                                <span class="date">{{ response.submittedAt | date:'dd/MM/yyyy' }}</span>
                            </div>
                            <h3>{{ response.formTemplateName }}</h3>
                            @if (response.patientName) {
                                <p class="patient">👤 {{ response.patientName }}</p>
                            }
                            
                            <div class="field-values">
                                @if (response.fieldValues) {
                                    @for (fv of response.fieldValues.slice(0, 4); track fv.formFieldId) {
                                        <div class="field-value-row">
                                            <span class="label">{{ fv.fieldLabel }}:</span>
                                            <span class="value">{{ getDisplayValue(fv) }}</span>
                                        </div>
                                    }
                                    @if (response.fieldValues.length > 4) {
                                        <div class="more-fields">+{{ response.fieldValues.length - 4 }} trường khác</div>
                                    }
                                }
                            </div>
                            
                            <div class="card-footer">
                                <a [routerLink]="['/forms/responses', response.id]" class="btn btn-sm">Xem chi tiết</a>
                                @if (response.status === ResponseStatus.Submitted) {
                                    <button class="btn btn-sm btn-success" (click)="approve(response)">Duyệt</button>
                                }
                            </div>
                        </div>
                    }
                </div>

                @if (responses.length === 0) {
                    <div class="empty-state-centered">
                        <span class="icon">📭</span>
                        <h3>Chưa có phản hồi nào</h3>
                        <p>Các phản hồi sẽ xuất hiện ở đây khi người dùng gửi biểu mẫu</p>
                    </div>
                }
            }

            <!-- Pagination -->
            @if (totalResponses > pageSize) {
                <div class="pagination">
                    <button (click)="prevPage()" [disabled]="currentPage === 1">← Trước</button>
                    <span>Trang {{ currentPage }} / {{ totalPages }} ({{ totalResponses }} phản hồi)</span>
                    <button (click)="nextPage()" [disabled]="currentPage >= totalPages">Sau →</button>
                </div>
            }
        </div>
    `,
    styles: [`
        .responses-container {
            padding: 24px;
            max-width: 100%;
            margin: 0 auto;
        }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 24px;
            padding: 24px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 16px;
            color: white;
        }

        .page-header h1 { margin: 0 0 8px; }
        .page-header p { margin: 0; opacity: 0.9; }

        .btn {
            padding: 8px 16px;
            border: none;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 500;
        }

        .btn-secondary { background: rgba(255,255,255,0.2); color: white; }
        .btn-success { background: #22c55e; color: white; }
        .btn-sm { padding: 6px 12px; font-size: 13px; }

        .filters-bar {
            display: flex;
            gap: 16px;
            flex-wrap: wrap;
            margin-bottom: 24px;
            padding: 16px 20px;
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            align-items: center;
        }

        .filter-group {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .filter-group label {
            font-size: 14px;
            color: #64748b;
            white-space: nowrap;
        }

        .filter-group select,
        .filter-group input {
            padding: 8px 12px;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            background: white;
        }

        .view-toggle {
            margin-left: auto;
            gap: 0;
        }

        .toggle-btn {
            padding: 8px 16px;
            border: 1px solid #e2e8f0;
            background: white;
            cursor: pointer;
        }

        .toggle-btn:first-child { border-radius: 6px 0 0 6px; }
        .toggle-btn:last-child { border-radius: 0 6px 6px 0; border-left: none; }
        .toggle-btn.active { background: #667eea; color: white; border-color: #667eea; }

        .stats-row {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 16px;
            margin-bottom: 24px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 12px;
            text-align: center;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
        }

        .stat-value {
            display: block;
            font-size: 32px;
            font-weight: 700;
            color: #1e293b;
        }

        .stat-label {
            color: #64748b;
            font-size: 14px;
        }

        .responses-table-wrapper {
            background: white;
            border-radius: 12px;
            overflow-x: auto;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
        }

        .responses-table {
            width: 100%;
            border-collapse: collapse;
            white-space: nowrap;
        }

        .responses-table th {
            background: #f8fafc;
            padding: 14px 16px;
            text-align: left;
            font-weight: 600;
            color: #475569;
            font-size: 13px;
            border-bottom: 2px solid #e2e8f0;
            position: sticky;
            top: 0;
        }

        .responses-table td {
            padding: 12px 16px;
            border-bottom: 1px solid #f1f5f9;
            max-width: 200px;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .responses-table tr:hover { background: #f8fafc; }
        .responses-table tr.even { background: #fafafa; }

        .sticky-col {
            position: sticky;
            left: 0;
            background: inherit;
            z-index: 1;
        }

        .sticky-col-right {
            position: sticky;
            right: 0;
            background: inherit;
            z-index: 1;
        }

        .status-badge {
            display: inline-block;
            padding: 4px 10px;
            border-radius: 20px;
            font-size: 11px;
            font-weight: 600;
        }

        .status-draft { background: #f1f5f9; color: #64748b; }
        .status-submitted { background: #fef3c7; color: #b45309; }
        .status-reviewed { background: #dbeafe; color: #1d4ed8; }
        .status-approved { background: #dcfce7; color: #16a34a; }
        .status-rejected { background: #fee2e2; color: #dc2626; }

        .actions {
            display: flex;
            gap: 4px;
        }

        .btn-icon {
            background: none;
            border: none;
            cursor: pointer;
            padding: 6px;
            border-radius: 6px;
            text-decoration: none;
            font-size: 14px;
        }

        .btn-icon:hover { background: #f1f5f9; }

        /* Grid View */
        .responses-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
            gap: 20px;
        }

        .response-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            border-left: 4px solid #e2e8f0;
        }

        .response-card.status-approved { border-left-color: #22c55e; }
        .response-card.status-submitted { border-left-color: #f59e0b; }
        .response-card.status-rejected { border-left-color: #ef4444; }

        .card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 12px;
        }

        .response-card h3 {
            margin: 0 0 8px;
            font-size: 16px;
            color: #1e293b;
        }

        .patient {
            color: #64748b;
            font-size: 14px;
            margin: 0 0 16px;
        }

        .field-values {
            background: #f8fafc;
            padding: 12px;
            border-radius: 8px;
            margin-bottom: 16px;
        }

        .field-value-row {
            display: flex;
            justify-content: space-between;
            padding: 4px 0;
            font-size: 13px;
        }

        .field-value-row .label { color: #64748b; }
        .field-value-row .value { font-weight: 500; color: #1e293b; }

        .more-fields {
            text-align: center;
            color: #94a3b8;
            font-size: 12px;
            padding-top: 8px;
        }

        .card-footer {
            display: flex;
            gap: 8px;
        }

        .empty-row { text-align: center; }

        .empty-state, .empty-state-centered {
            padding: 60px 20px;
            text-align: center;
        }

        .empty-state .icon, .empty-state-centered .icon {
            font-size: 64px;
            display: block;
            margin-bottom: 16px;
        }

        .empty-state-centered {
            background: white;
            border-radius: 12px;
            margin-top: 24px;
        }

        .pagination {
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 16px;
            margin-top: 24px;
            padding: 16px;
            background: white;
            border-radius: 12px;
        }

        .pagination button {
            padding: 8px 16px;
            border: 1px solid #e2e8f0;
            background: white;
            border-radius: 6px;
            cursor: pointer;
        }

        .pagination button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }

        /* Responsive Styles */
        @media (max-width: 1024px) {
            .stats-row {
                grid-template-columns: repeat(2, 1fr);
            }

            .responses-grid {
                grid-template-columns: repeat(2, 1fr);
            }

            .filters-bar {
                flex-direction: column;
                align-items: stretch;
            }

            .filter-group {
                width: 100%;
            }

            .filter-group select,
            .filter-group input {
                flex: 1;
            }

            .view-toggle {
                margin-left: 0;
                justify-content: center;
            }
        }

        @media (max-width: 768px) {
            .responses-container {
                padding: 12px;
            }

            .page-header {
                flex-direction: column;
                text-align: center;
                gap: 16px;
                padding: 16px;
            }

            .page-header h1 {
                font-size: 20px;
            }

            .stats-row {
                grid-template-columns: repeat(2, 1fr);
                gap: 8px;
            }

            .stat-card {
                padding: 12px;
            }

            .stat-value {
                font-size: 24px;
            }

            .responses-grid {
                grid-template-columns: 1fr;
            }

            .responses-table th,
            .responses-table td {
                padding: 8px 10px;
                font-size: 12px;
            }

            .sticky-col,
            .sticky-col-right {
                position: static;
            }

            .pagination {
                flex-direction: column;
                gap: 8px;
            }

            .pagination span {
                font-size: 13px;
            }
        }

        @media (max-width: 480px) {
            .filter-group {
                flex-direction: column;
                align-items: flex-start;
            }

            .filter-group select,
            .filter-group input {
                width: 100%;
            }

            .toggle-btn {
                padding: 6px 12px;
                font-size: 13px;
            }

            .response-card {
                padding: 14px;
            }

            .field-values {
                padding: 10px;
            }

            .card-footer {
                flex-direction: column;
            }

            .card-footer .btn {
                width: 100%;
                text-align: center;
            }
        }
    `]
})
export class FormResponsesComponent implements OnInit {
    private readonly formsService = inject(FormsService);

    ResponseStatus = ResponseStatus;
    responses: FormResponse[] = [];
    templates: FormTemplate[] = [];
    selectedTemplate: FormTemplate | null = null;
    totalResponses = 0;
    currentPage = 1;
    pageSize = 20;
    viewMode: 'table' | 'grid' = 'table';

    filterTemplateId = '';
    filterFrom = '';
    filterTo = '';
    filterStatus = '';

    get totalPages() {
        return Math.ceil(this.totalResponses / this.pageSize);
    }

    ngOnInit() {
        this.formsService.getTemplates(undefined, undefined, true).subscribe(t => this.templates = t);
        this.loadResponses();
    }

    onTemplateChange() {
        if (this.filterTemplateId) {
            this.selectedTemplate = this.templates.find(t => t.id === this.filterTemplateId) || null;
            // Load template with fields if not already loaded
            if (this.selectedTemplate && !this.selectedTemplate.fields) {
                this.formsService.getTemplateById(this.filterTemplateId).subscribe(t => {
                    this.selectedTemplate = t;
                });
            }
        } else {
            this.selectedTemplate = null;
        }
        this.loadResponses();
    }

    loadResponses() {
        const from = this.filterFrom ? new Date(this.filterFrom) : undefined;
        const to = this.filterTo ? new Date(this.filterTo) : undefined;

        this.formsService.getResponses(
            this.filterTemplateId || undefined,
            undefined,
            from,
            to,
            this.currentPage,
            this.pageSize
        ).subscribe(result => {
            this.responses = result.items;
            this.totalResponses = result.total;
        });
    }

    getFieldValue(response: FormResponse, field: FormField): string {
        const fv = response.fieldValues?.find(v => v.formFieldId === field.id);
        if (!fv) return '-';
        return this.getDisplayValue(fv);
    }

    getDisplayValue(fv: any): string {
        if (fv.textValue) return fv.textValue;
        if (fv.numericValue !== null && fv.numericValue !== undefined) return fv.numericValue.toString();
        if (fv.dateValue) return new Date(fv.dateValue).toLocaleDateString('vi-VN');
        if (fv.booleanValue !== null && fv.booleanValue !== undefined) return fv.booleanValue ? 'Có' : 'Không';
        if (fv.jsonValue) {
            try {
                const arr = JSON.parse(fv.jsonValue);
                return Array.isArray(arr) ? arr.join(', ') : fv.jsonValue;
            } catch {
                return fv.jsonValue;
            }
        }
        return '-';
    }

    getColumnWidth(field: FormField): number {
        switch (field.fieldType) {
            case 1: // Text
            case 2: // TextArea
                return 180;
            case 3: // Number
            case 4: // Decimal
                return 100;
            case 5: // Date
            case 6: // DateTime
                return 120;
            default:
                return 150;
        }
    }

    getColumnCount(): number {
        let count = 5; // Base columns: STT, Status, Patient, Date, Actions
        if (!this.filterTemplateId) count++;
        if (this.selectedTemplate?.fields) count += this.selectedTemplate.fields.length;
        return count;
    }

    getStatusCount(status: ResponseStatus): number {
        return this.responses.filter(r => r.status === status).length;
    }

    getStatusLabel(status: ResponseStatus): string {
        const labels: { [key: number]: string } = {
            [ResponseStatus.Draft]: 'Nháp',
            [ResponseStatus.Submitted]: 'Chờ duyệt',
            [ResponseStatus.Reviewed]: 'Đã xem',
            [ResponseStatus.Approved]: 'Đã duyệt',
            [ResponseStatus.Rejected]: 'Từ chối'
        };
        return labels[status] || status.toString();
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

    approve(response: FormResponse) {
        this.formsService.updateResponseStatus(response.id, ResponseStatus.Approved).subscribe(() => {
            this.loadResponses();
        });
    }

    reject(response: FormResponse) {
        const notes = prompt('Lý do từ chối:');
        if (notes !== null) {
            this.formsService.updateResponseStatus(response.id, ResponseStatus.Rejected, notes).subscribe(() => {
                this.loadResponses();
            });
        }
    }

    prevPage() {
        if (this.currentPage > 1) {
            this.currentPage--;
            this.loadResponses();
        }
    }

    nextPage() {
        if (this.currentPage < this.totalPages) {
            this.currentPage++;
            this.loadResponses();
        }
    }

    exportCSV() {
        if (!this.selectedTemplate || this.responses.length === 0) {
            alert('Vui lòng chọn biểu mẫu và đảm bảo có dữ liệu để xuất');
            return;
        }

        const headers = ['STT', 'Trạng thái', 'Bệnh nhân', 'Ngày gửi'];
        if (this.selectedTemplate.fields) {
            headers.push(...this.selectedTemplate.fields.map(f => f.label));
        }

        const rows = this.responses.map((r, i) => {
            const row = [
                i + 1,
                this.getStatusLabel(r.status),
                r.patientName || 'N/A',
                r.submittedAt ? new Date(r.submittedAt).toLocaleDateString('vi-VN') : '-'
            ];
            if (this.selectedTemplate?.fields) {
                row.push(...this.selectedTemplate.fields.map(f => this.getFieldValue(r, f)));
            }
            return row;
        });

        const csvContent = [headers.join(','), ...rows.map(r => r.map(v => `"${v}"`).join(','))].join('\n');
        const blob = new Blob(['\ufeff' + csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = `${this.selectedTemplate.name}_responses_${new Date().toISOString().split('T')[0]}.csv`;
        link.click();
    }
}
