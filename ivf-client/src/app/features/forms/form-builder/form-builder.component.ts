import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  FormsService,
  FormCategory,
  FormTemplate,
  FormField,
  FieldType,
  FieldTypeLabels,
} from '../forms.service';
import { ConceptPickerComponent } from '../concept-picker/concept-picker.component';
import { LinkedFieldConfigComponent } from '../linked-field-config/linked-field-config.component';
import { ConceptService, Concept } from '../services/concept.service';
import { FieldPaletteComponent } from './field-palette/field-palette.component';
import { FieldPropertiesPanelComponent } from './field-properties-panel/field-properties-panel.component';
import { FieldPreviewComponent } from './field-preview/field-preview.component';
import { normalizeFieldType, getFieldColSpan, hasOptions } from '../shared/form-field.utils';

@Component({
  selector: 'app-form-builder',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DragDropModule,
    ConceptPickerComponent,
    LinkedFieldConfigComponent,
    FieldPaletteComponent,
    FieldPropertiesPanelComponent,
    FieldPreviewComponent,
  ],
  templateUrl: './form-builder.component.html',
  styleUrls: ['./form-builder.component.scss'],
})
export class FormBuilderComponent implements OnInit {
  private readonly formsService = inject(FormsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly conceptService = inject(ConceptService);

  FieldType = FieldType;
  templateId: string | null = null;
  template: FormTemplate | null = null;
  categories: FormCategory[] = [];
  fields: FormField[] = [];
  selectedField: FormField | null = null;

  formName = '';
  formDescription = '';
  selectedCategoryId = '';

  ngOnInit() {
    this.formsService.getCategories().subscribe((cats) => {
      this.categories = cats;
      if (cats.length > 0 && !this.selectedCategoryId) {
        this.selectedCategoryId = cats[0].id;
      }
    });

    this.route.params.subscribe((params) => {
      if (params['id']) {
        this.templateId = params['id'];
        this.loadTemplate();
      }
    });
  }

  loadTemplate() {
    if (!this.templateId) return;
    this.formsService.getTemplateById(this.templateId).subscribe((template) => {
      this.template = template;
      this.formName = template.name;
      this.formDescription = template.description || '';
      this.selectedCategoryId = template.categoryId;

      this.fields = (template.fields || []).map((f) => ({
        ...f,
        fieldType: normalizeFieldType(f.fieldType),
      }));

      this.loadLinkedConcepts();
    });
  }

  private loadLinkedConcepts() {
    this.linkedConcepts.clear();

    // Get all fields that have conceptId
    const fieldsWithConcepts = this.fields.filter((f) => f.conceptId);

    // Fetch each linked concept
    fieldsWithConcepts.forEach((field) => {
      if (field.conceptId) {
        this.conceptService.getConceptById(field.conceptId).subscribe({
          next: (concept) => {
            this.linkedConcepts.set(field.id, concept);
          },
          error: (err) => {
            console.error(`Failed to load concept for field ${field.id}:`, err);
          },
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
      this.formsService
        .createTemplate({
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
            layoutJson: f.layoutJson,
            defaultValue: f.defaultValue,
            helpText: f.helpText,
          })),
        })
        .subscribe((template) => {
          this.templateId = template.id;
          this.template = template;
          alert('Đã lưu biểu mẫu!');
        });
    } else {
      // Update existing
      this.formsService
        .updateTemplate(this.templateId, {
          name: this.formName,
          description: this.formDescription,
          categoryId: this.selectedCategoryId,
        })
        .subscribe(() => {
          alert('Đã cập nhật biểu mẫu!');
        });
    }
  }

  saveFormSettings() {
    if (this.templateId) {
      this.formsService
        .updateTemplate(this.templateId, {
          name: this.formName,
          description: this.formDescription,
          categoryId: this.selectedCategoryId,
        })
        .subscribe();
    }
  }

  publish() {
    if (this.templateId) {
      this.formsService.publishTemplate(this.templateId).subscribe((template) => {
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

  addField(type: FieldType | string) {
    const numericType = normalizeFieldType(type);
    const newField: FormField = {
      id: '',
      fieldKey: `field_${this.fields.length + 1}`,
      label: FieldTypeLabels[numericType] || 'Trường mới',
      fieldType: numericType,
      displayOrder: this.fields.length,
      isRequired: false,
      placeholder: '',
      optionsJson: hasOptions(numericType)
        ? JSON.stringify([
            { value: 'opt1', label: 'Lựa chọn 1' },
            { value: 'opt2', label: 'Lựa chọn 2' },
          ])
        : numericType === FieldType.Slider
          ? JSON.stringify({ min: 0, max: 100, step: 1, unit: '' })
          : numericType === FieldType.Calculated
            ? JSON.stringify({ formula: '', decimalPlaces: 2, unit: '' })
            : numericType === FieldType.Hidden
              ? JSON.stringify({ token: '' })
              : numericType === FieldType.Repeater
                ? JSON.stringify({ minRows: 1, maxRows: 10, fields: [
                    { key: 'col1', label: 'Cột 1', type: 'text' },
                    { key: 'col2', label: 'Cột 2', type: 'text' },
                  ]})
                : undefined,
      conditionalLogicJson: undefined, // Ensure no logic is set by default
    };

    this.fields.push(newField);

    if (this.templateId) {
      this.formsService
        .addField(this.templateId, {
          fieldKey: newField.fieldKey,
          label: newField.label,
          fieldType: newField.fieldType,
          displayOrder: newField.displayOrder,
          isRequired: newField.isRequired,
          placeholder: newField.placeholder,
          optionsJson: newField.optionsJson,
          conditionalLogicJson: newField.conditionalLogicJson,
        })
        .subscribe((field) => {
          const index = this.fields.findIndex((f) => f.fieldKey === newField.fieldKey);
          if (index >= 0) this.fields[index] = field;
        });
    }
  }

  selectField(field: FormField) {
    this.selectedField = field;
  }

  updateField(field?: FormField) {
    const target = field || this.selectedField;
    if (target?.id) {
      this.formsService
        .updateField(target.id, {
          label: target.label,
          isRequired: target.isRequired,
          placeholder: target.placeholder,
          optionsJson: target.optionsJson,
          validationRulesJson: target.validationRulesJson,
          layoutJson: target.layoutJson,
          conditionalLogicJson: target.conditionalLogicJson,
          displayOrder: target.displayOrder,
        })
        .subscribe();
    }
  }

  getFieldColSpan(field: FormField): number {
    return getFieldColSpan(field);
  }

  deleteField(field: FormField) {
    if (confirm('Xóa trường này?')) {
      this.fields = this.fields.filter((f) => f !== field);
      if (field.id) {
        this.formsService.deleteField(field.id).subscribe();
      }
      if (this.selectedField === field) {
        this.selectedField = null;
      }
    }
  }

  cloneField(field: FormField) {
    const fieldIndex = this.fields.indexOf(field);
    const cloned: FormField = {
      id: '',
      fieldKey: `${field.fieldKey}_copy_${Date.now()}`,
      label: `${field.label} (bản sao)`,
      fieldType: field.fieldType,
      displayOrder: fieldIndex + 1,
      isRequired: field.isRequired,
      placeholder: field.placeholder,
      optionsJson: field.optionsJson,
      validationRulesJson: field.validationRulesJson,
      layoutJson: field.layoutJson,
      defaultValue: field.defaultValue,
      helpText: field.helpText,
      conditionalLogicJson: field.conditionalLogicJson,
    };

    this.fields.splice(fieldIndex + 1, 0, cloned);

    if (this.templateId) {
      this.formsService
        .addField(this.templateId, {
          fieldKey: cloned.fieldKey,
          label: cloned.label,
          fieldType: cloned.fieldType,
          displayOrder: cloned.displayOrder,
          isRequired: cloned.isRequired,
          placeholder: cloned.placeholder,
          optionsJson: cloned.optionsJson,
          validationRulesJson: cloned.validationRulesJson,
          layoutJson: cloned.layoutJson,
          defaultValue: cloned.defaultValue,
          helpText: cloned.helpText,
          conditionalLogicJson: cloned.conditionalLogicJson,
        })
        .subscribe((savedField) => {
          const idx = this.fields.indexOf(cloned);
          if (idx >= 0) this.fields[idx] = savedField;
        });
    }
  }

  onDropField(event: CdkDragDrop<FormField[]>) {
    moveItemInArray(this.fields, event.previousIndex, event.currentIndex);
    if (this.templateId) {
      const fieldIds = this.fields.filter((f) => f.id).map((f) => f.id);
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

  // ===== Concept Picker Integration =====
  showConceptPicker = false;
  selectedFieldForConcept?: FormField;
  linkedConcepts = new Map<string, Concept>();

  showLinkedFieldConfig = false;

  openLinkedFieldConfig() {
    this.showLinkedFieldConfig = true;
  }

  onConceptLinked(event: { fieldId: string; concept: Concept }) {
    this.linkedConcepts.set(event.fieldId, event.concept);
  }

  onConceptUnlinked(fieldId: string) {
    this.linkedConcepts.delete(fieldId);
  }

  onFieldPropertiesUpdated(field: FormField) {
    this.updateField(field);
  }

  deselectField() {
    this.selectedField = null;
  }
}
