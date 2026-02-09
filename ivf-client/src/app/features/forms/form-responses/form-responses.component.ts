import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormsService, FormResponse, FormTemplate, FormField, ResponseStatus } from '../forms.service';

@Component({
    selector: 'app-form-responses',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    templateUrl: './form-responses.component.html',
    styleUrls: ['./form-responses.component.scss']
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
        const status = this.filterStatus ? parseInt(this.filterStatus) : undefined;

        this.formsService.getResponses(
            this.filterTemplateId || undefined,
            undefined,
            from,
            to,
            this.currentPage,
            this.pageSize,
            status
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
