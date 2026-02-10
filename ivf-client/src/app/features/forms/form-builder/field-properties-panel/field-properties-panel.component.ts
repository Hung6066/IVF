import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  FormField,
  FieldType,
  FormsService,
  ConditionalLogic,
  Condition,
  ValidationRule,
} from '../../forms.service';
import { ConceptService, Concept } from '../../services/concept.service';
import { hasOptions } from '../../shared/form-field.utils';

export interface EditableOption {
  value: string;
  label: string;
  conceptId?: string;
  conceptCode?: string;
  conceptDisplay?: string;
}

@Component({
  selector: 'app-field-properties-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './field-properties-panel.component.html',
  styleUrls: ['./field-properties-panel.component.scss'],
})
export class FieldPropertiesPanelComponent implements OnChanges {
  private readonly formsService = inject(FormsService);
  private readonly conceptService = inject(ConceptService);

  @Input() field: FormField | null = null;
  @Input() fields: FormField[] = [];
  @Input() linkedConcepts = new Map<string, Concept>();
  @Output() fieldUpdated = new EventEmitter<FormField>();
  @Output() closed = new EventEmitter<void>();
  @Output() conceptLinked = new EventEmitter<{ fieldId: string; concept: Concept }>();
  @Output() conceptUnlinked = new EventEmitter<string>();

  FieldType = FieldType;
  activeTab: 'basic' | 'validation' | 'layout' = 'basic';
  selectedFieldColSpan = '4';
  selectedFieldHeight = 'auto';

  // Options editor
  editableOptions: EditableOption[] = [];

  // Address sub-fields
  editableSubFields: {
    key: string;
    label: string;
    type: string;
    required: boolean;
    width: number;
  }[] = [];

  // Concept search
  conceptSearchTerm = '';
  conceptSearchResults: Concept[] = [];
  showConceptDropdown = false;
  isSearchingConcepts = false;
  private searchTimeout: any;

  // Option concept search
  optionConceptSearch: { [key: number]: string } = {};
  optionConceptResults: { [key: number]: Concept[] } = {};
  activeOptionSearchIndex = -1;
  private optionSearchTimeout: any;

  // Validation
  currentValidationRules: ValidationRule[] = [];

  // Conditional Logic
  showLogicEditor = false;
  currentLogic: ConditionalLogic = { action: 'show', logic: 'AND', conditions: [] };

  ngOnChanges(changes: SimpleChanges) {
    if (changes['field'] && this.field) {
      this.activeTab = 'basic';
      this.showLogicEditor = false;

      // Load colSpan and height
      try {
        const json = this.field.layoutJson || this.field.validationRulesJson;
        const data = json ? JSON.parse(json) : {};
        this.selectedFieldColSpan = data.colSpan?.toString() || '4';
        this.selectedFieldHeight = data.height || 'auto';
      } catch {
        this.selectedFieldColSpan = '4';
        this.selectedFieldHeight = 'auto';
      }

      // Load options
      if (hasOptions(this.field.fieldType)) {
        this.loadOptionsForField(this.field);
      }

      // Load sub-fields
      this.loadSubFieldsForField(this.field);

      // Load validation rules
      this.loadValidationRules();
    }
  }

  hasOptions = hasOptions;

  getLinkedConcept(fieldId: string): Concept | undefined {
    return this.linkedConcepts.get(fieldId);
  }

  close() {
    this.closed.emit();
  }

  emitUpdate() {
    if (this.field) {
      this.fieldUpdated.emit(this.field);
    }
  }

