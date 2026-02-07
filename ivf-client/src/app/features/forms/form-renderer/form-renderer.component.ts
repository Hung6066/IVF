import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsService, FormTemplate, FormField, FieldType, FormFieldValue, FieldValueRequest } from '../forms.service';

@Component({
    selector: 'app-form-renderer',
    standalone: true,
    imports: [CommonModule, FormsModule, ReactiveFormsModule],
    template: `
        <div class="form-renderer-container">
            @if (template) {
                <div class="form-card">
                    <div class="form-header">
                        <h1>{{ template.name }}</h1>
                        @if (template.description) {
                            <p class="description">{{ template.description }}</p>
                        }
                    </div>

                    <form [formGroup]="form" (ngSubmit)="submit()">
                        <div class="form-fields-grid">
                            @for (field of fields; track field.id) {
                                <div 
                                    class="field-wrapper" 
                                    [class.has-error]="hasError(field.fieldKey)"
                                    [style.grid-column]="'span ' + getFieldColSpan(field)">
                                    @if (field.fieldType !== FieldType.Section && field.fieldType !== FieldType.Label) {
                                        <label class="field-label">
                                            {{ field.label }}
                                            @if (field.isRequired) {
                                                <span class="required">*</span>
                                            }
                                        </label>
                                    }

                                    @switch (field.fieldType) {
                                        @case (FieldType.Text) {
                                            <input 
                                                type="text" 
                                                [formControlName]="field.fieldKey"
                                                [placeholder]="field.placeholder || ''"
                                                [style.height]="getFieldHeight(field)"
                                                class="form-input">
                                        }
                                        @case (FieldType.TextArea) {
                                            <textarea 
                                                [formControlName]="field.fieldKey"
                                                [placeholder]="field.placeholder || ''"
                                                [style.height]="getFieldHeight(field)"
                                                [style.min-height]="getFieldHeight(field)"
                                                class="form-textarea"></textarea>
                                        }
                                        @case (FieldType.Number) {
                                            <input 
                                                type="number" 
                                                [formControlName]="field.fieldKey"
                                                [placeholder]="field.placeholder || ''"
                                                class="form-input">
                                        }
                                        @case (FieldType.Decimal) {
                                            <input 
                                                type="number" 
                                                step="0.01"
                                                [formControlName]="field.fieldKey"
                                                [placeholder]="field.placeholder || ''"
                                                class="form-input">
                                        }
                                        @case (FieldType.Date) {
                                            <input 
                                                type="date" 
                                                [formControlName]="field.fieldKey"
                                                class="form-input">
                                        }
                                        @case (FieldType.DateTime) {
                                            <input 
                                                type="datetime-local" 
                                                [formControlName]="field.fieldKey"
                                                class="form-input">
                                        }
                                        @case (FieldType.Time) {
                                            <input 
                                                type="time" 
                                                [formControlName]="field.fieldKey"
                                                class="form-input">
                                        }
                                        @case (FieldType.Dropdown) {
                                            <select [formControlName]="field.fieldKey" class="form-select">
                                                <option value="">-- Chọn --</option>
                                                @for (opt of getOptions(field); track opt.value) {
                                                    <option [value]="opt.value">{{ opt.label }}</option>
                                                }
                                            </select>
                                        }
                                        @case (FieldType.MultiSelect) {
                                            <select [formControlName]="field.fieldKey" class="form-select" multiple>
                                                @for (opt of getOptions(field); track opt.value) {
                                                    <option [value]="opt.value">{{ opt.label }}</option>
                                                }
                                            </select>
                                        }
                                        @case (FieldType.Radio) {
                                            <div class="radio-group">
                                                @for (opt of getOptions(field); track opt.value) {
                                                    <label class="radio-option">
                                                        <input 
                                                            type="radio" 
                                                            [formControlName]="field.fieldKey"
                                                            [value]="opt.value">
                                                        <span>{{ opt.label }}</span>
                                                    </label>
                                                }
                                            </div>
                                        }
                                        @case (FieldType.Checkbox) {
                                            <label class="checkbox-option">
                                                <input 
                                                    type="checkbox" 
                                                    [formControlName]="field.fieldKey">
                                                <span>{{ field.placeholder || field.label }}</span>
                                            </label>
                                        }
                                        @case (FieldType.Rating) {
                                            <div class="rating-input">
                                                @for (star of [1,2,3,4,5]; track star) {
                                                    <button 
                                                        type="button"
                                                        class="star-btn"
                                                        [class.active]="form.get(field.fieldKey)?.value >= star"
                                                        (click)="setRating(field.fieldKey, star)">
                                                        ⭐
                                                    </button>
                                                }
                                            </div>
                                        }
                                        @case (FieldType.Section) {
                                            <div class="section-header">
                                                <h3>{{ field.label }}</h3>
                                                @if (field.helpText) {
                                                    <p>{{ field.helpText }}</p>
                                                }
                                            </div>
                                        }
                                        @case (FieldType.Label) {
                                            <div class="label-field">
                                                <p>{{ field.label }}</p>
                                            </div>
                                        }
                                        @case (FieldType.FileUpload) {
                                            <input 
                                                type="file" 
                                                (change)="onFileChange($event, field.fieldKey)"
                                                class="form-file">
                                        }
                                    }

                                    @if (field.helpText && field.fieldType !== FieldType.Section) {
                                        <small class="help-text">{{ field.helpText }}</small>
                                    }

                                    @if (hasError(field.fieldKey)) {
                                        <small class="error-text">Trường này là bắt buộc</small>
                                    }
                                </div>
                            }
                        </div>

                        <div class="form-actions">
                            <button type="button" class="btn btn-secondary" (click)="cancel()">Hủy</button>
                            <button type="submit" class="btn btn-primary" [disabled]="isSubmitting">
                                @if (isSubmitting) {
                                    <span class="spinner"></span>
                                }
                                Gửi phản hồi
                            </button>
                        </div>
                    </form>
                </div>
            } @else {
                <div class="loading">
                    <div class="spinner-large"></div>
                    <p>Đang tải biểu mẫu...</p>
                </div>
            }
        </div>
    `,
    styles: [`
        .form-renderer-container {
            padding: 24px;
            max-width: 800px;
            margin: 0 auto;
            min-height: 100vh;
            background: #f1f5f9;
        }

        .form-card {
            background: white;
            border-radius: 16px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            overflow: hidden;
        }

        .form-header {
            padding: 32px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .form-header h1 {
            margin: 0 0 8px;
            font-size: 28px;
        }

        .form-header .description {
            margin: 0;
            opacity: 0.9;
        }

        .form-fields-grid {
            padding: 32px;
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 20px;
            align-items: start;
        }

        .field-wrapper {
            display: flex;
            flex-direction: column;
        }

        .section-header {
            grid-column: span 4;
        }

        .field-label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: #374151;
        }

        .required {
            color: #ef4444;
        }

        .form-input,
        .form-textarea,
        .form-select {
            width: 100%;
            padding: 12px 16px;
            border: 1px solid #d1d5db;
            border-radius: 8px;
            font-size: 15px;
            transition: all 0.2s;
        }

        .form-input:focus,
        .form-textarea:focus,
        .form-select:focus {
            outline: none;
            border-color: #667eea;
            box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
        }

        .has-error .form-input,
        .has-error .form-textarea,
        .has-error .form-select {
            border-color: #ef4444;
        }

        .help-text {
            display: block;
            margin-top: 6px;
            color: #64748b;
            font-size: 13px;
        }

        .error-text {
            display: block;
            margin-top: 6px;
            color: #ef4444;
            font-size: 13px;
        }

        .radio-group,
        .checkbox-group {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .radio-option,
        .checkbox-option {
            display: flex;
            align-items: center;
            gap: 10px;
            cursor: pointer;
        }

        .radio-option input,
        .checkbox-option input {
            width: 18px;
            height: 18px;
        }

        .rating-input {
            display: flex;
            gap: 8px;
        }

        .star-btn {
            background: none;
            border: none;
            font-size: 28px;
            cursor: pointer;
            opacity: 0.3;
            transition: all 0.2s;
        }

        .star-btn.active {
            opacity: 1;
            transform: scale(1.1);
        }

        .section-header {
            padding: 16px 0;
            border-bottom: 2px solid #e2e8f0;
            margin-bottom: 8px;
        }

        .section-header h3 {
            margin: 0 0 4px;
            color: #1e293b;
        }

        .section-header p {
            margin: 0;
            color: #64748b;
            font-size: 14px;
        }

        .label-field p {
            margin: 0;
            color: #64748b;
            font-style: italic;
        }

        .form-actions {
            display: flex;
            justify-content: flex-end;
            gap: 16px;
            padding: 24px 32px;
            border-top: 1px solid #e2e8f0;
            background: #f8fafc;
        }

        .btn {
            padding: 12px 32px;
            border: none;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            transition: all 0.2s;
        }

        .btn-primary {
            background: #667eea;
            color: white;
        }

        .btn-primary:hover {
            background: #5a67d8;
        }

        .btn-primary:disabled {
            background: #a5b4fc;
            cursor: not-allowed;
        }

        .btn-secondary {
            background: #e2e8f0;
            color: #475569;
        }

        .loading {
            text-align: center;
            padding: 80px 20px;
        }

        .spinner-large {
            width: 48px;
            height: 48px;
            border: 4px solid #e2e8f0;
            border-top-color: #667eea;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin: 0 auto 16px;
        }

        .spinner {
            width: 16px;
            height: 16px;
            border: 2px solid rgba(255,255,255,0.3);
            border-top-color: white;
            border-radius: 50%;
            animation: spin 0.8s linear infinite;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        /* Responsive */
        @media (max-width: 768px) {
            .form-renderer-container {
                padding: 12px;
            }

            .form-fields-grid {
                grid-template-columns: 1fr;
                padding: 20px;
                gap: 16px;
            }

            .field-wrapper {
                grid-column: span 1 !important;
            }

            .section-header {
                grid-column: span 1;
            }

            .form-header {
                padding: 20px;
            }

            .form-header h1 {
                font-size: 22px;
            }

            .form-actions {
                flex-direction: column;
                padding: 16px 20px;
            }

            .btn {
                width: 100%;
            }
        }
    `]
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
