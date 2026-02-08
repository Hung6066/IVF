import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsService, FormCategory, FormTemplate, FormField, FieldType, FieldTypeLabels, CreateFieldRequest } from '../forms.service';
import { ConceptPickerComponent } from '../concept-picker/concept-picker.component';
import { ConceptService, Concept } from '../services/concept.service';

@Component({
    selector: 'app-form-builder',
    standalone: true,
    imports: [CommonModule, FormsModule, DragDropModule, ConceptPickerComponent],
    templateUrl: './form-builder.component.html',
    styleUrls: ['./form-builder.component.scss']
})
export class FormBuilderComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);

    FieldType = FieldType;
    templateId: string | null = null;
    template: FormTemplate | null = null;
    categories: FormCategory[] = [];
    fields: FormField[] = [];
    selectedField: FormField | null = null;

    formName = '';
    formDescription = '';
    selectedCategoryId = '';
    selectedFieldColSpan = '4';
    selectedFieldHeight = 'auto';

    fieldTypes = [
        { type: FieldType.Text, icon: '📝', label: 'Văn bản' },
        { type: FieldType.TextArea, icon: '📄', label: 'Văn bản dài' },
        { type: FieldType.Number, icon: '🔢', label: 'Số' },
        { type: FieldType.Date, icon: '📅', label: 'Ngày' },
        { type: FieldType.Dropdown, icon: '📋', label: 'Dropdown' },
        { type: FieldType.Radio, icon: '⭕', label: 'Radio' },
        { type: FieldType.Checkbox, icon: '☑️', label: 'Checkbox' },
        { type: FieldType.Tags, icon: '🏷️', label: 'Tags' },
        { type: FieldType.Rating, icon: '⭐', label: 'Đánh giá' },
        { type: FieldType.Section, icon: '➖', label: 'Phân đoạn' },
        { type: FieldType.FileUpload, icon: '📎', label: 'Tải file' }
    ];

    ngOnInit() {
        this.formsService.getCategories().subscribe(cats => {
            this.categories = cats;
            if (cats.length > 0 && !this.selectedCategoryId) {
                this.selectedCategoryId = cats[0].id;
            }
        });

        this.route.params.subscribe(params => {
            if (params['id']) {
                this.templateId = params['id'];
                this.loadTemplate();
            }
        });
    }

    loadTemplate() {
        if (!this.templateId) return;
        this.formsService.getTemplateById(this.templateId).subscribe(template => {
            this.template = template;
            this.formName = template.name;
            this.formDescription = template.description || '';
            this.selectedCategoryId = template.categoryId;
            this.fields = template.fields || [];

            // Load linked concepts for fields that have conceptId
            this.loadLinkedConcepts();
        });
    }

    private loadLinkedConcepts() {
        this.linkedConcepts.clear();

        // Get all fields that have conceptId
        const fieldsWithConcepts = this.fields.filter(f => f.conceptId);

        // Fetch each linked concept
        fieldsWithConcepts.forEach(field => {
            if (field.conceptId) {
                this.conceptService.getConceptById(field.conceptId).subscribe({
                    next: (concept) => {
                        this.linkedConcepts.set(field.id, concept);
                    },
                    error: (err) => {
                        console.error(`Failed to load concept for field ${field.id}:`, err);
                    }
                });
            }
        });
    }

    goBack() {
        this.router.navigate(['/forms']);
    }

    save() {
        if (!this.templateId) {
            // Create new template
            this.formsService.createTemplate({
                categoryId: this.selectedCategoryId,
                name: this.formName || 'Biểu mẫu mới',
                description: this.formDescription,
                createdByUserId: 'current-user-id', // Should come from auth service
                fields: this.fields.map((f, i) => ({
                    fieldKey: f.fieldKey || `field_${i}`,
                    label: f.label,
                    fieldType: f.fieldType,
                    displayOrder: i,
                    isRequired: f.isRequired,
                    placeholder: f.placeholder,
                    optionsJson: f.optionsJson,
                    validationRulesJson: f.validationRulesJson,
                    defaultValue: f.defaultValue,
                    helpText: f.helpText
                }))
            }).subscribe(template => {
                this.templateId = template.id;
                this.template = template;
                alert('Đã lưu biểu mẫu!');
            });
        } else {
            // Update existing
            this.formsService.updateTemplate(this.templateId, {
                name: this.formName,
                description: this.formDescription,
                categoryId: this.selectedCategoryId
            }).subscribe(() => {
                alert('Đã cập nhật biểu mẫu!');
            });
        }
    }

    saveFormSettings() {
        if (this.templateId) {
            this.formsService.updateTemplate(this.templateId, {
                name: this.formName,
                description: this.formDescription,
                categoryId: this.selectedCategoryId
            }).subscribe();
        }
    }

    publish() {
        if (this.templateId) {
            this.formsService.publishTemplate(this.templateId).subscribe(template => {
                this.template = template;
                alert('Đã xuất bản biểu mẫu!');
            });
        }
    }

    preview() {
        if (this.templateId) {
            this.router.navigate(['/forms/fill', this.templateId]);
        }
    }

    addField(type: FieldType) {
        const newField: FormField = {
            id: '',
            fieldKey: `field_${this.fields.length + 1}`,
            label: this.getFieldTypeLabel(type),
            fieldType: type,
            displayOrder: this.fields.length,
            isRequired: false,
            placeholder: '',
            optionsJson: this.hasOptions(type) ? JSON.stringify([
                { value: 'opt1', label: 'Lựa chọn 1' },
                { value: 'opt2', label: 'Lựa chọn 2' }
            ]) : undefined
        };

        this.fields.push(newField);

        if (this.templateId) {
            this.formsService.addField(this.templateId, {
                fieldKey: newField.fieldKey,
                label: newField.label,
                fieldType: newField.fieldType,
                displayOrder: newField.displayOrder,
                isRequired: newField.isRequired,
                placeholder: newField.placeholder,
                optionsJson: newField.optionsJson
            }).subscribe(field => {
                const index = this.fields.findIndex(f => f.fieldKey === newField.fieldKey);
                if (index >= 0) this.fields[index] = field;
            });
        }
    }

    selectField(field: FormField) {
        this.selectedField = field;
        console.log('selectField:', field.label, 'fieldType:', field.fieldType, 'hasOptions:', this.hasOptions(field.fieldType));
        // Get colSpan and height from field's validation rules
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            this.selectedFieldColSpan = rules.colSpan?.toString() || '4';
            this.selectedFieldHeight = rules.height || 'auto';
        } catch {
            this.selectedFieldColSpan = '4';
            this.selectedFieldHeight = 'auto';
        }
        // Load options for structured editing
        if (this.hasOptions(field.fieldType)) {
            this.loadOptionsForField(field);
            console.log('editableOptions loaded:', this.editableOptions);
        }
    }

    updateField() {
        if (this.selectedField?.id && this.templateId) {
            this.formsService.updateField(this.selectedField.id, this.selectedField).subscribe();
        }
    }

    setColSpan(span: string) {
        this.selectedFieldColSpan = span;
        this.updateFieldLayout();
    }

    updateFieldLayout() {
        if (this.selectedField) {
            // Store colSpan and height in validationRulesJson
            try {
                const rules = this.selectedField.validationRulesJson
                    ? JSON.parse(this.selectedField.validationRulesJson)
                    : {};
                rules.colSpan = parseInt(this.selectedFieldColSpan);
                rules.height = this.selectedFieldHeight;
                this.selectedField.validationRulesJson = JSON.stringify(rules);
                this.updateField();
            } catch {
                // Ignore parse errors
            }
        }
    }

    getFieldColSpan(field: FormField): number {
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            return rules.colSpan || 4; // Default to full width (4 columns)
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

    deleteField(field: FormField, event: Event) {
        event.stopPropagation();
        if (confirm('Xóa trường này?')) {
            this.fields = this.fields.filter(f => f !== field);
            if (field.id) {
                this.formsService.deleteField(field.id).subscribe();
            }
            if (this.selectedField === field) {
                this.selectedField = null;
            }
        }
    }

    onDropField(event: CdkDragDrop<FormField[]>) {
        moveItemInArray(this.fields, event.previousIndex, event.currentIndex);
        if (this.templateId) {
            const fieldIds = this.fields.filter(f => f.id).map(f => f.id);
            this.formsService.reorderFields(this.templateId, fieldIds).subscribe();
        }
    }

    onDragStart(event: DragEvent, type: FieldType) {
        event.dataTransfer?.setData('fieldType', type.toString());
    }

    onDragOver(event: DragEvent) {
        event.preventDefault();
    }

    onDrop(event: DragEvent) {
        event.preventDefault();
        const typeStr = event.dataTransfer?.getData('fieldType');
        if (typeStr) {
            this.addField(parseInt(typeStr) as FieldType);
        }
    }

    getFieldTypeLabel(type: FieldType): string {
        return FieldTypeLabels[type] || 'Trường mới';
    }

    hasOptions(type: FieldType | string): boolean {
        // Handle both numeric enum values and string names from API
        const optionTypes = [FieldType.Dropdown, FieldType.MultiSelect, FieldType.Radio, FieldType.Checkbox, FieldType.Tags];
        const optionTypeNames = ['Dropdown', 'MultiSelect', 'Radio', 'Checkbox', 'Tags'];

        if (typeof type === 'string') {
            return optionTypeNames.includes(type);
        }
        return optionTypes.includes(type);
    }

    getOptions(field: FormField): { value: string; label: string }[] {
        return this.formsService.parseOptions(field.optionsJson);
    }

    getOptionsText(field: FormField): string {
        const options = this.getOptions(field);
        return options.map(o => `${o.value}|${o.label}`).join('\n');
    }

    setOptionsFromText(event: Event, field: FormField) {
        const text = (event.target as HTMLTextAreaElement).value;
        const options = text.split('\n').filter(line => line.trim()).map(line => {
            const parts = line.split('|');
            return {
                value: parts[0]?.trim() || parts[0],
                label: parts[1]?.trim() || parts[0]
            };
        });
        field.optionsJson = JSON.stringify(options);
        this.updateField();
    }

    // ===== Concept Picker Integration =====
    showConceptPicker = false;
    selectedFieldForConcept?: FormField;
    linkedConcepts = new Map<string, Concept>();
    private conceptService = inject(ConceptService);

    // Inline concept search
    conceptSearchTerm = '';
    conceptSearchResults: Concept[] = [];
    showConceptDropdown = false;
    isSearchingConcepts = false;
    private searchTimeout: any;

    openConceptPicker(field: FormField) {
        this.selectedFieldForConcept = field;
        this.showConceptPicker = true;
    }

    onConceptLinked(concept: Concept) {
        // Route to option linking if we're linking an option
        if (this.selectedOptionIndex >= 0) {
            this.onConceptSelectedForOption(concept);
            return;
        }

        if (this.selectedFieldForConcept) {
            // Store concept for display
            this.linkedConcepts.set(this.selectedFieldForConcept.id || '', concept);

            // Reload form to get updated field data
            if (this.templateId) {
                this.loadTemplate();
            }
        }
        this.showConceptPicker = false;
    }

    getLinkedConcept(fieldId: string): Concept | undefined {
        return this.linkedConcepts.get(fieldId);
    }

    // Inline concept search methods
    searchConcepts() {
        clearTimeout(this.searchTimeout);

        if (!this.conceptSearchTerm || this.conceptSearchTerm.length < 2) {
            this.conceptSearchResults = [];
            return;
        }

        this.isSearchingConcepts = true;
        this.searchTimeout = setTimeout(() => {
            this.conceptService.searchConcepts(this.conceptSearchTerm).subscribe({
                next: (result) => {
                    this.conceptSearchResults = result.concepts;
                    this.isSearchingConcepts = false;
                },
                error: () => {
                    this.conceptSearchResults = [];
                    this.isSearchingConcepts = false;
                }
            });
        }, 300);
    }

    selectConcept(concept: Concept) {
        if (this.selectedField?.id) {
            // Link the concept to the field via API
            this.conceptService.linkFieldToConcept(this.selectedField.id, concept.id).subscribe({
                next: () => {
                    this.linkedConcepts.set(this.selectedField!.id || '', concept);

                    // Set field label to concept display name
                    this.selectedField!.label = concept.display;
                    this.updateField();

                    this.conceptSearchTerm = '';
                    this.conceptSearchResults = [];
                    this.showConceptDropdown = false;
                },
                error: (err) => {
                    console.error('Failed to link concept:', err);
                }
            });
        }
    }

    unlinkConcept(field: FormField) {
        if (field.id) {
            this.linkedConcepts.delete(field.id);
            // Note: API call to unlink could be added here if needed
        }
    }

    // ===== Structured Options Editor =====
    editableOptions: EditableOption[] = [];
    selectedOptionIndex: number = -1;

    // Called when a field is selected to load its options
    loadOptionsForField(field: FormField) {
        const opts = this.formsService.parseOptions(field.optionsJson);
        this.editableOptions = opts.map(o => ({
            value: o.value,
            label: o.label,
            conceptId: undefined,
            conceptCode: undefined,
            conceptDisplay: undefined
        }));
    }

    addOption() {
        const newOpt: EditableOption = {
            value: `opt${this.editableOptions.length + 1}`,
            label: `Lựa chọn ${this.editableOptions.length + 1}`,
            conceptId: undefined,
            conceptCode: undefined,
            conceptDisplay: undefined
        };
        this.editableOptions.push(newOpt);
        this.saveOptions();
    }

    removeOption(index: number) {
        this.editableOptions.splice(index, 1);
        this.saveOptions();
    }

    saveOptions() {
        if (this.selectedField) {
            const options = this.editableOptions.map(o => ({
                value: o.value,
                label: o.label
            }));
            this.selectedField.optionsJson = JSON.stringify(options);
            this.updateField();
        }
    }

    openConceptPickerForOption(index: number) {
        this.selectedOptionIndex = index;
        this.showConceptPicker = true;
    }

    onConceptSelectedForOption(concept: Concept) {
        if (this.selectedOptionIndex >= 0 && this.selectedOptionIndex < this.editableOptions.length) {
            const opt = this.editableOptions[this.selectedOptionIndex];
            opt.conceptId = concept.id;
            opt.conceptCode = concept.code;
            opt.conceptDisplay = concept.display;

            // TODO: API call to link option to concept when options are stored in DB
            // this.conceptService.linkOptionToConcept(opt.dbId, concept.id).subscribe();
        }
        this.showConceptPicker = false;
        this.selectedOptionIndex = -1;
    }

    unlinkOptionConcept(index: number) {
        if (index >= 0 && index < this.editableOptions.length) {
            const opt = this.editableOptions[index];
            opt.conceptId = undefined;
            opt.conceptCode = undefined;
            opt.conceptDisplay = undefined;
        }
    }

    // ===== Inline Concept Search for Options =====
    optionConceptSearch: { [key: number]: string } = {};
    optionConceptResults: { [key: number]: Concept[] } = {};
    activeOptionSearchIndex: number = -1;
    private optionSearchTimeout: any;

    searchConceptsForOption(index: number) {
        clearTimeout(this.optionSearchTimeout);
        const query = this.optionConceptSearch[index];

        if (!query || query.length < 2) {
            this.optionConceptResults[index] = [];
            return;
        }

        this.optionSearchTimeout = setTimeout(() => {
            this.conceptService.searchConcepts(query, undefined, 10).subscribe({
                next: (result) => {
                    this.optionConceptResults[index] = result.concepts;
                },
                error: (err) => {
                    console.error('Concept search error:', err);
                    this.optionConceptResults[index] = [];
                }
            });
        }, 300);
    }

    selectConceptForOption(index: number, concept: Concept) {
        if (index >= 0 && index < this.editableOptions.length) {
            const opt = this.editableOptions[index];
            opt.conceptId = concept.id;
            opt.conceptCode = concept.code;
            opt.conceptDisplay = concept.display;
            // Auto-fill label with concept display name
            opt.label = concept.display;
            // Save the options
            this.saveOptions();
        }
        // Clear search
        this.optionConceptSearch[index] = '';
        this.optionConceptResults[index] = [];
        this.activeOptionSearchIndex = -1;
    }
}

// Interface for editable options with concept linking
interface EditableOption {
    value: string;
    label: string;
    conceptId?: string;
    conceptCode?: string;
    conceptDisplay?: string;
}