  // ===== Concept Search =====
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
        },
      });
    }, 300);
  }

  selectConcept(concept: Concept) {
    if (this.field?.id) {
      this.conceptService.linkFieldToConcept(this.field.id, concept.id).subscribe({
        next: () => {
          this.conceptLinked.emit({ fieldId: this.field!.id, concept });
          this.field!.label = concept.display;
          this.emitUpdate();
          this.conceptSearchTerm = '';
          this.conceptSearchResults = [];
          this.showConceptDropdown = false;
        },
        error: (err) => console.error('Failed to link concept:', err),
      });
    }
  }

  unlinkConcept() {
    if (this.field?.id) {
      this.conceptUnlinked.emit(this.field.id);
    }
  }

  // ===== Options Editor =====
  loadOptionsForField(field: FormField) {
    const opts = this.formsService.parseOptions(field.optionsJson);
    this.editableOptions = opts.map((o) => ({
      value: o.value,
      label: o.label,
      conceptId: undefined,
      conceptCode: undefined,
      conceptDisplay: undefined,
    }));
  }

  addOption() {
    this.editableOptions.push({
      value: `opt${this.editableOptions.length + 1}`,
      label: `Lựa chọn ${this.editableOptions.length + 1}`,
    });
    this.saveOptions();
  }

  removeOption(index: number) {
    this.editableOptions.splice(index, 1);
    this.saveOptions();
  }

  saveOptions() {
    if (this.field) {
      const options = this.editableOptions.map((o) => ({
        value: o.value,
        label: o.label,
        conceptId: o.conceptId,
        conceptCode: o.conceptCode,
        conceptDisplay: o.conceptDisplay,
      }));
      this.field.optionsJson = JSON.stringify(options);
      this.emitUpdate();
    }
  }

  unlinkOptionConcept(index: number) {
    if (index >= 0 && index < this.editableOptions.length) {
      const opt = this.editableOptions[index];
      opt.conceptId = undefined;
      opt.conceptCode = undefined;
      opt.conceptDisplay = undefined;
    }
  }

  searchConceptsForOption(index: number) {
    clearTimeout(this.optionSearchTimeout);
    const query = this.optionConceptSearch[index];
    if (!query || query.length < 2) {
      this.optionConceptResults[index] = [];
      return;
    }
    this.optionSearchTimeout = setTimeout(() => {
      this.conceptService.searchConcepts(query, undefined, 10).subscribe({
        next: (result) => (this.optionConceptResults[index] = result.concepts),
        error: () => (this.optionConceptResults[index] = []),
      });
    }, 300);
  }

  selectConceptForOption(index: number, concept: Concept) {
    if (index >= 0 && index < this.editableOptions.length) {
      const opt = this.editableOptions[index];
      opt.conceptId = concept.id;
      opt.conceptCode = concept.code;
      opt.conceptDisplay = concept.display;
      opt.label = concept.display;
      this.saveOptions();
    }
    this.optionConceptSearch[index] = '';
    this.optionConceptResults[index] = [];
    this.activeOptionSearchIndex = -1;
  }

  // ===== Address Sub-Fields =====
  loadSubFieldsForField(field: FormField) {
    if (field.fieldType !== FieldType.Address) {
      this.editableSubFields = [];
      return;
    }
    try {
      const subs = field.optionsJson ? JSON.parse(field.optionsJson) : [];
      this.editableSubFields = Array.isArray(subs) && subs.length > 0 && subs[0].key ? subs : [];
    } catch {
      this.editableSubFields = [];
    }
  }

  addSubField() {
    this.editableSubFields.push({
      key: `field${this.editableSubFields.length + 1}`,
      label: '',
      type: 'text',
      required: false,
      width: 50,
    });
    this.saveSubFields();
  }

  removeSubField(index: number) {
    this.editableSubFields.splice(index, 1);
    this.saveSubFields();
  }

  saveSubFields() {
    if (this.field) {
      this.field.optionsJson = JSON.stringify(this.editableSubFields);
      this.emitUpdate();
    }
  }

  loadDefaultAddressFields() {
    this.editableSubFields = [
      { key: 'street', label: 'Đường / Số nhà', type: 'text', required: true, width: 100 },
      { key: 'ward', label: 'Phường/Xã', type: 'text', required: false, width: 50 },
      { key: 'district', label: 'Quận/Huyện', type: 'text', required: false, width: 50 },
      { key: 'province', label: 'Tỉnh/Thành phố', type: 'text', required: true, width: 50 },
      { key: 'country', label: 'Quốc gia', type: 'text', required: false, width: 50 },
    ];
    this.saveSubFields();
  }

  // ===== Slider/Calculated/Repeater Config =====
  getSliderConfig(key: string): any {
    if (!this.field) return '';
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      return config[key] ?? '';
    } catch {
      return '';
    }
  }

  setSliderConfig(key: string, value: any) {
    if (!this.field) return;
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      config[key] = key === 'unit' ? value : Number(value);
      this.field.optionsJson = JSON.stringify(config);
      this.emitUpdate();
    } catch {}
  }

  getCalcConfig(key: string): any {
    if (!this.field) return '';
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      return config[key] ?? '';
    } catch {
      return '';
    }
  }

  setCalcConfig(key: string, value: any) {
    if (!this.field) return;
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      config[key] = key === 'formula' || key === 'unit' ? value : Number(value);
      this.field.optionsJson = JSON.stringify(config);
      this.emitUpdate();
    } catch {}
  }

  getRepeaterConfig(key: string): any {
    if (!this.field) return '';
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      return config[key] ?? '';
    } catch {
      return '';
    }
  }

  setRepeaterConfig(key: string, value: any) {
    if (!this.field) return;
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      config[key] = Number(value);
      this.field.optionsJson = JSON.stringify(config);
      this.emitUpdate();
    } catch {}
  }

  getRepeaterFields(): { key: string; label: string; type: string }[] {
    if (!this.field) return [];
    try {
      const config = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : {};
      return config.fields || [];
    } catch {
      return [];
    }
  }

  addRepeaterField() {
    if (!this.field) return;
    try {
      const config = this.field.optionsJson
        ? JSON.parse(this.field.optionsJson)
        : { minRows: 1, maxRows: 10, fields: [] };
      if (!config.fields) config.fields = [];
      config.fields.push({
        key: `col${config.fields.length + 1}`,
        label: `Cột ${config.fields.length + 1}`,
        type: 'text',
      });
      this.field.optionsJson = JSON.stringify(config);
      this.emitUpdate();
    } catch {}
  }

  removeRepeaterField(index: number) {
    if (!this.field) return;
    try {
      const config = JSON.parse(this.field.optionsJson || '{}');
      if (config.fields) {
        config.fields.splice(index, 1);
        this.field.optionsJson = JSON.stringify(config);
        this.emitUpdate();
      }
    } catch {}
  }

  updateRepeaterField(index: number, key: string, value: string) {
    if (!this.field) return;
    try {
      const config = JSON.parse(this.field.optionsJson || '{}');
      if (config.fields && config.fields[index]) {
        config.fields[index][key] = value;
        this.field.optionsJson = JSON.stringify(config);
        this.emitUpdate();
      }
    } catch {}
  }

  // ===== Layout =====
  setColSpan(span: string) {
    this.selectedFieldColSpan = span;
    this.updateFieldLayout();
  }

  updateFieldLayout() {
    if (this.field) {
      const layout = {
        colSpan: parseInt(this.selectedFieldColSpan),
        height: this.selectedFieldHeight,
      };
      this.field.layoutJson = JSON.stringify(layout);
      this.emitUpdate();
    }
  }

  // ===== Validation =====
  loadValidationRules() {
    if (!this.field) {
      this.currentValidationRules = [];
      return;
    }
    this.currentValidationRules = this.formsService.parseValidationRules(
      this.field.validationRulesJson,
    );
  }

  getValidationRule(type: string): ValidationRule | undefined {
    return this.currentValidationRules.find((r) => r.type === type);
  }

  getValidationValue(type: string): any {
    return this.getValidationRule(type)?.value ?? '';
  }

  setValidationRule(type: string, value: any) {
    const existing = this.currentValidationRules.findIndex((r) => r.type === type);
    if (value === '' || value === null || value === undefined || value === false) {
      if (existing >= 0) this.currentValidationRules.splice(existing, 1);
    } else {
      const rule: ValidationRule = { type: type as any, value };
      if (existing >= 0) {
        this.currentValidationRules[existing] = rule;
      } else {
        this.currentValidationRules.push(rule);
      }
    }
    this.saveValidationRules();
  }

  saveValidationRules() {
    if (!this.field) return;
    try {
      const existing = this.field.validationRulesJson
        ? JSON.parse(this.field.validationRulesJson)
        : {};
      const layoutData: any = {};
      if (existing.colSpan) layoutData.colSpan = existing.colSpan;
      if (existing.height) layoutData.height = existing.height;
      const merged = { ...layoutData, rules: this.currentValidationRules };
      this.field.validationRulesJson = JSON.stringify(merged);
      this.emitUpdate();
    } catch {
      this.field.validationRulesJson = JSON.stringify({ rules: this.currentValidationRules });
      this.emitUpdate();
    }
  }

  isValidationApplicable(validationType: string): boolean {
    if (!this.field) return false;
    const ft = this.field.fieldType;
    switch (validationType) {
      case 'minLength':
      case 'maxLength':
        return ft === FieldType.Text || ft === FieldType.TextArea;
      case 'min':
      case 'max':
        return (
          ft === FieldType.Number ||
          ft === FieldType.Decimal ||
          ft === FieldType.Rating ||
          ft === FieldType.Slider
        );
      case 'pattern':
        return ft === FieldType.Text || ft === FieldType.TextArea || ft === FieldType.RichText;
      case 'email':
        return ft === FieldType.Text;
      default:
        return false;
    }
  }

  // ===== Conditional Logic =====
  toggleLogicEditor() {
    if (!this.field) return;
    this.showLogicEditor = !this.showLogicEditor;
    if (this.showLogicEditor) {
      try {
        this.currentLogic = this.field.conditionalLogicJson
          ? JSON.parse(this.field.conditionalLogicJson)
          : { action: 'show', logic: 'AND', conditions: [] };
      } catch {
        this.currentLogic = { action: 'show', logic: 'AND', conditions: [] };
      }
    }
  }

  addCondition() {
    this.currentLogic.conditions.push({ fieldId: '', operator: 'eq', value: '' });
  }

  removeCondition(index: number) {
    this.currentLogic.conditions.splice(index, 1);
    this.saveLogic();
  }

  saveLogic() {
    if (!this.field) return;
    const validConditions = this.currentLogic.conditions.filter((c) => c.fieldId);
    if (validConditions.length > 0) {
      this.field.conditionalLogicJson = JSON.stringify({
        ...this.currentLogic,
        conditions: validConditions,
      });
    } else {
      this.field.conditionalLogicJson = undefined;
    }
    this.emitUpdate();
  }

  getAvailableTriggerFields(): FormField[] {
    if (!this.field) return [];
    const currentIndex = this.fields.indexOf(this.field);
    if (currentIndex <= 0) return [];
    return this.fields
      .slice(0, currentIndex)
      .filter((f) => f.fieldType !== FieldType.Section && f.fieldType !== FieldType.Label);
  }

  getOptionsForCondition(fieldId: string): { value: any; label: string }[] | null {
    if (!fieldId) return null;
    const field = this.fields.find((f) => f.id === fieldId);
    if (!field) return null;
    const type = Number(field.fieldType);
    if (type === FieldType.Checkbox) {
      return [
        { value: 'true', label: 'Đã chọn (Có)' },
        { value: 'false', label: 'Không chọn (Không)' },
      ];
    }
    if (hasOptions(type) || type === FieldType.Tags) {
      try {
        if (field.optionsJson) {
          const opts = JSON.parse(field.optionsJson);
          return opts.map((o: any) => ({ value: o.value, label: o.label }));
        }
      } catch {
        return null;
      }
    }
    return null;
  }
}
