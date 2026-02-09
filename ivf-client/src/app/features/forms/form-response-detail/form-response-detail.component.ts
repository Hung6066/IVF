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
                // Prioritize Details (Normalized Data)
                if (fv.details && fv.details.length > 0) {
                    const detail = fv.details[0];
                    if (detail.conceptId) {
                        // We don't have code/display in detail yet, just label. 
                        // If we want code, we'd need to fetch concept or store it in detail.
                        // For now, just label is better than nothing.
                        return detail.label || detail.value;
                    }
                    return detail.label || detail.value;
                }

                const val = fv.textValue;
                if (!val) return '-';
                // Try to find label for the value
                const options = getOptions();
                const option = options.find(o => o.value === val);
                const label = option ? option.label : val;

                const conceptInfo = this.getConceptInfo(field, val);
                if (conceptInfo?.code) {
                    return `${label} <span class="concept-badge-small" title="${conceptInfo.display || ''}">[${conceptInfo.code}]</span>`;
                }
                return label;

            case 9: // MultiSelect
                if (fv.details && fv.details.length > 0) {
                    return fv.details.map(d => d.label || d.value).join(', ');
                }

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
                // Check if this is a checkbox group (has details or jsonValue array)
                if (fv.details && fv.details.length > 0) {
                    return fv.details.map(d => d.label || d.value).join(', ');
                }

                if (fv.jsonValue) {
                    try {
                        const selectedValues = JSON.parse(fv.jsonValue);
                        if (Array.isArray(selectedValues) && selectedValues.length > 0) {
                            const opts = getOptions();
                            const labels = selectedValues.map(v => {
                                const opt = opts.find(o => o.value === v);
                                return opt ? opt.label : v;
                            });
                            return labels.join(', ');
                        }
                    } catch {
                        // Not JSON, fall through to boolean
                    }
                }

                // Single boolean checkbox
                if (fv.booleanValue !== null && fv.booleanValue !== undefined) {
                    return fv.booleanValue ? 'Có' : 'Không';
                }
                return '-';

            case 16: // Tags
                // Prioritize details (normalized data with concept info)
                if (fv.details && fv.details.length > 0) {
                    return fv.details.map(d => {
                        const label = d.label || d.value;
                        if (d.conceptId) {
                            return `🏷️ ${label}`;
                        }
                        return `🏷️ ${label}`;
                    }).join(' ');
                }

                // Fallback to jsonValue
                if (fv.jsonValue) {
                    try {
                        const tagValues = JSON.parse(fv.jsonValue);
                        if (Array.isArray(tagValues) && tagValues.length > 0) {
                            const opts = getOptions();
                            const labels = tagValues.map(v => {
                                const opt = opts.find(o => o.value === v);
                                return opt ? opt.label : v;
                            });
                            return labels.map(l => `🏷️ ${l}`).join(' ');
                        }
                    } catch {
                        return fv.jsonValue;
                    }
                }

                if (fv.textValue) return fv.textValue;
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

        // Use Normalized Details if available
        if (fv.details && fv.details.length > 0) {
            return fv.details.map(d => `<span class="tag-badge">🏷️ ${d.label || d.value}</span>`).join(' ');
        }

        // Check jsonValue (structure: { text, tagIds, html })
        // Check textValue (fallback for older submissions or if jsonValue failed)
        let jsonData = fv.jsonValue || fv.textValue;

        if (!jsonData) return '-';

        // If it looks like JSON array (old logic might result in array string)
        if (jsonData.trim().startsWith('[')) {
            try {
                const arr = JSON.parse(jsonData);
                if (Array.isArray(arr)) {
                    // It's a string array of tags
                    return arr.map(t => `<span class="tag-badge">🏷️ ${t}</span>`).join(' ');
                }
            } catch { }
        }

        // If it looks like JSON object (legacy implementation reference?)
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

    getConceptInfo(field: FormField, value: string): { code?: string; display?: string } | null {
        try {
            const options = field.optionsJson ? JSON.parse(field.optionsJson) : [];
            const option = options.find((o: any) => o.value === value);
            if (option && option.conceptCode) {
                return {
                    code: option.conceptCode,
                    display: option.conceptDisplay
                };
            }
            return null;
        } catch {
            return null;
        }
    }
}
