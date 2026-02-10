import {
  Component,
  OnInit,
  OnDestroy,
  Input,
  Output,
  EventEmitter,
  inject,
  AfterViewInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  FormControl,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  FormsService,
  FormTemplate,
  FormField,
  FieldType,
  FormFieldValue,
  FieldValueRequest,
  FieldValueDetailRequest,
  ConditionalLogic,
  Condition,
  FileUploadResult,
  LinkedDataValue,
} from '../forms.service';
import { ConceptService } from '../services/concept.service';
import { Subject, Subscription, debounceTime, distinctUntilChanged, lastValueFrom } from 'rxjs';

@Component({
  selector: 'app-form-renderer',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './form-renderer.component.html',
  styleUrls: ['./form-renderer.component.scss'],
})
export class FormRendererComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly formsService = inject(FormsService);
  private readonly conceptService = inject(ConceptService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  FieldType = FieldType;
  templateId = '';
  responseId = ''; // For edit mode
  isEditMode = false;
  template: FormTemplate | null = null;
  fields: FormField[] = [];
  form: FormGroup = this.fb.group({});
  isSubmitting = false;
  fileValues: { [key: string]: File } = {};
  existingResponse: any = null; // Store loaded response for edit
  visibleFields: Set<string> = new Set();

  // Multi-page support
  pages: FormField[][] = []; // Fields split into pages by PageBreak
  currentPageIndex = 0;
  isMultiPage = false;

  // Review step
  showReview = false;

  // Signature canvases
  signatureContexts: { [key: string]: CanvasRenderingContext2D } = {};
  signatureDrawing: { [key: string]: boolean } = {};

  // Slider display values
  sliderValues: { [key: string]: number } = {};

  // Linked data (cross-form auto-fill)
  linkedData: LinkedDataValue[] = [];
  linkedDataMap: { [fieldId: string]: LinkedDataValue } = {};
  linkedFieldIds: Set<string> = new Set();
  patientId: string | null = null;
  cycleId: string | null = null;

  // Auto-save draft
  autoSaveEnabled = true;
  autoSaveStatus: 'idle' | 'saving' | 'saved' | 'error' = 'idle';
  lastAutoSavedAt: Date | null = null;
  private autoSaveSubject = new Subject<void>();
  private autoSaveSubscription?: Subscription;
  private draftResponseId: string | null = null;

  private valueChangesSubscription: any;

  ngOnInit() {
    // Set up auto-save debounce (5 seconds after last change)
    this.autoSaveSubscription = this.autoSaveSubject
      .pipe(debounceTime(5000), distinctUntilChanged())
      .subscribe(() => this.autoSaveDraft());

    this.route.params.subscribe((params) => {
      if (params['responseId']) {
        // Edit mode - load existing response
        this.responseId = params['responseId'];
        this.isEditMode = true;
        this.autoSaveEnabled = false; // Don't auto-save when editing existing response
        this.loadResponseForEdit();
      } else if (params['id']) {
        // New mode - load template only
        this.templateId = params['id'];
        this.loadTemplate();
        // Restore draft from localStorage if available
        this.restoreDraft();
      }
    });

    // Read patient/cycle from query params for linked data
    this.route.queryParams.subscribe((qp) => {
      this.patientId = qp['patientId'] || null;
      this.cycleId = qp['cycleId'] || null;
    });
  }

  ngOnDestroy() {
    if (this.valueChangesSubscription) {
      this.valueChangesSubscription.unsubscribe();
    }
    this.autoSaveSubscription?.unsubscribe();
  }

  loadResponseForEdit() {
    this.formsService.getResponseById(this.responseId).subscribe((response) => {
      this.existingResponse = response;
      this.templateId = response.formTemplateId;
      this.loadTemplate(() => this.populateFormWithResponse(response));
    });
  }

  loadTemplate(onComplete?: () => void) {
    this.formsService.getTemplateById(this.templateId).subscribe((template) => {
      this.template = template;
      // Normalize fieldType from string to enum value (API returns strings)
      this.fields = (template.fields || [])
        .map((field) => ({
          ...field,
          fieldType: this.normalizeFieldType(field.fieldType),
        }))
        .sort((a, b) => a.displayOrder - b.displayOrder);
      this.buildForm();
      if (onComplete) {
        onComplete();
      } else if (!this.isEditMode) {
        // For new entries, load linked data to auto-fill
        this.loadLinkedData();
      }
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
      Text: FieldType.Text,
      TextArea: FieldType.TextArea,
      Number: FieldType.Number,
      Decimal: FieldType.Decimal,
      Date: FieldType.Date,
      DateTime: FieldType.DateTime,
      Time: FieldType.Time,
      Dropdown: FieldType.Dropdown,
      MultiSelect: FieldType.MultiSelect,
      Radio: FieldType.Radio,
      Checkbox: FieldType.Checkbox,
      FileUpload: FieldType.FileUpload,
      Rating: FieldType.Rating,
      Section: FieldType.Section,
      Label: FieldType.Label,
      Tags: FieldType.Tags,
      PageBreak: FieldType.PageBreak,
      Address: FieldType.Address,
      Hidden: FieldType.Hidden,
      Slider: FieldType.Slider,
      Calculated: FieldType.Calculated,
      RichText: FieldType.RichText,
      Signature: FieldType.Signature,
      Lookup: FieldType.Lookup,
      Repeater: FieldType.Repeater,
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
      Text: FieldType.Text,
      TextArea: FieldType.TextArea,
      Number: FieldType.Number,
      Decimal: FieldType.Decimal,
      Date: FieldType.Date,
      DateTime: FieldType.DateTime,
      Time: FieldType.Time,
      Dropdown: FieldType.Dropdown,
      MultiSelect: FieldType.MultiSelect,
      Radio: FieldType.Radio,
      Checkbox: FieldType.Checkbox,
      FileUpload: FieldType.FileUpload,
      Rating: FieldType.Rating,
      Section: FieldType.Section,
      Label: FieldType.Label,
      Tags: FieldType.Tags,
      PageBreak: FieldType.PageBreak,
      Address: FieldType.Address,
    };

    for (const field of this.fields) {
      // Normalize fieldType
      if (typeof field.fieldType === 'number') {
        // Already a number, keep it
      } else if (typeof field.fieldType === 'string') {
        field.fieldType =
          fieldTypeMap[field.fieldType] ?? (Number(field.fieldType) || FieldType.Text);
      } else {
        field.fieldType = FieldType.Text;
      }

      if (
        field.fieldType === FieldType.Section ||
        field.fieldType === FieldType.Label ||
        field.fieldType === FieldType.PageBreak
      ) {
        continue;
      }

      // Calculated fields: read-only display with formula evaluation
      if (field.fieldType === FieldType.Calculated) {
        formConfig[field.id] = new FormControl({ value: '', disabled: true });
        continue;
      }

      // Hidden fields: resolve token-based default values
      if (field.fieldType === FieldType.Hidden) {
        const resolvedValue = this.resolveHiddenToken(field.defaultValue || '');
        formConfig[field.id] = new FormControl(resolvedValue);
        continue;
      }

      // Address/Composite: create a nested FormGroup with sub-field controls
      if (field.fieldType === FieldType.Address) {
        const subFields = this.getAddressSubFields(field);
        const subGroup: { [key: string]: FormControl } = {};
        for (const sub of subFields) {
          const subValidators = sub.required ? [Validators.required] : [];
          subGroup[sub.key] = new FormControl('', subValidators);
        }
        formConfig[field.id] = new FormGroup(subGroup) as any;
        continue;
      }

      // Repeater: store as JSON string
      if (field.fieldType === FieldType.Repeater) {
        formConfig[field.id] = new FormControl('[]');
        continue;
      }

      const validators = [];
      if (field.isRequired) {
        validators.push(Validators.required);
      }

      // Parse and apply validation rules from validationRulesJson
      const validationRules = this.formsService.parseValidationRules(field.validationRulesJson);
      for (const rule of validationRules) {
        switch (rule.type) {
          case 'minLength':
            if (rule.value != null) validators.push(Validators.minLength(Number(rule.value)));
            break;
          case 'maxLength':
            if (rule.value != null) validators.push(Validators.maxLength(Number(rule.value)));
            break;
          case 'min':
            if (rule.value != null) validators.push(Validators.min(Number(rule.value)));
            break;
          case 'max':
            if (rule.value != null) validators.push(Validators.max(Number(rule.value)));
            break;
          case 'pattern':
            if (rule.value) validators.push(Validators.pattern(rule.value));
            break;
          case 'email':
            validators.push(Validators.email);
            break;
        }
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
      } else if (field.fieldType === FieldType.Slider) {
        try {
          const config = field.optionsJson ? JSON.parse(field.optionsJson) : {};
          defaultValue = field.defaultValue ? parseFloat(field.defaultValue) : (config.min || 0);
        } catch {
          defaultValue = 0;
        }
      } else if (field.fieldType === FieldType.Signature) {
        defaultValue = '';
      }

      formConfig[field.id] = new FormControl(defaultValue, validators);
      // console.log(`Built field: ${field.label} (${field.id}), Type: ${field.fieldType} (Normalized)`);
    }

    this.form = this.fb.group(formConfig);

    // Initial evaluation
    this.evaluateConditions();

    // Build multi-page structure
    this.buildPages();

    // Subscribe to changes
    this.valueChangesSubscription = this.form.valueChanges.subscribe(() => {
      this.evaluateConditions();
      this.evaluateCalculatedFields();
      // Trigger auto-save on value changes
      if (this.autoSaveEnabled && !this.isEditMode) {
        this.autoSaveSubject.next();
      }
    });
  }

  // ===== Linked Data (Cross-Form Auto-Fill) =====
  loadLinkedData() {
    if (!this.patientId || !this.templateId) return;

    this.formsService.getLinkedData(this.templateId, this.patientId, this.cycleId || undefined).subscribe({
      next: (data) => {
        this.linkedData = data;
        this.linkedDataMap = {};
        this.linkedFieldIds = new Set();

        for (const item of data) {
          this.linkedDataMap[item.fieldId] = item;
          this.linkedFieldIds.add(item.fieldId);
        }

        if (data.length > 0) {
          this.applyLinkedData();
        }
      },
      error: (err) => {
        console.warn('Failed to load linked data:', err);
      },
    });
  }

  applyLinkedData() {
    for (const item of this.linkedData) {
      const control = this.form.get(item.fieldId);
      if (!control) continue;

      // Only pre-fill if the field is currently empty
      const currentValue = control.value;
      if (currentValue !== '' && currentValue !== null && currentValue !== undefined) continue;

      const field = this.fields.find(f => f.id === item.fieldId);
      if (!field) continue;

      // Apply value based on field type
      switch (field.fieldType) {
        case FieldType.Number:
        case FieldType.Decimal:
        case FieldType.Rating:
        case FieldType.Slider:
          if (item.numericValue != null) {
            control.setValue(item.numericValue, { emitEvent: true });
          }
          break;
        case FieldType.Date:
          if (item.dateValue) {
            control.setValue(new Date(item.dateValue).toISOString().split('T')[0], { emitEvent: true });
          }
          break;
        case FieldType.DateTime:
          if (item.dateValue) {
            control.setValue(new Date(item.dateValue).toISOString().slice(0, 16), { emitEvent: true });
          }
          break;
        case FieldType.Checkbox:
          if (item.booleanValue != null) {
            control.setValue(item.booleanValue, { emitEvent: true });
          } else if (item.jsonValue) {
            try {
              control.setValue(JSON.parse(item.jsonValue), { emitEvent: true });
            } catch { /* ignore */ }
          }
          break;
        case FieldType.MultiSelect:
        case FieldType.Tags:
          if (item.jsonValue) {
            try {
              control.setValue(JSON.parse(item.jsonValue), { emitEvent: true });
            } catch { /* ignore */ }
          }
          break;
        default:
          if (item.textValue) {
            control.setValue(item.textValue, { emitEvent: true });
          }
          break;
      }
    }
  }

  isLinkedField(fieldId: string): boolean {
    return this.linkedFieldIds.has(fieldId);
  }

  getLinkedDataInfo(fieldId: string): LinkedDataValue | null {
    return this.linkedDataMap[fieldId] || null;
  }

  clearLinkedValue(fieldId: string) {
    const control = this.form.get(fieldId);
    if (control) {
      control.setValue('', { emitEvent: true });
    }
    this.linkedFieldIds.delete(fieldId);
    delete this.linkedDataMap[fieldId];
  }

  // ===== Multi-page Support =====
  buildPages() {
    const hasPageBreak = this.fields.some((f) => f.fieldType === FieldType.PageBreak);
    if (!hasPageBreak) {
      this.isMultiPage = false;
      this.pages = [this.fields];
      return;
    }

    this.isMultiPage = true;
    this.pages = [];
    let currentPage: FormField[] = [];

    for (const field of this.fields) {
      if (field.fieldType === FieldType.PageBreak) {
        if (currentPage.length > 0) {
          this.pages.push(currentPage);
        }
        currentPage = [];
      } else {
        currentPage.push(field);
      }
    }
    // Push last page
    if (currentPage.length > 0) {
      this.pages.push(currentPage);
    }

    this.currentPageIndex = 0;
  }

  get currentPageFields(): FormField[] {
    if (!this.isMultiPage) return this.fields;
    return this.pages[this.currentPageIndex] || [];
  }

  get totalPages(): number {
    return this.pages.length;
  }

  get isFirstPage(): boolean {
    return this.currentPageIndex === 0;
  }

  get isLastPage(): boolean {
    return this.currentPageIndex === this.pages.length - 1;
  }

  get pageProgress(): number {
    if (this.totalPages <= 1) return 100;
    return Math.round(((this.currentPageIndex + 1) / this.totalPages) * 100);
  }

  nextPage() {
    // Validate current page fields before proceeding
    const currentFields = this.currentPageFields;
    let hasErrors = false;

    for (const field of currentFields) {
      if (field.fieldType === FieldType.Section || field.fieldType === FieldType.Label) continue;
      const control = this.form.get(field.id);
      if (control) {
        control.markAsTouched();
        if (control.invalid && this.isFieldVisible(field.id)) {
          hasErrors = true;
        }
      }
    }

    if (hasErrors) return;

    if (this.currentPageIndex < this.pages.length - 1) {
      this.currentPageIndex++;
      this.scrollToTop();
    }
  }

  prevPage() {
    if (this.currentPageIndex > 0) {
      this.currentPageIndex--;
      this.scrollToTop();
    }
  }

  goToPage(index: number) {
    if (index >= 0 && index < this.pages.length) {
      this.currentPageIndex = index;
      this.scrollToTop();
    }
  }

  private scrollToTop() {
    setTimeout(() => {
      const container = document.querySelector('.form-renderer-container');
      if (container) container.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  isFieldOnCurrentPage(fieldId: string): boolean {
    if (!this.isMultiPage) return true;
    return this.currentPageFields.some((f) => f.id === fieldId);
  }

  ngAfterViewInit() {
    // Initialize tags fields content
    setTimeout(() => {
      this.initTagsFields();
    });
  }

  initTagsFields() {
    this.fields.forEach((field) => {
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
            mentions: data.mentions,
          };
        }
      } else if (Array.isArray(data)) {
        // Format: Array of tag values or objects
        // Reconstruct HTML with badges
        const field = this.fields.find((f) => f.id === fieldId);
        const options = field ? this.getOptions(field) : [];

        let html = '';
        data.forEach((item: any) => {
          const tagValue = typeof item === 'string' ? item : item.value;
          const opt =
            options.find((o) => o.value === tagValue) ||
            options.find((o) => (o as any).conceptId === tagValue);
          const label = opt?.label || tagValue;
          const conceptId =
            (opt as any)?.conceptId || (typeof item === 'object' ? item.conceptId : null);

          // Create badge HTML similar to what insertMentionTag creates
          html += `<span class="mention-tag" contenteditable="false" data-concept-id="${conceptId || ''}" data-value="${tagValue}">${label}</span> `;
        });

        editor.innerHTML = html;
      } else {
        // Plain text fallback - but avoid showing "undefined"
        editor.innerText = val && val !== 'undefined' ? String(val) : '';
      }
    } catch {
      // Plain text fallback - but avoid showing "undefined"
      editor.innerText = val && val !== 'undefined' ? String(val) : '';
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
          const results = logic.conditions.map((cond) => {
            const triggerValue = formValues[cond.fieldId];
            return this.checkCondition(triggerValue, cond.operator, cond.value);
          });

          if (logic.logic === 'AND') {
            isMatch = results.every((r) => r);
          } else {
            // OR
            isMatch = results.some((r) => r);
          }
        }

        // Determine visibility based on Action + Match
        let shouldShow = true;
        if (logic.action === 'show') {
          shouldShow = isMatch;
        } else {
          // hide
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
      case 'eq':
        return tStr === vStr;
      case 'neq':
        return tStr !== vStr;
      case 'gt':
        return parseFloat(triggerValue) > parseFloat(targetValue);
      case 'lt':
        return parseFloat(triggerValue) < parseFloat(targetValue);
      case 'contains':
        return tStr.includes(vStr);
      default:
        return false;
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

          case FieldType.Address:
            if (fv.jsonValue) {
              try {
                const addrData = JSON.parse(fv.jsonValue);
                control.patchValue(addrData);
              } catch {
                // ignore
              }
            }
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
    tags.forEach((tag) => {
      (tag as HTMLElement).style.cssText = inlineStyles;
      (tag as HTMLElement).contentEditable = 'false';
    });
  }

  getOptions(field: FormField) {
    return this.formsService.parseOptions(field.optionsJson);
  }

  getAddressSubFields(
    field: FormField,
  ): { key: string; label: string; type: string; required: boolean; width: number }[] {
    try {
      const subs = field.optionsJson ? JSON.parse(field.optionsJson) : [];
      if (Array.isArray(subs) && subs.length > 0 && subs[0].key) {
        return subs;
      }
    } catch {}
    // Default Vietnamese address sub-fields
    return [
      { key: 'street', label: 'Đường / Số nhà', type: 'text', required: true, width: 100 },
      { key: 'ward', label: 'Phường/Xã', type: 'text', required: false, width: 50 },
      { key: 'district', label: 'Quận/Huyện', type: 'text', required: false, width: 50 },
      { key: 'province', label: 'Tỉnh/Thành phố', type: 'text', required: true, width: 50 },
      { key: 'country', label: 'Quốc gia', type: 'text', required: false, width: 50 },
    ];
  }

  getFieldColSpan(field: FormField): number {
    try {
      // Read from layoutJson first, then fallback to validationRulesJson for backward compat
      const json = field.layoutJson || field.validationRulesJson;
      const data = json ? JSON.parse(json) : {};
      // Section always spans 4 columns
      if (
        field.fieldType === FieldType.Section ||
        field.fieldType === FieldType.Label ||
        field.fieldType === FieldType.PageBreak
      ) {
        return 4;
      }
      return data.colSpan || 4;
    } catch {
      return 4;
    }
  }

  getFieldHeight(field: FormField): string {
    try {
      const json = field.layoutJson || field.validationRulesJson;
      const data = json ? JSON.parse(json) : {};
      const height = data.height || 'auto';
      const heightMap: { [key: string]: string } = {
        auto: 'auto',
        small: '60px',
        medium: '100px',
        large: '150px',
        xlarge: '200px',
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

  getFieldErrors(fieldId: string): string[] {
    const control = this.form.get(fieldId);
    if (!control || !control.errors || !control.touched) return [];
    const errors: string[] = [];
    const field = this.fields.find((f) => f.id === fieldId);
    const rules = field ? this.formsService.parseValidationRules(field.validationRulesJson) : [];
    const ruleMessageMap = new Map(rules.filter((r) => r.message).map((r) => [r.type, r.message!]));

    if (control.errors['required']) {
      errors.push(ruleMessageMap.get('required') || 'Trường này là bắt buộc');
    }
    if (control.errors['minlength']) {
      const req = control.errors['minlength'].requiredLength;
      errors.push(ruleMessageMap.get('minLength') || `Tối thiểu ${req} ký tự`);
    }
    if (control.errors['maxlength']) {
      const req = control.errors['maxlength'].requiredLength;
      errors.push(ruleMessageMap.get('maxLength') || `Tối đa ${req} ký tự`);
    }
    if (control.errors['min']) {
      const min = control.errors['min'].min;
      errors.push(ruleMessageMap.get('min') || `Giá trị tối thiểu là ${min}`);
    }
    if (control.errors['max']) {
      const max = control.errors['max'].max;
      errors.push(ruleMessageMap.get('max') || `Giá trị tối đa là ${max}`);
    }
    if (control.errors['pattern']) {
      errors.push(ruleMessageMap.get('pattern') || 'Giá trị không đúng định dạng');
    }
    if (control.errors['email']) {
      errors.push(ruleMessageMap.get('email') || 'Email không hợp lệ');
    }
    return errors;
  }

  // ===== Auto-save Draft =====
  private autoSaveDraft() {
    if (!this.templateId || this.isSubmitting) return;

    const formValues = this.form.getRawValue();
    const draftKey = `form_draft_${this.templateId}`;

    // Save to localStorage for quick recovery
    try {
      const draftData = {
        templateId: this.templateId,
        values: formValues,
        savedAt: new Date().toISOString(),
      };
      localStorage.setItem(draftKey, JSON.stringify(draftData));
      this.autoSaveStatus = 'saved';
      this.lastAutoSavedAt = new Date();

      // Reset status after 3 seconds
      setTimeout(() => {
        if (this.autoSaveStatus === 'saved') {
          this.autoSaveStatus = 'idle';
        }
      }, 3000);
    } catch (e) {
      this.autoSaveStatus = 'error';
      console.error('Auto-save failed:', e);
    }
  }

  private restoreDraft() {
    if (!this.templateId) return;
    const draftKey = `form_draft_${this.templateId}`;

    try {
      const stored = localStorage.getItem(draftKey);
      if (!stored) return;

      const draftData = JSON.parse(stored);
      if (draftData.templateId !== this.templateId) return;

      // Check if draft is less than 24 hours old
      const savedAt = new Date(draftData.savedAt);
      const hoursSinceSave = (Date.now() - savedAt.getTime()) / (1000 * 60 * 60);
      if (hoursSinceSave > 24) {
        localStorage.removeItem(draftKey);
        return;
      }

      // Wait for form to be built, then restore values
      setTimeout(() => {
        if (draftData.values && Object.keys(draftData.values).length > 0) {
          // Check if form has any user-entered values already
          const formValues = this.form.getRawValue();
          const hasValues = Object.values(formValues).some(
            (v) => v !== '' && v !== null && v !== undefined && v !== false,
          );
          if (hasValues) return; // Don't overwrite if user already entered data

          this.form.patchValue(draftData.values, { emitEvent: false });
          this.lastAutoSavedAt = savedAt;
          this.autoSaveStatus = 'saved';

          // Re-evaluate conditions after restoring
          this.evaluateConditions();
        }
      }, 500);
    } catch (e) {
      console.error('Failed to restore draft:', e);
    }
  }

  clearDraft() {
    if (!this.templateId) return;
    localStorage.removeItem(`form_draft_${this.templateId}`);
    this.autoSaveStatus = 'idle';
    this.lastAutoSavedAt = null;
  }

  toggleAutoSave() {
    this.autoSaveEnabled = !this.autoSaveEnabled;
  }

  async submit() {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    this.isSubmitting = true;
    const fieldValues: FieldValueRequest[] = [];

    for (const field of this.fields) {
      if (
        field.fieldType === FieldType.Section ||
        field.fieldType === FieldType.Label ||
        field.fieldType === FieldType.PageBreak ||
        field.fieldType === FieldType.Calculated
      ) {
        continue;
      }

      const value = this.form.get(field.id)?.value;
      const fieldValue: FieldValueRequest = { formFieldId: field.id };
      const details: FieldValueDetailRequest[] = [];

      switch (field.fieldType) {
        case FieldType.Number:
        case FieldType.Decimal:
        case FieldType.Rating:
        case FieldType.Slider:
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
                const opt = checkboxOptions.find((o) => o.value === v);
                if (opt) {
                  details.push({
                    value: opt.value,
                    label: opt.label,
                    conceptId: (opt as any).conceptId,
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
                conceptId: field.conceptId,
              });
            }
          }
          break;
        case FieldType.MultiSelect:
          fieldValue.jsonValue = JSON.stringify(value || []);
          if (Array.isArray(value)) {
            const options = this.getOptions(field);
            value.forEach((v: string) => {
              const opt = options.find((o) => o.value === v);
              if (opt) {
                details.push({
                  value: opt.value,
                  label: opt.label,
                  conceptId: (opt as any).conceptId,
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

          tags.forEach((t) => {
            // Look up by VALUE or CONCEPT ID (to handle legacy data)
            const opt =
              tagOptions.find((o) => o.value === t) ||
              tagOptions.find((o) => (o as any).conceptId === t);

            if (opt) {
              details.push({
                value: opt.value,
                label: opt.label,
                conceptId: (opt as any).conceptId,
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
            const opt = options.find((o) => o.value === value);
            if (opt) {
              details.push({
                value: opt.value,
                label: opt.label,
                conceptId: (opt as any).conceptId,
              });
            }
          }
          break;
        case FieldType.FileUpload:
          // Upload file if one was selected
          const file = this.fileValues[field.fieldKey || field.id];
          if (file) {
            try {
              const uploadResult = await lastValueFrom(this.formsService.uploadFile(file));
              fieldValue.textValue = uploadResult.url;
              fieldValue.jsonValue = JSON.stringify({
                fileName: uploadResult.fileName,
                filePath: uploadResult.filePath,
                contentType: uploadResult.contentType,
                fileSize: uploadResult.fileSize,
                url: uploadResult.url,
              });
            } catch {
              this.isSubmitting = false;
              alert(`Lỗi tải file cho trường "${field.label}"`);
              return;
            }
          } else if (value) {
            // Existing file URL (edit mode)
            fieldValue.textValue = value?.toString() || '';
          }
          break;
        case FieldType.Address:
          // Address sub-fields are stored as JSON object
          const addressGroup = this.form.get(field.id);
          if (addressGroup) {
            const addressValue = addressGroup.value;
            fieldValue.jsonValue = JSON.stringify(addressValue);
            // Build a readable text summary for textValue
            const parts = Object.values(addressValue).filter((v) => v);
            fieldValue.textValue = parts.join(', ');
          }
          break;
        case FieldType.Signature:
        case FieldType.RichText:
          fieldValue.textValue = value?.toString() || '';
          break;
        case FieldType.Hidden:
          fieldValue.textValue = value?.toString() || '';
          break;
        case FieldType.Lookup:
          fieldValue.textValue = value?.toString() || '';
          if (value) {
            const lookupOpts = this.getOptions(field);
            const lookupOpt = lookupOpts.find((o) => o.value === value);
            if (lookupOpt) {
              details.push({ value: lookupOpt.value, label: lookupOpt.label, conceptId: (lookupOpt as any).conceptId });
            }
          }
          break;
        case FieldType.Repeater:
          fieldValue.jsonValue = value || '[]';
          break;
        default:
          fieldValue.textValue = value?.toString() || '';
      }

      if (details.length > 0) {
        fieldValue.details = details;
      }

      // Debug log each field
      console.log(
        `Field: ${field.label} (${field.id}), Type: ${field.fieldType}, ID: ${field.id}, Value:`,
        value,
        '=> fieldValue:',
        fieldValue,
      );

      fieldValues.push(fieldValue);
    }

    const request = {
      formTemplateId: this.templateId,
      submittedByUserId: null, // Will be set from auth when available
      fieldValues,
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
        error: (err) => {
          this.isSubmitting = false;
          const msg = err.error || 'Có lỗi xảy ra, vui lòng thử lại.';
          alert(typeof msg === 'string' ? msg : JSON.stringify(msg));
        },
      });
    } else {
      // Create new response
      this.formsService.submitResponse(request).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.clearDraft(); // Clear auto-saved draft on successful submit
          alert('Đã gửi phản hồi thành công!');
          this.router.navigate(['/forms']);
        },
        error: (err) => {
          this.isSubmitting = false;
          const msg = err.error || 'Có lỗi xảy ra, vui lòng thử lại.';
          alert(typeof msg === 'string' ? msg : JSON.stringify(msg));
        },
      });
    }
  }

  cancel() {
    this.router.navigate(['/forms']);
  }

  saveDraftToDb() {
    this.isSubmitting = true;
    const fieldValues: FieldValueRequest[] = [];

    for (const field of this.fields) {
      if (
        field.fieldType === FieldType.Section ||
        field.fieldType === FieldType.Label ||
        field.fieldType === FieldType.PageBreak
      ) {
        continue;
      }
      const value = this.form.get(field.id)?.value;
      if (value === null || value === undefined || value === '') continue;

      const fieldValue: FieldValueRequest = { formFieldId: field.id };
      if (typeof value === 'number') {
        fieldValue.numericValue = value;
      } else if (typeof value === 'boolean') {
        fieldValue.booleanValue = value;
      } else if (Array.isArray(value)) {
        fieldValue.jsonValue = JSON.stringify(value);
      } else {
        fieldValue.textValue = value?.toString() || '';
      }
      fieldValues.push(fieldValue);
    }

    const request = {
      formTemplateId: this.templateId,
      submittedByUserId: null as string | null,
      fieldValues,
      isDraft: true,
    };

    this.formsService.submitResponse(request).subscribe({
      next: (resp) => {
        this.isSubmitting = false;
        alert('Đã lưu bản nháp thành công!');
        this.router.navigate(['/forms/fill', this.templateId], {
          queryParams: { responseId: resp.id },
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error || 'Có lỗi xảy ra khi lưu nháp.';
        alert(typeof msg === 'string' ? msg : JSON.stringify(msg));
      },
    });
  }

  // ===== Entry Preview / Review Step =====
  toggleReview() {
    // Validate all fields before showing review
    if (!this.showReview) {
      this.form.markAllAsTouched();
      if (this.form.invalid) return;
    }
    this.showReview = !this.showReview;
  }

  getReviewDisplayValue(field: FormField): string {
    const value = this.form.get(field.id)?.value;
    if (value === null || value === undefined || value === '') return '—';

    switch (field.fieldType) {
      case FieldType.Dropdown:
      case FieldType.Radio: {
        const opts = this.getOptions(field);
        const opt = opts.find((o: any) => o.value === value);
        return opt?.label ?? value;
      }
      case FieldType.Checkbox: {
        const opts = this.getOptions(field);
        if (opts.length > 0 && Array.isArray(value)) {
          return value.map((v: string) => {
            const opt = opts.find((o: any) => o.value === v);
            return opt?.label ?? v;
          }).join(', ');
        }
        return typeof value === 'boolean' ? (value ? 'Có' : 'Không') : String(value);
      }
      case FieldType.MultiSelect: {
        if (!Array.isArray(value)) return '—';
        const opts = this.getOptions(field);
        return value.map((v: string) => {
          const opt = opts.find((o: any) => o.value === v);
          return opt?.label ?? v;
        }).join(', ');
      }
      case FieldType.Rating:
        return '⭐'.repeat(value || 0);
      case FieldType.Date:
        return value ? new Date(value).toLocaleDateString('vi-VN') : '—';
      case FieldType.DateTime:
        return value ? new Date(value).toLocaleString('vi-VN') : '—';
      case FieldType.Address:
        if (typeof value === 'object') {
          const parts = Object.values(value).filter((v: any) => v);
          return parts.length > 0 ? (parts as string[]).join(', ') : '—';
        }
        return '—';
      case FieldType.FileUpload:
        return this.fileValues[field.id]?.name ?? (value || '—');
      case FieldType.Slider: {
        const config = this.getSliderConfig(field);
        return `${value}${config.unit ? ' ' + config.unit : ''}`;
      }
      case FieldType.Signature:
        return value ? '✍️ Đã ký' : '—';
      case FieldType.Hidden:
        return value || '—';
      case FieldType.Calculated:
        return this.form.get(field.id)?.value || '—';
      default:
        return String(value);
    }
  }

  getReviewFields(): FormField[] {
    return this.fields.filter(f =>
      f.fieldType !== FieldType.Section &&
      f.fieldType !== FieldType.Label &&
      f.fieldType !== FieldType.PageBreak &&
      f.fieldType !== FieldType.Hidden &&
      this.isFieldVisible(f.id)
    );
  }

  // ===== Hidden Field Token Resolution =====
  resolveHiddenToken(token: string): string {
    if (!token) return '';
    const now = new Date();
    return token
      .replace(/\{currentUser\}/gi, 'current-user-id')
      .replace(/\{currentDate\}/gi, now.toISOString().split('T')[0])
      .replace(/\{currentTime\}/gi, now.toTimeString().slice(0, 5))
      .replace(/\{currentDateTime\}/gi, now.toISOString())
      .replace(/\{timestamp\}/gi, now.getTime().toString());
  }

  // ===== Calculated Fields =====
  evaluateCalculatedFields() {
    const calcFields = this.fields.filter(f => f.fieldType === FieldType.Calculated);
    if (calcFields.length === 0) return;

    const formValues = this.form.getRawValue();
    // Build a field key-to-id map and key-to-value map
    const keyToValue: { [key: string]: number } = {};
    for (const field of this.fields) {
      const val = formValues[field.id];
      if (val !== null && val !== undefined && !isNaN(Number(val))) {
        keyToValue[field.fieldKey] = Number(val);
      }
    }

    for (const field of calcFields) {
      try {
        const config = field.optionsJson ? JSON.parse(field.optionsJson) : {};
        const formula: string = config.formula || '';
        if (!formula) continue;

        // Replace {fieldKey} tokens with numeric values
        let expression = formula.replace(/\{(\w+)\}/g, (_, key) => {
          return (keyToValue[key] ?? 0).toString();
        });

        // Replace ^ with ** for exponentiation
        expression = expression.replace(/\^/g, '**');

        // Safe eval using Function constructor (only math operations)
        const result = new Function('return ' + expression)();
        const decimalPlaces = config.decimalPlaces ?? 2;
        const unit = config.unit || '';

        const displayValue = isNaN(result) ? 'N/A' : `${Number(result).toFixed(decimalPlaces)}${unit ? ' ' + unit : ''}`;

        const control = this.form.get(field.id);
        if (control && control.value !== displayValue) {
          control.setValue(displayValue, { emitEvent: false });
        }
      } catch {
        const control = this.form.get(field.id);
        if (control) control.setValue('Lỗi công thức', { emitEvent: false });
      }
    }
  }

  // ===== Slider Helpers =====
  getSliderConfig(field: FormField): { min: number; max: number; step: number; unit: string } {
    try {
      const config = field.optionsJson ? JSON.parse(field.optionsJson) : {};
      return { min: config.min ?? 0, max: config.max ?? 100, step: config.step ?? 1, unit: config.unit || '' };
    } catch {
      return { min: 0, max: 100, step: 1, unit: '' };
    }
  }

  onSliderChange(fieldId: string, event: Event) {
    const val = Number((event.target as HTMLInputElement).value);
    this.form.get(fieldId)?.setValue(val);
    this.sliderValues[fieldId] = val;
  }

  getSliderDisplayValue(fieldId: string, field: FormField): string {
    const val = this.form.get(fieldId)?.value;
    const config = this.getSliderConfig(field);
    return `${val ?? config.min}${config.unit ? ' ' + config.unit : ''}`;
  }

  // ===== Signature Helpers =====
  initSignatureCanvas(fieldId: string) {
    setTimeout(() => {
      const canvas = document.getElementById('sig-' + fieldId) as HTMLCanvasElement;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      this.signatureContexts[fieldId] = ctx;
      ctx.strokeStyle = '#1e293b';
      ctx.lineWidth = 2;
      ctx.lineCap = 'round';

      canvas.addEventListener('mousedown', (e) => this.startSignatureDraw(fieldId, e));
      canvas.addEventListener('mousemove', (e) => this.drawSignature(fieldId, e));
      canvas.addEventListener('mouseup', () => this.endSignatureDraw(fieldId));
      canvas.addEventListener('mouseleave', () => this.endSignatureDraw(fieldId));
      // Touch support
      canvas.addEventListener('touchstart', (e) => { e.preventDefault(); this.startSignatureDraw(fieldId, e.touches[0]); });
      canvas.addEventListener('touchmove', (e) => { e.preventDefault(); this.drawSignature(fieldId, e.touches[0]); });
      canvas.addEventListener('touchend', () => this.endSignatureDraw(fieldId));
    }, 100);
  }

  startSignatureDraw(fieldId: string, e: MouseEvent | Touch) {
    this.signatureDrawing[fieldId] = true;
    const canvas = document.getElementById('sig-' + fieldId) as HTMLCanvasElement;
    const rect = canvas.getBoundingClientRect();
    const ctx = this.signatureContexts[fieldId];
    if (ctx) {
      ctx.beginPath();
      ctx.moveTo(e.clientX - rect.left, e.clientY - rect.top);
    }
  }

  drawSignature(fieldId: string, e: MouseEvent | Touch) {
    if (!this.signatureDrawing[fieldId]) return;
    const canvas = document.getElementById('sig-' + fieldId) as HTMLCanvasElement;
    const rect = canvas.getBoundingClientRect();
    const ctx = this.signatureContexts[fieldId];
    if (ctx) {
      ctx.lineTo(e.clientX - rect.left, e.clientY - rect.top);
      ctx.stroke();
    }
  }

  endSignatureDraw(fieldId: string) {
    if (!this.signatureDrawing[fieldId]) return;
    this.signatureDrawing[fieldId] = false;
    const canvas = document.getElementById('sig-' + fieldId) as HTMLCanvasElement;
    if (canvas) {
      const dataUrl = canvas.toDataURL('image/png');
      this.form.get(fieldId)?.setValue(dataUrl);
    }
  }

  clearSignature(fieldId: string) {
    const canvas = document.getElementById('sig-' + fieldId) as HTMLCanvasElement;
    if (canvas) {
      const ctx = this.signatureContexts[fieldId];
      if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
      this.form.get(fieldId)?.setValue('');
    }
  }

  // ===== Lookup Helpers =====
  lookupSearchQueries: { [key: string]: string } = {};
  lookupSearchResults: { [key: string]: { value: string; label: string }[] } = {};
  activeLookupFieldId = '';

  searchLookupOptions(field: FormField) {
    const query = (this.lookupSearchQueries[field.id] || '').toLowerCase();
    const options = this.getOptions(field);

    if (!query || query.length < 1) {
      this.lookupSearchResults[field.id] = options.slice(0, 10);
      return;
    }

    this.lookupSearchResults[field.id] = options
      .filter(opt => opt.label.toLowerCase().includes(query) || opt.value.toLowerCase().includes(query))
      .slice(0, 10);
  }

  selectLookupOption(fieldId: string, opt: { value: string; label: string }) {
    this.form.get(fieldId)?.setValue(opt.value);
    this.lookupSearchQueries[fieldId] = opt.label;
    this.lookupSearchResults[fieldId] = [];
    this.activeLookupFieldId = '';
  }

  getLookupDisplayLabel(field: FormField): string {
    const val = this.form.get(field.id)?.value;
    if (!val) return '';
    const opts = this.getOptions(field);
    const opt = opts.find(o => o.value === val);
    return opt?.label || val;
  }

  // ===== Repeater Helpers =====
  repeaterRows: { [key: string]: any[][] } = {};

  getRepeaterConfig(field: FormField): { minRows: number; maxRows: number; fields: { key: string; label: string; type: string }[] } {
    try {
      const config = field.optionsJson ? JSON.parse(field.optionsJson) : {};
      return {
        minRows: config.minRows ?? 1,
        maxRows: config.maxRows ?? 10,
        fields: config.fields || [],
      };
    } catch {
      return { minRows: 1, maxRows: 10, fields: [] };
    }
  }

  initRepeater(field: FormField) {
    if (!this.repeaterRows[field.id]) {
      const config = this.getRepeaterConfig(field);
      this.repeaterRows[field.id] = [];
      for (let i = 0; i < (config.minRows || 1); i++) {
        this.addRepeaterRow(field);
      }
    }
  }

  addRepeaterRow(field: FormField) {
    const config = this.getRepeaterConfig(field);
    if (!this.repeaterRows[field.id]) this.repeaterRows[field.id] = [];
    if (this.repeaterRows[field.id].length >= config.maxRows) return;

    const row: any = {};
    for (const col of config.fields) {
      row[col.key] = '';
    }
    this.repeaterRows[field.id].push(row);
    this.updateRepeaterFormValue(field.id);
  }

  removeRepeaterRow(field: FormField, index: number) {
    const config = this.getRepeaterConfig(field);
    if (this.repeaterRows[field.id]?.length <= (config.minRows || 0)) return;
    this.repeaterRows[field.id].splice(index, 1);
    this.updateRepeaterFormValue(field.id);
  }

  onRepeaterCellChange(fieldId: string) {
    this.updateRepeaterFormValue(fieldId);
  }

  private updateRepeaterFormValue(fieldId: string) {
    const control = this.form.get(fieldId);
    if (control) {
      control.setValue(JSON.stringify(this.repeaterRows[fieldId] || []));
    }
  }

  // Mention-style Tags input
  mentionTexts: { [key: string]: string } = {};
  mentionData: {
    [key: string]: {
      text: string;
      mentions: { text: string; conceptId: string; code: string; start: number; end: number }[];
    };
  } = {};
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
        this.mentionSearchResults = options.slice(0, 10).map((opt) => ({
          id: (opt as any).conceptId || opt.value,
          code: opt.value,
          name: opt.label,
        }));
      } else {
        const q = query.toLowerCase();
        this.mentionSearchResults = options
          .filter(
            (opt) => opt.label.toLowerCase().includes(q) || opt.value.toLowerCase().includes(q),
          )
          .slice(0, 10)
          .map((opt) => ({
            id: (opt as any).conceptId || opt.value,
            code: opt.value,
            name: opt.label,
          }));
      }
    } else {
      // No predefined options - search ALL concepts from API
      this.conceptService.searchConcepts(query || '', undefined, 10).subscribe({
        next: (result) => {
          this.mentionSearchResults = result.concepts.map((c) => ({
            id: c.id,
            code: c.code,
            name: c.display, // Concept uses 'display' not 'name'
          }));
        },
        error: () => {
          this.mentionSearchResults = [];
        },
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
      end: this.mentionSearchStart + concept.name.length,
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
    if (
      (event.key === 'Tab' || event.key === 'Enter') &&
      this.showMentionDropdown &&
      this.mentionSearchResults.length > 0
    ) {
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
      end: 0,
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
    tagElements.forEach((el) => {
      const id = el.getAttribute('data-concept-id');
      if (id) tagIds.push(id);
    });

    const textContent = editor.textContent || '';

    control.setValue(
      JSON.stringify({
        text: textContent,
        tagIds: [...new Set(tagIds)],
        html: editor.innerHTML,
      }),
    );
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
    const field = this.fields.find((f) => f.id === fieldKey);
    if (!field) return [];

    const options = this.getOptions(field);

    console.log('getMentionTagIds - field:', field.label);
    console.log('getMentionTagIds - options:', options);
    console.log('getMentionTagIds - mentions:', data.mentions);

    // Map concept IDs to option values
    const optionValues: string[] = [];
    data.mentions.forEach((m) => {
      console.log(`Looking for conceptId ${m.conceptId} in options`);
      const opt = options.find((o) => (o as any).conceptId === m.conceptId);
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
    control.setValue(
      JSON.stringify({
        text: data.text,
        tagIds: this.getMentionTagIds(fieldKey),
        mentions: data.mentions,
      }),
    );
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

  getSelectedTags(
    fieldKey: string,
  ): { value: string; label: string; isCustom?: boolean; conceptId?: string }[] {
    const currentValue = this.form.get(fieldKey)?.value;
    if (!currentValue) return [];

    try {
      const selectedValues =
        typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue;
      if (!Array.isArray(selectedValues)) return [];

      // Find the field to get options
      const field = this.fields.find((f) => f.fieldKey === fieldKey);
      if (!field)
        return selectedValues.map((v: string) => ({ value: v, label: v, isCustom: true }));

      const options = this.getOptions(field);
      return selectedValues.map((v: string) => {
        const opt = options.find((o) => o.value === v);
        if (opt) {
          // Predefined tag with concept ID for analytics
          return {
            value: opt.value,
            label: opt.label,
            isCustom: false,
            conceptId: (opt as any).conceptId, // Include concept ID if available
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
    this.tagSearchResults[field.id] = allOptions
      .filter(
        (opt) =>
          opt.label.toLowerCase().includes(searchTerm) ||
          opt.value.toLowerCase().includes(searchTerm),
      )
      .slice(0, 10);
  }

  selectTag(fieldKey: string, opt: { value: string; label: string }) {
    const control = this.form.get(fieldKey);
    if (!control) return;

    let current: string[] = [];
    try {
      const currentValue = control.value;
      current = typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue || [];
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
      current = typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue || [];
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
      current = typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue || [];
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
      current = typeof currentValue === 'string' ? JSON.parse(currentValue) : currentValue || [];
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
    return options.some(
      (opt) =>
        opt.value.toLowerCase() === query.toLowerCase() ||
        opt.label.toLowerCase() === query.toLowerCase(),
    );
  }
}
