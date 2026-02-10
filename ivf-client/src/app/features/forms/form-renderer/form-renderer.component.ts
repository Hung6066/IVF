import { Component, OnInit, OnDestroy, inject, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  FormControl,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  FormsService,
  FormTemplate,
  FormField,
  FieldType,
  FieldValueRequest,
  FieldValueDetailRequest,
  ConditionalLogic,
  LinkedDataValue,
  ResponseStatus,
} from '../forms.service';
import { ConceptService } from '../services/concept.service';
import { PatientService } from '../../../core/services/patient.service';
import { Patient } from '../../../core/models/patient.models';
import {
  Subject,
  Subscription,
  debounceTime,
  distinctUntilChanged,
  switchMap,
  of,
  lastValueFrom,
} from 'rxjs';
import { normalizeFieldType, getFieldColSpan } from '../shared/form-field.utils';
import { FieldInputComponent } from './field-input/field-input.component';
import { PatientContextBarComponent } from './patient-context-bar/patient-context-bar.component';
import { FormReviewComponent } from './form-review/form-review.component';
import { FormActionsBarComponent } from './form-actions-bar/form-actions-bar.component';

@Component({
  selector: 'app-form-renderer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    FieldInputComponent,
    PatientContextBarComponent,
    FormReviewComponent,
    FormActionsBarComponent,
  ],
  templateUrl: './form-renderer.component.html',
  styleUrls: ['./form-renderer.component.scss'],
})
export class FormRendererComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly formsService = inject(FormsService);
  private readonly conceptService = inject(ConceptService);
  private readonly patientService = inject(PatientService);
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

  // (Signature, Slider states moved to FieldInputComponent)

  // Linked data (cross-form auto-fill)
  linkedData: LinkedDataValue[] = [];
  linkedDataMap: { [fieldId: string]: LinkedDataValue } = {};
  linkedFieldIds: Set<string> = new Set();
  patientId: string | null = null;
  cycleId: string | null = null;

  // Patient search (inline selector when patientId not provided)
  patientSearchQuery = '';
  patientSearchResults: Patient[] = [];
  selectedPatient: Patient | null = null;
  showPatientDropdown = false;
  private patientSearchSubject = new Subject<string>();
  private patientSearchSub?: Subscription;

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

    // Set up patient search debounce
    this.patientSearchSub = this.patientSearchSubject
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) =>
          q.length >= 2
            ? this.patientService.searchPatients(q, 1, 10)
            : of({ items: [], totalCount: 0 }),
        ),
      )
      .subscribe((result) => {
        this.patientSearchResults = result.items || [];
        this.showPatientDropdown = this.patientSearchResults.length > 0;
      });

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
      const newPatientId = qp['patientId'] || null;
      const newCycleId = qp['cycleId'] || null;
      const patientChanged = newPatientId !== this.patientId;
      this.patientId = newPatientId;
      this.cycleId = newCycleId;

      // Load patient info for display when patientId is provided via URL
      if (this.patientId && !this.selectedPatient) {
        this.patientService.getPatient(this.patientId).subscribe({
          next: (p) => (this.selectedPatient = p),
          error: () => {},
        });
      }

      // If template already loaded and patient changed, reload linked data
      if (patientChanged && this.template && this.patientId) {
        this.loadLinkedData();
      }
    });
  }

  ngOnDestroy() {
    if (this.valueChangesSubscription) {
      this.valueChangesSubscription.unsubscribe();
    }
    this.autoSaveSubscription?.unsubscribe();
    this.patientSearchSub?.unsubscribe();
  }

  loadResponseForEdit() {
    this.formsService.getResponseById(this.responseId).subscribe((response) => {
      this.existingResponse = response;
      this.templateId = response.formTemplateId;

      // Restore patient context from the saved response
      if (response.patientId) {
        this.patientId = response.patientId;
        this.patientService.getPatient(response.patientId).subscribe({
          next: (p) => (this.selectedPatient = p),
          error: () => {},
        });
      }

      this.loadTemplate(() => {
        this.populateFormWithResponse(response);
        // Load linked data after populating so existing values are preserved
        this.loadLinkedData();
      });
    });
  }

  loadTemplate(onComplete?: () => void) {
    this.formsService.getTemplateById(this.templateId).subscribe((template) => {
      this.template = template;
      // Normalize fieldType from string to enum value (API returns strings)
      this.fields = (template.fields || [])
        .map((field) => ({
          ...field,
          fieldType: normalizeFieldType(field.fieldType),
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

  buildForm() {
    const formConfig: { [key: string]: FormControl } = {};

    // Normalize fieldType - backend may return string name or number
    for (const field of this.fields) {
      field.fieldType = normalizeFieldType(field.fieldType);

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
          defaultValue = field.defaultValue ? parseFloat(field.defaultValue) : config.min || 0;
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
    if (!this.patientId || !this.templateId) {
      console.log('[loadLinkedData] Skipped - no patientId or templateId');
      return;
    }

    console.log(
      '[loadLinkedData] Fetching for template:',
      this.templateId,
      'patient:',
      this.patientId,
    );
    this.formsService
      .getLinkedData(this.templateId, this.patientId, this.cycleId || undefined)
      .subscribe({
        next: (data) => {
          console.log('[loadLinkedData] Received', data.length, 'linked data items:', data);
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

          // Also load previous response for same template+patient (self-fill)
          this.loadPreviousResponse();
        },
        error: (err) => {
          console.warn('[loadLinkedData] Failed:', err);
          // Still try previous response even if linked data fails
          this.loadPreviousResponse();
        },
      });
  }

  // Load the most recent submitted response for this template+patient
  // and pre-fill any fields that are still empty (not already filled by linked data)
  loadPreviousResponse() {
    if (!this.patientId || !this.templateId || this.isEditMode) {
      console.log(
        '[loadPreviousResponse] Skipped - patientId:',
        this.patientId,
        'templateId:',
        this.templateId,
        'isEditMode:',
        this.isEditMode,
      );
      return;
    }

    console.log(
      '[loadPreviousResponse] Fetching responses for template:',
      this.templateId,
      'patient:',
      this.patientId,
    );
    this.formsService
      .getResponses(
        this.templateId,
        this.patientId,
        undefined,
        undefined,
        1,
        1,
        ResponseStatus.Submitted,
      )
      .subscribe({
        next: (result) => {
          console.log(
            '[loadPreviousResponse] Found',
            result.items?.length || 0,
            'submitted responses',
          );
          if (!result.items || result.items.length === 0) return;
          const latestResponseId = result.items[0].id;
          console.log('[loadPreviousResponse] Loading response details:', latestResponseId);
          this.formsService.getResponseById(latestResponseId).subscribe({
            next: (response) => {
              console.log(
                '[loadPreviousResponse] Response loaded with',
                response.fieldValues?.length || 0,
                'field values',
              );
              if (!response.fieldValues) return;
              this.prefillFromPreviousResponse(response);
            },
            error: (err) => {
              console.error('[loadPreviousResponse] Error loading response details:', err);
            },
          });
        },
        error: (err) => {
          console.error('[loadPreviousResponse] Error fetching responses:', err);
        },
      });
  }

  prefillFromPreviousResponse(response: any) {
    console.log(
      '[prefillFromPreviousResponse] Processing',
      response.fieldValues.length,
      'field values',
    );
    const valueMap: { [key: string]: any } = {};
    response.fieldValues.forEach((fv: any) => {
      valueMap[fv.formFieldId] = fv;
    });

    setTimeout(() => {
      let filledCount = 0;
      let skippedCount = 0;
      for (const field of this.fields) {
        // Skip non-data fields
        if (
          field.fieldType === FieldType.Section ||
          field.fieldType === FieldType.Label ||
          field.fieldType === FieldType.PageBreak
        )
          continue;

        const fv = valueMap[field.id];
        if (!fv) continue;

        const control = this.form.get(field.id);
        if (!control) continue;

        // Only fill if field is currently empty (don't overwrite linked data or user input)
        if (!this.isFieldValueEmpty(control.value)) {
          skippedCount++;
          continue;
        }

        switch (field.fieldType) {
          case FieldType.Number:
          case FieldType.Decimal:
          case FieldType.Rating:
            if (fv.numericValue != null) {
              control.setValue(fv.numericValue);
              filledCount++;
            }
            break;
          case FieldType.Date:
            if (fv.dateValue) {
              control.setValue(new Date(fv.dateValue).toISOString().split('T')[0]);
              filledCount++;
            }
            break;
          case FieldType.DateTime:
            if (fv.dateValue) {
              control.setValue(new Date(fv.dateValue).toISOString().slice(0, 16));
              filledCount++;
            }
            break;
          case FieldType.Checkbox:
            if (fv.jsonValue) {
              try {
                const parsed = JSON.parse(fv.jsonValue);
                if (Array.isArray(parsed)) {
                  control.setValue(parsed);
                  filledCount++;
                  break;
                }
              } catch {
                /* ignore */
              }
            }
            if (fv.booleanValue != null) {
              control.setValue(fv.booleanValue);
              filledCount++;
            }
            break;
          case FieldType.MultiSelect:
            if (fv.jsonValue) {
              try {
                control.setValue(JSON.parse(fv.jsonValue));
                filledCount++;
              } catch {
                /* ignore */
              }
            }
            break;
          case FieldType.Tags:
            if (fv.jsonValue) {
              control.setValue(fv.jsonValue);
              filledCount++;
            } else if (fv.textValue) {
              control.setValue(fv.textValue);
              filledCount++;
            }
            break;
          default:
            if (fv.textValue) {
              control.setValue(fv.textValue);
              filledCount++;
            }
        }
      }
      console.log(
        '[prefillFromPreviousResponse] Filled',
        filledCount,
        'fields, skipped',
        skippedCount,
        'non-empty fields',
      );
    }, 150);
  }

  private isFieldValueEmpty(value: any): boolean {
    if (value === '' || value === null || value === undefined) return true;
    if (Array.isArray(value) && value.length === 0) return true;
    if (value === false) return true;
    return false;
  }

  applyLinkedData() {
    console.log('[applyLinkedData] Applying', this.linkedData.length, 'items');
    for (const item of this.linkedData) {
      const control = this.form.get(item.fieldId);
      if (!control) {
        console.log('[applyLinkedData] No control for field:', item.fieldId);
        continue;
      }

      // Only pre-fill if the field is currently empty
      if (!this.isFieldValueEmpty(control.value)) {
        console.log(
          '[applyLinkedData] Field not empty, skipping:',
          item.fieldId,
          'value:',
          control.value,
        );
        continue;
      }

      const field = this.fields.find((f) => f.id === item.fieldId);
      if (!field) {
        console.log('[applyLinkedData] No field definition for:', item.fieldId);
        continue;
      }

      console.log(
        '[applyLinkedData] Applying to field:',
        field.label,
        'type:',
        field.fieldType,
        'jsonValue:',
        item.jsonValue?.substring(0, 50),
      );
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
            control.setValue(new Date(item.dateValue).toISOString().split('T')[0], {
              emitEvent: true,
            });
          }
          break;
        case FieldType.DateTime:
          if (item.dateValue) {
            control.setValue(new Date(item.dateValue).toISOString().slice(0, 16), {
              emitEvent: true,
            });
          }
          break;
        case FieldType.Checkbox:
          if (item.booleanValue != null) {
            control.setValue(item.booleanValue, { emitEvent: true });
          } else if (item.jsonValue) {
            try {
              control.setValue(JSON.parse(item.jsonValue), { emitEvent: true });
            } catch {
              /* ignore */
            }
          }
          break;
        case FieldType.MultiSelect:
          if (item.jsonValue) {
            try {
              control.setValue(JSON.parse(item.jsonValue), { emitEvent: true });
            } catch {
              /* ignore */
            }
          }
          break;
        case FieldType.Tags:
          if (item.jsonValue) {
            // Tags control stores the raw JSON string, not parsed object
            control.setValue(item.jsonValue, { emitEvent: true });
          } else if (item.textValue) {
            control.setValue(item.textValue, { emitEvent: true });
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

  // ===== Patient Search (inline selector) =====
  onPatientSearchInput(query: string) {
    this.patientSearchQuery = query;
    this.patientSearchSubject.next(query);
  }

  selectPatient(patient: Patient) {
    this.selectedPatient = patient;
    this.patientId = patient.id;
    this.patientSearchQuery = '';
    this.showPatientDropdown = false;
    this.patientSearchResults = [];
    // Now load linked data for this patient
    this.loadLinkedData();
    // Update URL query params without reloading
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { patientId: patient.id },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  clearPatient() {
    this.selectedPatient = null;
    this.patientId = null;
    this.linkedData = [];
    this.linkedDataMap = {};
    this.linkedFieldIds = new Set();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { patientId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  hidePatientDropdown() {
    // Delay to allow click on dropdown item
    setTimeout(() => (this.showPatientDropdown = false), 200);
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
    // Tags fields are now initialized by FieldInputComponent
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

    // Check for visibility changes
    this.visibleFields = newVisibleFields;
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

  // getFieldColSpan kept for parent template (grid layout)
  getFieldColSpan(field: FormField): number {
    return getFieldColSpan(field);
  }

  // ===== File handling (fileValues tracked here for submit) =====
  onFieldFileChanged(event: { fieldId: string; file: File }) {
    this.fileValues[event.fieldId] = event.file;
  }

  hasError(fieldKey: string): boolean {
    const control = this.form.get(fieldKey);
    return control ? control.invalid && control.touched : false;
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
    // Don't restore draft when we have a patient context — loadPreviousResponse handles that
    if (this.route.snapshot.queryParams['patientId']) return;

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
          const hasValues = Object.values(formValues).some((v) => !this.isFieldValueEmpty(v));
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
              details.push({
                value: lookupOpt.value,
                label: lookupOpt.label,
                conceptId: (lookupOpt as any).conceptId,
              });
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

    const request: any = {
      formTemplateId: this.templateId,
      submittedByUserId: null, // Will be set from auth when available
      fieldValues,
    };

    // Include patient/cycle context so backend creates concept snapshots
    if (this.patientId) request.patientId = this.patientId;
    if (this.cycleId) request.cycleId = this.cycleId;

    console.log('Submitting form:', JSON.stringify(request, null, 2));

    if (this.isEditMode) {
      // Update existing response
      this.formsService.updateResponse(this.responseId, request).subscribe({
        next: () => {
          this.isSubmitting = false;
          alert('Đã cập nhật phản hồi thành công!');
          if (this.patientId) {
            this.router.navigate(['/patients', this.patientId]);
          } else {
            this.router.navigate(['/forms/responses', this.responseId]);
          }
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
          if (this.patientId) {
            this.router.navigate(['/patients', this.patientId]);
          } else {
            this.router.navigate(['/forms']);
          }
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
    if (this.patientId) {
      this.router.navigate(['/patients', this.patientId]);
    } else {
      this.router.navigate(['/forms']);
    }
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

    const request: any = {
      formTemplateId: this.templateId,
      submittedByUserId: null as string | null,
      fieldValues,
      isDraft: true,
    };

    // Include patient/cycle context in draft too
    if (this.patientId) request.patientId = this.patientId;
    if (this.cycleId) request.cycleId = this.cycleId;

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
    const calcFields = this.fields.filter((f) => f.fieldType === FieldType.Calculated);
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

        const displayValue = isNaN(result)
          ? 'N/A'
          : `${Number(result).toFixed(decimalPlaces)}${unit ? ' ' + unit : ''}`;

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

  // (Signature, Lookup, Repeater, Tags/Mention helpers moved to FieldInputComponent)
}
