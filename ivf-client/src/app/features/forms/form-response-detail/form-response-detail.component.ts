import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsService, FormResponse, FormFieldValue, ResponseStatus, FormTemplate, FormField, FieldType } from '../forms.service';

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
                            // Normalize field types
                            this.template = {
                                ...t,
                                fields: (t.fields || []).map(field => ({
                                    ...field,
                                    fieldType: this.normalizeFieldType(field.fieldType)
                                }))
                            };
                        });
                    }
                });
            }
        });
    }

    // Convert string field type from API to numeric enum value
    normalizeFieldType(type: FieldType | string): FieldType {
        if (typeof type === 'number') {
            return type;
        }
        const typeMap: { [key: string]: FieldType } = {
            'Text': FieldType.Text,
            'TextArea': FieldType.TextArea,
            'Number': FieldType.Number,
            'Decimal': FieldType.Decimal,
            'Date': FieldType.Date,
            'DateTime': FieldType.DateTime,
            'Time': FieldType.Time,
            'Dropdown': FieldType.Dropdown,
            'MultiSelect': FieldType.MultiSelect,
            'Radio': FieldType.Radio,
            'Checkbox': FieldType.Checkbox,
            'FileUpload': FieldType.FileUpload,
            'Rating': FieldType.Rating,
            'Section': FieldType.Section,
            'Label': FieldType.Label,
            'Tags': FieldType.Tags
        };
        return typeMap[type as string] || FieldType.Text;
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
        return this.getDisplayValue(field);
    }

    getDisplayValue(field: FormField): string {
        const fv = this.fieldValuesMap.get(field.id);
        if (!fv) return '-';

        // Helper to get options from field definition
        const getOptions = (): any[] => {
            try {
                return field.optionsJson ? JSON.parse(field.optionsJson) : [];
            } catch {
                return [];
            }
        };

        // Handle specific field types
        switch (field.fieldType) {
            case 10: // Radio
            case 8:  // Dropdown
                const val = fv.textValue;
                if (!val) return '-';
                // Try to find label for the value
                const options = getOptions();
                const option = options.find(o => o.value === val);
                return option ? option.label : val;

            case 9: // MultiSelect
                if (fv.jsonValue) {
                    try {
                        const selectedValues = JSON.parse(fv.jsonValue);
                        if (Array.isArray(selectedValues) && selectedValues.length > 0) {
                            const opts = getOptions();
                            // Map values to labels
                            const labels = selectedValues.map(v => {
                                const opt = opts.find(o => o.value === v);
                                return opt ? opt.label : v;
                            });
                            return labels.join(', ');
                        }
                    } catch {
                        return fv.jsonValue;
                    }
                }
                return '-';

            case 11: // Checkbox
                if (fv.booleanValue !== null && fv.booleanValue !== undefined) {
                    return fv.booleanValue ? 'Có' : 'Không';
                }
                return '-';

            case 16: // Tags
                // Allow fallback to text if HTML rendering fails or is not used
                if (fv.textValue) return fv.textValue;
                if (fv.jsonValue) {
                    try {
                        const data = JSON.parse(fv.jsonValue);
                        return data.text || fv.jsonValue;
                    } catch {
                        return fv.jsonValue;
                    }
                }
                return '-';

            case 5: // Date
                if (fv.dateValue) return new Date(fv.dateValue).toLocaleDateString('vi-VN');
                return '-';

            case 6: // DateTime
                if (fv.dateValue) return new Date(fv.dateValue).toLocaleString('vi-VN');
                return '-';

            default:
                if (fv.textValue) return fv.textValue;
                if (fv.numericValue != null) return fv.numericValue.toString();
                return '-';
        }
    }

    // Check if field is Tags type
    isTagsField(field: FormField): boolean {
        return field.fieldType === 16; // FieldType.Tags = 16
    }

    // Get HTML for Tags field (renders inline tag badges)
    getTagsDisplayHtml(field: FormField): string {
        const fv = this.fieldValuesMap.get(field.id);
        if (!fv) return '-';

        // Check jsonValue (structure: { text, tagIds, html })
        // Check textValue (fallback for older submissions or if jsonValue failed)
        let jsonData = fv.jsonValue || fv.textValue;

        if (!jsonData) return '-';

        // If it looks like JSON, parse it
        if (jsonData.trim().startsWith('{')) {
            try {
                const data = JSON.parse(jsonData);
                if (data.html) return data.html;
                if (data.text) return data.text;
            } catch {
                // Not valid JSON, treat as text
            }
        }

        return jsonData;
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
