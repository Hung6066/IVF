import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsService, FormTemplate, FormField, FieldType, FormFieldValue, FieldValueRequest } from '../forms.service';
import { ConceptService } from '../services/concept.service';

@Component({
    selector: 'app-form-renderer',
    standalone: true,
    imports: [CommonModule, FormsModule, ReactiveFormsModule],
    templateUrl: './form-renderer.component.html',
    styleUrls: ['./form-renderer.component.scss']
})
export class FormRendererComponent implements OnInit {
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
        return typeMap[type] ?? FieldType.Text;
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

            formConfig[field.id] = new FormControl(defaultValue, validators);
        }

        this.form = this.fb.group(formConfig);
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
                        // Set form control value
                        control.setValue(fv.jsonValue || fv.textValue || '');
                        // Populate contenteditable editor with HTML
                        this.populateTagsEditor(field.id, fv.jsonValue || fv.textValue);
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
                    fieldValue.jsonValue = JSON.stringify(value || []);
                    break;
                case FieldType.Tags:
                    // Tags value is already a JSON string from updateContentEditableFormValue
                    fieldValue.jsonValue = typeof value === 'string' ? value : JSON.stringify(value || {});
                    break;
                default:
                    fieldValue.textValue = value?.toString() || '';
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
        return [...new Set(data.mentions.map(m => m.conceptId))];
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
