import { Component, OnInit, OnDestroy, Input, Output, EventEmitter, inject, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsService, FormTemplate, FormField, FieldType, FormFieldValue, FieldValueRequest, FieldValueDetailRequest, ConditionalLogic, Condition } from '../forms.service';
import { ConceptService } from '../services/concept.service';

@Component({
    selector: 'app-form-renderer',
    standalone: true,
    imports: [CommonModule, FormsModule, ReactiveFormsModule],
    templateUrl: './form-renderer.component.html',
    styleUrls: ['./form-renderer.component.scss']
})
export class FormRendererComponent implements OnInit, OnDestroy, AfterViewInit {
    private readonly formsService = inject(FormsService);
    private readonly conceptService = inject(ConceptService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);
    private readonly fb = inject(FormBuilder);

    FieldType = FieldType;
    templateId = '';
    responseId = '';  // For edit mode
    isEditMode = false;
    template: FormTemplate | null = null;
    fields: FormField[] = [];
    form: FormGroup = this.fb.group({});
    isSubmitting = false;
    fileValues: { [key: string]: File } = {};
    existingResponse: any = null;  // Store loaded response for edit
    visibleFields: Set<string> = new Set();

    private valueChangesSubscription: any;

    ngOnInit() {
        this.route.params.subscribe(params => {
            if (params['responseId']) {
                // Edit mode - load existing response
                this.responseId = params['responseId'];
                this.isEditMode = true;
                this.loadResponseForEdit();
            } else if (params['id']) {
                // New mode - load template only
                this.templateId = params['id'];
                this.loadTemplate();
            }
        });
    }

    ngOnDestroy() {
        if (this.valueChangesSubscription) {
            this.valueChangesSubscription.unsubscribe();
        }
    }

    loadResponseForEdit() {
        this.formsService.getResponseById(this.responseId).subscribe(response => {
            this.existingResponse = response;
            this.templateId = response.formTemplateId;
            this.loadTemplate(() => this.populateFormWithResponse(response));
        });
    }

    loadTemplate(onComplete?: () => void) {
        this.formsService.getTemplateById(this.templateId).subscribe(template => {
            this.template = template;
            // Normalize fieldType from string to enum value (API returns strings)
            this.fields = (template.fields || []).map(field => ({
                ...field,
                fieldType: this.normalizeFieldType(field.fieldType)
            })).sort((a, b) => a.displayOrder - b.displayOrder);
            this.buildForm();
            if (onComplete) onComplete();
        });
    }

