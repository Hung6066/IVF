import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsService, FormTemplate, FormField, FieldType, FormFieldValue, FieldValueRequest } from '../forms.service';

@Component({
    selector: 'app-form-renderer',
    standalone: true,
    imports: [CommonModule, FormsModule, ReactiveFormsModule],
    templateUrl: './form-renderer.component.html',
    styleUrls: ['./form-renderer.component.scss']
})
export class FormRendererComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);
    private readonly fb = inject(FormBuilder);

    FieldType = FieldType;
    templateId = '';
    template: FormTemplate | null = null;
    fields: FormField[] = [];
    form: FormGroup = this.fb.group({});
    isSubmitting = false;
    fileValues: { [key: string]: File } = {};

    ngOnInit() {
        this.route.params.subscribe(params => {
            if (params['id']) {
                this.templateId = params['id'];
                this.loadTemplate();
            }
        });
    }

    loadTemplate() {
        this.formsService.getTemplateById(this.templateId).subscribe(template => {
            this.template = template;
            this.fields = (template.fields || []).sort((a, b) => a.displayOrder - b.displayOrder);
            this.buildForm();
        });
    }

    buildForm() {
        const formConfig: { [key: string]: FormControl } = {};

        for (const field of this.fields) {
            if (field.fieldType === FieldType.Section || field.fieldType === FieldType.Label) {
                continue;
            }

            const validators = [];
            if (field.isRequired) {
                validators.push(Validators.required);
            }

            let defaultValue: any = field.defaultValue || '';
            if (field.fieldType === FieldType.Checkbox) {
                defaultValue = field.defaultValue === 'true';
            } else if (field.fieldType === FieldType.Number || field.fieldType === FieldType.Decimal) {
                defaultValue = field.defaultValue ? parseFloat(field.defaultValue) : null;
            }

            formConfig[field.fieldKey] = new FormControl(defaultValue, validators);
        }

        this.form = this.fb.group(formConfig);
    }

    getOptions(field: FormField) {
        return this.formsService.parseOptions(field.optionsJson);
    }

    getFieldColSpan(field: FormField): number {
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            // Section always spans 4 columns
            if (field.fieldType === FieldType.Section || field.fieldType === FieldType.Label) {
                return 4;
            }
            return rules.colSpan || 4;
        } catch {
            return 4;
        }
    }

    getFieldHeight(field: FormField): string {
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            const height = rules.height || 'auto';
            const heightMap: { [key: string]: string } = {
                'auto': 'auto',
                'small': '60px',
                'medium': '100px',
                'large': '150px',
                'xlarge': '200px'
            };
            return heightMap[height] || 'auto';
        } catch {
            return 'auto';
        }
    }

    setRating(fieldKey: string, value: number) {
        this.form.get(fieldKey)?.setValue(value);
    }

    onFileChange(event: Event, fieldKey: string) {
        const input = event.target as HTMLInputElement;
        if (input.files?.[0]) {
            this.fileValues[fieldKey] = input.files[0];
        }
    }

    hasError(fieldKey: string): boolean {
        const control = this.form.get(fieldKey);
        return control ? control.invalid && control.touched : false;
    }

    submit() {
        this.form.markAllAsTouched();
        if (this.form.invalid) {
            return;
        }

        this.isSubmitting = true;
        const fieldValues: FieldValueRequest[] = [];

        for (const field of this.fields) {
            if (field.fieldType === FieldType.Section || field.fieldType === FieldType.Label) {
                continue;
            }

            const value = this.form.get(field.fieldKey)?.value;
            const fieldValue: FieldValueRequest = { formFieldId: field.id };

            switch (field.fieldType) {
                case FieldType.Number:
                case FieldType.Decimal:
                case FieldType.Rating:
                    fieldValue.numericValue = value != null ? parseFloat(value) : undefined;
                    break;
                case FieldType.Date:
                case FieldType.DateTime:
                    fieldValue.dateValue = value ? new Date(value) : undefined;
                    break;
                case FieldType.Checkbox:
                    fieldValue.booleanValue = value;
                    break;
                case FieldType.MultiSelect:
                    fieldValue.jsonValue = JSON.stringify(value);
                    break;
                default:
                    fieldValue.textValue = value?.toString() || '';
            }

            fieldValues.push(fieldValue);
        }

        this.formsService.submitResponse({
            formTemplateId: this.templateId,
            submittedByUserId: 'current-user-id', // Should come from auth
            fieldValues
        }).subscribe({
            next: () => {
                this.isSubmitting = false;
                alert('Đã gửi phản hồi thành công!');
                this.router.navigate(['/forms']);
            },
            error: () => {
                this.isSubmitting = false;
                alert('Có lỗi xảy ra, vui lòng thử lại.');
            }
        });
    }

    cancel() {
        this.router.navigate(['/forms']);
    }
}
