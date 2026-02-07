import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsService, FormResponse, FormFieldValue, ResponseStatus, FormTemplate, FormField } from '../forms.service';

@Component({
    selector: 'app-form-response-detail',
    standalone: true,
    imports: [CommonModule, RouterLink],
    templateUrl: './form-response-detail.component.html',
    styleUrls: ['./form-response-detail.component.scss']
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