    // Convert string field type from API to numeric enum value
    normalizeFieldType(type: FieldType | string): FieldType {
        // Handle 0 or invalid numbers immediately
        if ((type as any) === 0 || type === '0') {
            console.warn(`FieldType is 0 (Invalid), defaulting to Text`);
            return FieldType.Text;
        }

        if (typeof type === 'number') {
            return type;
        }
        // Handle numeric string
        const parsed = Number(type);
        if (!isNaN(parsed) && parsed !== 0) {
            return parsed as FieldType;
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
        const mapped = typeMap[type];
        if (mapped === undefined) {
            console.warn(`Unknown FieldType: ${type}, defaulting to Text`);
            return FieldType.Text;
        }
        return mapped;
    }

    buildForm() {
        const formConfig: { [key: string]: FormControl } = {};

        // Normalize fieldType - backend may return string name or number
        const fieldTypeMap: { [key: string]: number } = {
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

        for (const field of this.fields) {
            // Normalize fieldType
            if (typeof field.fieldType === 'number') {
                // Already a number, keep it
            } else if (typeof field.fieldType === 'string') {
                field.fieldType = fieldTypeMap[field.fieldType] ?? (Number(field.fieldType) || FieldType.Text);
            } else {
                field.fieldType = FieldType.Text;
            }

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
            } else if (field.fieldType === FieldType.Tags || field.fieldType === FieldType.MultiSelect) {
                try {
                    defaultValue = field.defaultValue ? JSON.parse(field.defaultValue) : [];
                    if (!Array.isArray(defaultValue)) {
                        // If parsing succeeded but not array (e.g. number), wrap it
                        defaultValue = [defaultValue];
                    }
                } catch {
                    // If parsing failed (plain string), wrap it
                    defaultValue = field.defaultValue ? [field.defaultValue] : [];
                }
            }

            formConfig[field.id] = new FormControl(defaultValue, validators);
            // console.log(`Built field: ${field.label} (${field.id}), Type: ${field.fieldType} (Normalized)`);
        }

        this.form = this.fb.group(formConfig);

        // Initial evaluation
        this.evaluateConditions();

        // Subscribe to changes
        this.valueChangesSubscription = this.form.valueChanges.subscribe(() => {
            this.evaluateConditions();
        });
    }

    ngAfterViewInit() {
        // Initialize tags fields content
        setTimeout(() => {
            this.initTagsFields();
        });
    }

    initTagsFields() {
        this.fields.forEach(field => {
            if (field.fieldType === FieldType.Tags && this.isFieldVisible(field.id)) {
                this.updateTagsEditorFromValue(field.id);
            }
        });
    }

    updateTagsEditorFromValue(fieldId: string) {
        const control = this.form.get(fieldId);
        if (!control) return;

        const editor = document.getElementById('mention-' + fieldId);
        if (!editor) return;

        const val = control.value;
        if (!val) {
            editor.innerHTML = '';
            return;
        }

        try {
            // Try parsing as JSON
            const data = typeof val === 'string' ? JSON.parse(val) : val;

            if (data && data.html) {
                // Format: { text, html, mentions }
                editor.innerHTML = data.html;
                if (data.mentions) {
                    this.mentionData[fieldId] = {
                        text: data.text,
                        mentions: data.mentions
                    };
                }
            } else if (Array.isArray(data)) {
                // Format: Array of tag values or objects
                // Reconstruct HTML with badges
                const field = this.fields.find(f => f.id === fieldId);
                const options = field ? this.getOptions(field) : [];

                let html = '';
                data.forEach((item: any) => {
                    const tagValue = typeof item === 'string' ? item : item.value;
                    const opt = options.find(o => o.value === tagValue) || options.find(o => (o as any).conceptId === tagValue);
                    const label = opt?.label || tagValue;
                    const conceptId = (opt as any)?.conceptId || (typeof item === 'object' ? item.conceptId : null);

                    // Create badge HTML similar to what insertMentionTag creates
                    html += `<span class="mention-tag" contenteditable="false" data-concept-id="${conceptId || ''}" data-value="${tagValue}">${label}</span> `;
                });

                editor.innerHTML = html;
            } else {
                // Plain text fallback - but avoid showing "undefined"
                editor.innerText = (val && val !== 'undefined') ? String(val) : '';
            }
        } catch {
            // Plain text fallback - but avoid showing "undefined"
            editor.innerText = (val && val !== 'undefined') ? String(val) : '';
        }
    }

    isFieldVisible(fieldId: string): boolean {
        return this.visibleFields.has(fieldId);
    }

    evaluateConditions() {
        const newVisibleFields = new Set<string>();
        const formValues = this.form.getRawValue(); // Use getRawValue to get values even from disabled fields

        // Iterate fields in order (assuming this.fields is sorted by displayOrder)
        for (const field of this.fields) {
            if (!field.conditionalLogicJson) {
                newVisibleFields.add(field.id);
                this.enableField(field.id);
                continue;
            }

            try {
                const logic: ConditionalLogic = JSON.parse(field.conditionalLogicJson);
                let isMatch = false;

                // Evaluate all conditions
                if (logic.conditions && Array.isArray(logic.conditions)) {
                    const results = logic.conditions.map(cond => {
                        const triggerValue = formValues[cond.fieldId];
                        return this.checkCondition(triggerValue, cond.operator, cond.value);
                    });

                    if (logic.logic === 'AND') {
                        isMatch = results.every(r => r);
                    } else { // OR
                        isMatch = results.some(r => r);
                    }
                }

                // Determine visibility based on Action + Match
                let shouldShow = true;
                if (logic.action === 'show') {
                    shouldShow = isMatch;
                } else { // hide
                    shouldShow = !isMatch;
                }

                if (shouldShow) {
                    newVisibleFields.add(field.id);
                    this.enableField(field.id);
                } else {
                    this.disableField(field.id);
                }

            } catch (err) {
                console.error('Error evaluating logic for field', field.id, err);
                newVisibleFields.add(field.id); // Fallback to visible
                this.enableField(field.id);
            }
        }

        // Check for visibility changes to re-init tags
        const prevSize = this.visibleFields.size;
        this.visibleFields = newVisibleFields;

        // If visibility changed, we might need to init tags for newly visible fields
        // Since DOM update happens after this, use setTimeout
        if (this.visibleFields.size !== prevSize) {
            setTimeout(() => this.initTagsFields());
        }
    }

    checkCondition(triggerValue: any, operator: string, targetValue: any): boolean {
        // Handle null/undefined
        if (triggerValue === null || triggerValue === undefined) triggerValue = '';
        if (targetValue === null || targetValue === undefined) targetValue = '';

        // Convert to strings for comparison usually, or basic types
        const tStr = String(triggerValue).toLowerCase();
        const vStr = String(targetValue).toLowerCase();

        switch (operator) {
            case 'eq': return tStr === vStr;
            case 'neq': return tStr !== vStr;
            case 'gt': return parseFloat(triggerValue) > parseFloat(targetValue);
            case 'lt': return parseFloat(triggerValue) < parseFloat(targetValue);
            case 'contains': return tStr.includes(vStr);
            default: return false;
        }
    }

    enableField(fieldId: string) {
        const control = this.form.get(fieldId);
        if (control && control.disabled) {
            control.enable({ emitEvent: false }); // Prevent infinite loop
        }
    }

    disableField(fieldId: string) {
        const control = this.form.get(fieldId);
        if (control && control.enabled) {
            control.disable({ emitEvent: false }); // Prevent infinite loop
            // Optional: Clear value when hidden? 
            // control.setValue(null, { emitEvent: false });
        }
    }

    // Populate form with existing response data for edit mode
    populateFormWithResponse(response: any) {
        if (!response.fieldValues) return;

        // Build a map of fieldId -> fieldValue
        const valueMap: { [key: string]: any } = {};
        response.fieldValues.forEach((fv: any) => {
            valueMap[fv.formFieldId] = fv;
        });

        // Timeout to ensure DOM is ready for Tags fields
        setTimeout(() => {
            for (const field of this.fields) {
                const fv = valueMap[field.id];
                if (!fv) continue;

                const control = this.form.get(field.id);
                if (!control) continue;

                switch (field.fieldType) {
                    case FieldType.Number:
                    case FieldType.Decimal:
                    case FieldType.Rating:
                        control.setValue(fv.numericValue);
                        break;

                    case FieldType.Date:
                        if (fv.dateValue) {
                            control.setValue(new Date(fv.dateValue).toISOString().split('T')[0]);
                        }
                        break;

                    case FieldType.DateTime:
                        if (fv.dateValue) {
                            control.setValue(new Date(fv.dateValue).toISOString().slice(0, 16));
                        }
                        break;

                    case FieldType.Checkbox:
                        // Check if this is a checkbox group or single boolean
                        if (fv.jsonValue) {
                            try {
                                const parsed = JSON.parse(fv.jsonValue);
                                if (Array.isArray(parsed)) {
                                    control.setValue(parsed);
                                    break;
                                }
                            } catch {
                                // Not JSON, fall through to boolean
                            }
                        }
                        control.setValue(fv.booleanValue);
                        break;

                    case FieldType.MultiSelect:
                        if (fv.jsonValue) {
                            try {
                                control.setValue(JSON.parse(fv.jsonValue));
                            } catch {
                                control.setValue([]);
                            }
                        }
                        break;

                    case FieldType.Tags:
                        // Set form control value from jsonValue
                        if (fv.jsonValue) {
                            control.setValue(fv.jsonValue);
                        } else if (fv.textValue) {
                            control.setValue(fv.textValue);
                        }
                        // Update the Tags editor display
                        this.updateTagsEditorFromValue(field.id);
                        break;

                    case FieldType.Radio:
                    case FieldType.Dropdown:
                        control.setValue(fv.textValue || '');
                        break;

                    default:
                        control.setValue(fv.textValue || '');
                }
            }
        }, 100);
    }

    // Populate Tags contenteditable editor with saved HTML
    populateTagsEditor(fieldId: string, jsonData: string, retries = 3) {
        if (!jsonData) return;

        const editor = document.getElementById('mention-' + fieldId);
        if (!editor) {
            // Retry if DOM not ready
            if (retries > 0) {
                setTimeout(() => this.populateTagsEditor(fieldId, jsonData, retries - 1), 200);
            }
            return;
        }

        try {
            const data = JSON.parse(jsonData);
            console.log('Populating Tags editor:', fieldId, data);
            if (data.html) {
                editor.innerHTML = data.html;
                // Apply inline styles to all inline-tag elements (for backward compatibility)
                this.applyInlineTagStyles(editor);
            } else if (data.text) {
                editor.textContent = data.text;
            }
        } catch {
            // Not JSON, treat as plain text
            editor.textContent = jsonData;
        }
    }

    // Apply inline styles to all inline-tag elements inside an editor
    private applyInlineTagStyles(container: HTMLElement) {
        const tags = container.querySelectorAll('.inline-tag');
        const inlineStyles = `
            background-color: #e7f3ff;
            color: #1877f2;
            padding: 0 2px;
            border-radius: 3px;
            font-weight: 500;
            cursor: default;
        `;
        tags.forEach(tag => {
            (tag as HTMLElement).style.cssText = inlineStyles;
            (tag as HTMLElement).contentEditable = 'false';
        });
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

    isCheckboxChecked(fieldId: string, optionValue: string): boolean {
        const control = this.form.get(fieldId);
        const value = control?.value;
        return Array.isArray(value) && value.includes(optionValue);
    }

    onCheckboxChange(fieldId: string, optionValue: string, event: Event) {
        const checkbox = event.target as HTMLInputElement;
        const control = this.form.get(fieldId);
        if (!control) return;

        let currentValue = control.value || [];
        if (!Array.isArray(currentValue)) {
            currentValue = [];
        }

        if (checkbox.checked) {
            // Add value if not already present
            if (!currentValue.includes(optionValue)) {
                control.setValue([...currentValue, optionValue]);
            }
        } else {
            // Remove value
            control.setValue(currentValue.filter((v: string) => v !== optionValue));
        }
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

            const value = this.form.get(field.id)?.value;
            const fieldValue: FieldValueRequest = { formFieldId: field.id };
            const details: FieldValueDetailRequest[] = [];

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
                    // Check if this checkbox has options (checkbox group) or is a single boolean
                    const checkboxOptions = this.getOptions(field);
                    if (checkboxOptions && checkboxOptions.length > 0) {
                        // Checkbox group with multiple options - treat like MultiSelect
                        fieldValue.jsonValue = JSON.stringify(value || []);
                        if (Array.isArray(value)) {
                            value.forEach((v: string) => {
                                const opt = checkboxOptions.find(o => o.value === v);
                                if (opt) {
                                    details.push({
                                        value: opt.value,
                                        label: opt.label,
                                        conceptId: (opt as any).conceptId
                                    });
                                } else {
                                    details.push({ value: v, label: v });
                                }
                            });
                        }
                    } else {
                        // Single boolean checkbox
                        fieldValue.booleanValue = value;
                        if (value === true && field.conceptId) {
                            details.push({
                                value: 'true',
                                label: field.label,
                                conceptId: field.conceptId
                            });
                        }
                    }
                    break;
                case FieldType.MultiSelect:
                    fieldValue.jsonValue = JSON.stringify(value || []);
                    if (Array.isArray(value)) {
                        const options = this.getOptions(field);
                        value.forEach((v: string) => {
                            const opt = options.find(o => o.value === v);
                            if (opt) {
                                details.push({
                                    value: opt.value,
                                    label: opt.label,
                                    conceptId: (opt as any).conceptId
                                });
                            } else {
                                details.push({ value: v, label: v });
                            }
                        });
                    }
                    break;
                case FieldType.Tags:
                    // Tags value is stored as JSON: {text, tagIds, mentions} or just an array
                    let parsed: any = null;
                    let tags: string[] = [];

                    try {
                        parsed = typeof value === 'string' ? JSON.parse(value) : value;

                        if (Array.isArray(parsed)) {
                            tags = parsed;
                            // If it's a simple array, we construct a simple JSON value
                            fieldValue.jsonValue = JSON.stringify(tags);
                        } else if (parsed && parsed.tagIds && Array.isArray(parsed.tagIds)) {
                            tags = parsed.tagIds;
                            // Critical: Save the FULL object to preserve text/html context (remark)
                            fieldValue.jsonValue = typeof value === 'string' ? value : JSON.stringify(parsed);

                            // Also save plain text to TextValue for searchability
                            if (parsed.text) {
                                fieldValue.textValue = parsed.text;
                            }
                        } else if (parsed) {
                            tags = [parsed];
                            fieldValue.jsonValue = JSON.stringify(tags);
                        }
                    } catch (e) {
                        console.error('Error parsing Tags value:', e);
                        fieldValue.jsonValue = JSON.stringify([]);
                    }

                    // Ensure tags is always an array before using forEach
                    if (!Array.isArray(tags)) {
                        tags = [];
                    }

                    const tagOptions = this.getOptions(field);

                    tags.forEach(t => {
                        // Look up by VALUE or CONCEPT ID (to handle legacy data)
                        const opt = tagOptions.find(o => o.value === t) || tagOptions.find(o => (o as any).conceptId === t);

                        if (opt) {
                            details.push({
                                value: opt.value,
                                label: opt.label,
                                conceptId: (opt as any).conceptId
                            });
                        } else {
                            // Fallback for values not in options (or if tag is raw text/guid)
                            details.push({ value: t, label: t });
                        }
                    });

                    break;
                case FieldType.Dropdown:
                case FieldType.Radio:
                    fieldValue.textValue = value?.toString() || '';
                    if (value) {
                        const options = this.getOptions(field);
                        const opt = options.find(o => o.value === value);
                        if (opt) {
                            details.push({
                                value: opt.value,
                                label: opt.label,
                                conceptId: (opt as any).conceptId
                            });
                        }
                    }
                    break;
                default:
                    fieldValue.textValue = value?.toString() || '';
            }

            if (details.length > 0) {
                fieldValue.details = details;
            }

            // Debug log each field
            console.log(`Field: ${field.label} (${field.id}), Type: ${field.fieldType}, ID: ${field.id}, Value:`, value, '=> fieldValue:', fieldValue);

            fieldValues.push(fieldValue);
        }

        const request = {
            formTemplateId: this.templateId,
            submittedByUserId: null, // Will be set from auth when available
            fieldValues
        };

        console.log('Submitting form:', JSON.stringify(request, null, 2));

        if (this.isEditMode) {
            // Update existing response
            this.formsService.updateResponse(this.responseId, request).subscribe({
                next: () => {
                    this.isSubmitting = false;
                    alert('Đã cập nhật phản hồi thành công!');
                    this.router.navigate(['/forms/responses', this.responseId]);
                },
                error: () => {
                    this.isSubmitting = false;
                    alert('Có lỗi xảy ra, vui lòng thử lại.');
                }
            });
        } else {
            // Create new response
            this.formsService.submitResponse(request).subscribe({
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
    }

    cancel() {
        this.router.navigate(['/forms']);
    }

    // Mention-style Tags input
    mentionTexts: { [key: string]: string } = {};
    mentionData: { [key: string]: { text: string; mentions: { text: string; conceptId: string; code: string; start: number; end: number }[] } } = {};
    showMentionDropdown = false;
    activeMentionFieldKey = '';
    mentionSearchResults: { id: string; code: string; name: string }[] = [];
    mentionDropdownPosition = { top: 0, left: 0 };
    private mentionSearchStart = -1;
    private conceptsCache: { [id: string]: { code: string; name: string } } = {};

    // Initialize mention data when building form
    initMentionField(fieldKey: string) {
        if (!this.mentionTexts[fieldKey]) {
            this.mentionTexts[fieldKey] = '';
        }
        if (!this.mentionData[fieldKey]) {
            this.mentionData[fieldKey] = { text: '', mentions: [] };
        }
    }

    onMentionInput(field: FormField, event: Event) {
        const textarea = event.target as HTMLTextAreaElement;
        const text = textarea.value;
        const cursorPos = textarea.selectionStart;

        this.mentionTexts[field.id] = text;

        // Detect @ trigger
        const beforeCursor = text.substring(0, cursorPos);
        const atIndex = beforeCursor.lastIndexOf('@');

        if (atIndex !== -1) {
            const afterAt = beforeCursor.substring(atIndex + 1);
            // Check if there's no space between @ and cursor (still typing mention)
            if (!afterAt.includes(' ') && afterAt.length <= 30) {
                this.mentionSearchStart = atIndex;
                this.activeMentionFieldKey = field.id;
                this.searchConcepts(field, afterAt);
                this.showMentionDropdown = true;
                return;
            }
        }

        this.showMentionDropdown = false;
        this.updateMentionFormValue(field.id);
    }

    searchConcepts(field: FormField, query: string) {
        // First try to get options from field (linked concepts)
        const options = this.getOptions(field);

        if (options.length > 0) {
            // Search within predefined options
            if (!query) {
                this.mentionSearchResults = options.slice(0, 10).map(opt => ({
                    id: (opt as any).conceptId || opt.value,
                    code: opt.value,
                    name: opt.label
                }));
            } else {
                const q = query.toLowerCase();
                this.mentionSearchResults = options.filter(opt =>
                    opt.label.toLowerCase().includes(q) ||
                    opt.value.toLowerCase().includes(q)
                ).slice(0, 10).map(opt => ({
                    id: (opt as any).conceptId || opt.value,
                    code: opt.value,
                    name: opt.label
                }));
            }
        } else {
            // No predefined options - search ALL concepts from API
            this.conceptService.searchConcepts(query || '', undefined, 10).subscribe({
                next: (result) => {
                    this.mentionSearchResults = result.concepts.map(c => ({
                        id: c.id,
                        code: c.code,
                        name: c.display  // Concept uses 'display' not 'name'
                    }));
                },
                error: () => {
                    this.mentionSearchResults = [];
                }
            });
        }
    }

    selectMention(fieldKey: string, concept: { id: string; code: string; name: string }) {
        const text = this.mentionTexts[fieldKey] || '';
        const beforeMention = text.substring(0, this.mentionSearchStart);
        const afterAtPos = text.indexOf(' ', this.mentionSearchStart);
        const afterMention = afterAtPos !== -1 ? text.substring(afterAtPos) : '';

        // Insert the concept name
        const newText = beforeMention + concept.name + ' ' + afterMention.trimStart();
        this.mentionTexts[fieldKey] = newText;

        // Track the mention
        if (!this.mentionData[fieldKey]) {
            this.mentionData[fieldKey] = { text: '', mentions: [] };
        }
        this.mentionData[fieldKey].mentions.push({
            text: concept.name,
            conceptId: concept.id,
            code: concept.code,
            start: this.mentionSearchStart,
            end: this.mentionSearchStart + concept.name.length
        });

        // Cache concept for display
        // Cache concept for display
        this.conceptsCache[concept.id] = { code: concept.code, name: concept.name };

        this.showMentionDropdown = false;
        this.updateMentionFormValue(fieldKey);

        // Focus back on textarea
        setTimeout(() => this.focusMentionInput(fieldKey), 0);
    }

    // Contenteditable methods for inline tag display
    onContentEditableInput(field: FormField, event: Event) {
        const editor = event.target as HTMLDivElement;
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) return;

        // Get text content before cursor
        const range = selection.getRangeAt(0);
        const preCaretRange = range.cloneRange();
        preCaretRange.selectNodeContents(editor);
        preCaretRange.setEnd(range.endContainer, range.endOffset);
        const textBeforeCursor = preCaretRange.toString();

        // Detect @ trigger
        const atIndex = textBeforeCursor.lastIndexOf('@');
        if (atIndex !== -1) {
            const afterAt = textBeforeCursor.substring(atIndex + 1);
            // Check if still typing mention (no space after @)
            if (!afterAt.includes(' ') && afterAt.length <= 30) {
                this.mentionSearchStart = atIndex;
                this.activeMentionFieldKey = field.id;
                this.searchConcepts(field, afterAt);
                this.showMentionDropdown = true;
                return;
            }
        }

        this.showMentionDropdown = false;
        this.updateContentEditableFormValue(field.id, editor);
    }

    onContentEditableKeydown(field: FormField, event: KeyboardEvent) {
        if (event.key === 'Escape') {
            this.showMentionDropdown = false;
        }

        // Tab or Enter to select first concept from dropdown
        if ((event.key === 'Tab' || event.key === 'Enter') && this.showMentionDropdown && this.mentionSearchResults.length > 0) {
            event.preventDefault();
            this.insertMentionTag(field.id, this.mentionSearchResults[0]);
        }
    }

    insertMentionTag(fieldKey: string, concept: { id: string; code: string; name: string }) {
        const editor = document.getElementById('mention-' + fieldKey) as HTMLDivElement;
        if (!editor) return;

        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) return;

        // Delete the @query text
        const range = selection.getRangeAt(0);

        // Find and remove @query from before cursor
        const textNode = range.startContainer;
        if (textNode.nodeType === Node.TEXT_NODE) {
            const text = textNode.textContent || '';
            const atIndex = text.lastIndexOf('@');
            if (atIndex !== -1) {
                // Remove @query
                textNode.textContent = text.substring(0, atIndex);
            }
        }

        // Create tag span element with inline styles (Angular ViewEncapsulation blocks component styles)
        const tagSpan = document.createElement('span');
        tagSpan.className = 'inline-tag';
        tagSpan.contentEditable = 'false';
        tagSpan.setAttribute('data-concept-id', concept.id);
        tagSpan.setAttribute('data-concept-code', concept.code);
        tagSpan.textContent = concept.name;
        // Apply inline styles - Facebook style: black text, light blue background
        tagSpan.style.cssText = `
            background-color: #e7f3ff;
            color: #1877f2;
            padding: 0 2px;
            border-radius: 3px;
            font-weight: 500;
            cursor: default;
        `;

        // Insert the tag
        const newRange = document.createRange();
        newRange.selectNodeContents(editor);
        newRange.collapse(false);
        newRange.insertNode(tagSpan);

        // Add space after tag and position cursor
        const space = document.createTextNode(' ');
        tagSpan.after(space);

        // Move cursor after space
        const cursorRange = document.createRange();
        cursorRange.setStartAfter(space);
        cursorRange.collapse(true);
        selection.removeAllRanges();
        selection.addRange(cursorRange);

        // Track mention
        if (!this.mentionData[fieldKey]) {
            this.mentionData[fieldKey] = { text: '', mentions: [] };
        }
        this.mentionData[fieldKey].mentions.push({
            text: concept.name,
            conceptId: concept.id,
            code: concept.code,
            start: 0,
            end: 0
        });

        // Cache concept
        this.conceptsCache[concept.id] = { code: concept.code, name: concept.name };

        this.showMentionDropdown = false;
        this.updateContentEditableFormValue(fieldKey, editor);

        // Keep focus on editor
        editor.focus();
    }

    updateContentEditableFormValue(fieldKey: string, editor: HTMLDivElement) {
        const control = this.form.get(fieldKey);
        if (!control) return;

        // Extract text and tag IDs
        const tagIds: string[] = [];
        const tagElements = editor.querySelectorAll('.inline-tag');
        tagElements.forEach(el => {
            const id = el.getAttribute('data-concept-id');
            if (id) tagIds.push(id);
        });

        const textContent = editor.textContent || '';

        control.setValue(JSON.stringify({
            text: textContent,
            tagIds: [...new Set(tagIds)],
            html: editor.innerHTML
        }));
    }

    onMentionKeydown(fieldKey: string, event: KeyboardEvent) {
        if (event.key === 'Escape') {
            this.showMentionDropdown = false;
        }
    }

    onMentionBlur(fieldKey: string) {
        setTimeout(() => {
            this.showMentionDropdown = false;
        }, 200);
    }

    focusMentionInput(fieldKey: string) {
        const textarea = document.getElementById('mention-' + fieldKey) as HTMLTextAreaElement;
        if (textarea) {
            textarea.focus();
        }
    }

    getMentionDisplayHTML(fieldKey: string): string {
        const text = this.mentionTexts[fieldKey] || '';
        if (!text) return '';

        const data = this.mentionData[fieldKey];
        if (!data || data.mentions.length === 0) {
            return this.escapeHtml(text);
        }

        // Highlight mentions in text
        let html = this.escapeHtml(text);
        // Sort mentions by position descending to replace from end
        const sortedMentions = [...data.mentions].sort((a, b) => b.start - a.start);

        for (const mention of sortedMentions) {
            const escapedText = this.escapeHtml(mention.text);
            const badge = `<span class="mention-badge" data-concept-id="${mention.conceptId}">${escapedText}</span>`;
            // Find and replace the mention text
            html = html.replace(new RegExp(this.escapeRegExp(escapedText), 'g'), badge);
        }

        return html;
    }

    private escapeHtml(text: string): string {
        return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    private escapeRegExp(str: string): string {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    getMentionTagIds(fieldKey: string): string[] {
        const data = this.mentionData[fieldKey];
        if (!data) return [];

        // Get the field to access its options
        const field = this.fields.find(f => f.id === fieldKey);
        if (!field) return [];

        const options = this.getOptions(field);

        console.log('getMentionTagIds - field:', field.label);
        console.log('getMentionTagIds - options:', options);
        console.log('getMentionTagIds - mentions:', data.mentions);

        // Map concept IDs to option values
        const optionValues: string[] = [];
        data.mentions.forEach(m => {
            console.log(`Looking for conceptId ${m.conceptId} in options`);
            const opt = options.find(o => (o as any).conceptId === m.conceptId);
            console.log('Found option:', opt);
            if (opt) {
                optionValues.push(opt.value);
            }
        });

        console.log('getMentionTagIds - returning:', optionValues);
        return [...new Set(optionValues)]; // Remove duplicates
    }

    getConceptName(conceptId: string): string {
        const cached = this.conceptsCache[conceptId];
        return cached ? cached.name : conceptId;
    }

    updateMentionFormValue(fieldKey: string) {
        const control = this.form.get(fieldKey);
        if (!control) return;

        const data = this.mentionData[fieldKey] || { text: '', mentions: [] };
        data.text = this.mentionTexts[fieldKey] || '';

        // Store as JSON with text and tagIds
        control.setValue(JSON.stringify({
            text: data.text,
            tagIds: this.getMentionTagIds(fieldKey),
            mentions: data.mentions
        }));
    }

    // Keep old tag helpers for backward compatibility (can be removed later)
    tagSearchQueries: { [key: string]: string } = {};
    tagSearchResults: { [key: string]: { value: string; label: string }[] } = {};
    activeTagFieldKey: string = '';
    private tagFieldOptions: { [key: string]: { value: string; label: string }[] } = {};

    hasTagSearchResults(fieldKey: string): boolean {
        return !!(this.tagSearchResults[fieldKey] && this.tagSearchResults[fieldKey].length > 0);
    }

    getTagSearchResults(fieldKey: string): { value: string; label: string }[] {
        return this.tagSearchResults[fieldKey] || [];
    }

    hasTagSearchQuery(fieldKey: string): boolean {
        return !!(this.tagSearchQueries[fieldKey] && this.tagSearchQueries[fieldKey].length > 0);
    }

    getTagSearchQuery(fieldKey: string): string {
        return this.tagSearchQueries[fieldKey] || '';
    }

    isTagSelected(fieldKey: string, value: string): boolean {
        const currentValue = this.form.get(fieldKey)?.value;
        if (!currentValue) return false;
        try {
            const selected = typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue;
            return Array.isArray(selected) && selected.includes(value);
        } catch {
            return false;
        }
    }

    getSelectedTags(fieldKey: string): { value: string; label: string; isCustom?: boolean; conceptId?: string }[] {
        const currentValue = this.form.get(fieldKey)?.value;
        if (!currentValue) return [];

        try {
            const selectedValues = typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue;
            if (!Array.isArray(selectedValues)) return [];

            // Find the field to get options
            const field = this.fields.find(f => f.fieldKey === fieldKey);
            if (!field) return selectedValues.map((v: string) => ({ value: v, label: v, isCustom: true }));

            const options = this.getOptions(field);
            return selectedValues.map((v: string) => {
                const opt = options.find(o => o.value === v);
                if (opt) {
                    // Predefined tag with concept ID for analytics
                    return {
                        value: opt.value,
                        label: opt.label,
                        isCustom: false,
                        conceptId: (opt as any).conceptId // Include concept ID if available
                    };
                }
                // Custom tag (user typed)
                return { value: v, label: v, isCustom: true };
            });
        } catch {
            return [];
        }
    }

    searchTagOptions(field: FormField) {
        const query = (this.tagSearchQueries[field.id] || '').toLowerCase();

        // Get all options for this field
        if (!this.tagFieldOptions[field.id]) {
            this.tagFieldOptions[field.id] = this.getOptions(field);
        }

        const allOptions = this.tagFieldOptions[field.id];

        if (!query || query.length < 1) {
            // Show all options if no query
            this.tagSearchResults[field.id] = allOptions.slice(0, 10);
            return;
        }

        // Filter by query - handle @ prefix
        const searchTerm = query.startsWith('@') ? query.substring(1) : query;
        this.tagSearchResults[field.id] = allOptions.filter(opt =>
            opt.label.toLowerCase().includes(searchTerm) ||
            opt.value.toLowerCase().includes(searchTerm)
        ).slice(0, 10);
    }

    selectTag(fieldKey: string, opt: { value: string; label: string }) {
        const control = this.form.get(fieldKey);
        if (!control) return;

        let current: string[] = [];
        try {
            const currentValue = control.value;
            current = typeof currentValue === 'string' ? JSON.parse(currentValue) : (currentValue || []);
        } catch {
            current = [];
        }

        if (!current.includes(opt.value)) {
            current.push(opt.value);
            control.setValue(current);
        }

        // Clear search
        this.tagSearchQueries[fieldKey] = '';
        this.tagSearchResults[fieldKey] = [];
    }

    removeTag(fieldKey: string, value: string) {
        const control = this.form.get(fieldKey);
        if (!control) return;

        let current: string[] = [];
        try {
            const currentValue = control.value;
            current = typeof currentValue === 'string' ? JSON.parse(currentValue) : (currentValue || []);
        } catch {
            current = [];
        }

        const index = current.indexOf(value);
        if (index >= 0) {
            current.splice(index, 1);
            control.setValue(current);
        }
    }

    // Custom tag methods
    addCustomTag(fieldKey: string, event: Event) {
        event.preventDefault();
        const query = this.tagSearchQueries[fieldKey]?.trim();
        if (!query) return;

        const control = this.form.get(fieldKey);
        if (!control) return;

        let current: string[] = [];
        try {
            const currentValue = control.value;
            current = typeof currentValue === 'string' ? JSON.parse(currentValue) : (currentValue || []);
        } catch {
            current = [];
        }

        if (!current.includes(query)) {
            current.push(query);
            control.setValue(current);
        }

        this.tagSearchQueries[fieldKey] = '';
        this.tagSearchResults[fieldKey] = [];
    }

    addCustomTagFromInput(fieldKey: string) {
        const query = this.tagSearchQueries[fieldKey]?.trim();
        if (!query) return;

        const control = this.form.get(fieldKey);
        if (!control) return;

        let current: string[] = [];
        try {
            const currentValue = control.value;
            current = typeof currentValue === 'string' ? JSON.parse(currentValue) : (currentValue || []);
        } catch {
            current = [];
        }

        if (!current.includes(query)) {
            current.push(query);
            control.setValue(current);
        }

        this.tagSearchQueries[fieldKey] = '';
        this.tagSearchResults[fieldKey] = [];
    }

    onTagInputBlur(fieldKey: string) {
        // Delay to allow click events to fire first
        setTimeout(() => {
            this.activeTagFieldKey = '';
        }, 200);
    }

    isExactMatch(fieldKey: string, query: string): boolean {
        if (!query) return false;
        const options = this.tagFieldOptions[fieldKey] || [];
        return options.some(opt =>
            opt.value.toLowerCase() === query.toLowerCase() ||
            opt.label.toLowerCase() === query.toLowerCase()
        );
    }

}
