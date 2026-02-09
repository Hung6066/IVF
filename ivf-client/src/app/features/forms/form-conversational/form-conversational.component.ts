import { Component, OnInit, OnDestroy, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormControl,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  FormsService,
  FormTemplate,
  FormField,
  FieldType,
  FieldValueRequest,
  FieldValueDetailRequest,
} from '../forms.service';
import { lastValueFrom } from 'rxjs';

interface ConversationalField {
  field: FormField;
  control: FormControl | FormGroup;
  answered: boolean;
  answer?: string; // Display text for the answer bubble
}

@Component({
  selector: 'app-form-conversational',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './form-conversational.component.html',
  styleUrls: ['./form-conversational.component.scss'],
})
export class FormConversationalComponent implements OnInit, OnDestroy {
  private readonly formsService = inject(FormsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  FieldType = FieldType;
  templateId = '';
  template: FormTemplate | null = null;
  allFields: FormField[] = [];
  chatFields: ConversationalField[] = [];
  currentIndex = 0;
  isSubmitting = false;
  isComplete = false;
  showReview = false;
  animationState: 'entering' | 'active' | 'exiting' = 'active';
  fileValues: { [key: string]: File } = {};

  // Keyboard shortcut hint
  showKeyHint = true;

  get currentChatField(): ConversationalField | null {
    return this.chatFields[this.currentIndex] ?? null;
  }

  get progress(): number {
    if (this.chatFields.length === 0) return 0;
    if (this.showReview) return 100;
    return Math.round((this.currentIndex / this.chatFields.length) * 100);
  }

  get answeredFields(): ConversationalField[] {
    return this.chatFields.filter((cf) => cf.answered);
  }

  ngOnInit() {
    this.route.params.subscribe((params) => {
      if (params['id']) {
        this.templateId = params['id'];
        this.loadTemplate();
      }
    });
  }

  ngOnDestroy() {}

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      const currentField = this.currentChatField;
      if (!currentField) return;

      // Don't capture Enter for multi-line fields
      const ft = currentField.field.fieldType;
      if (ft === FieldType.TextArea) return;

      // Don't capture if reviewing
      if (this.showReview) return;

      event.preventDefault();
      this.next();
    }

    if (event.key === 'Escape') {
      this.cancel();
    }
  }

  loadTemplate() {
    this.formsService.getTemplateById(this.templateId).subscribe((template) => {
      this.template = template;
      this.allFields = (template.fields || [])
        .map((f) => ({ ...f, fieldType: this.normalizeFieldType(f.fieldType) }))
        .sort((a, b) => a.displayOrder - b.displayOrder);

      this.buildConversationalFields();
    });
  }

  normalizeFieldType(type: FieldType | string): FieldType {
    if ((type as any) === 0 || type === '0') return FieldType.Text;
    if (typeof type === 'number') return type;
    const parsed = Number(type);
    if (!isNaN(parsed) && parsed !== 0) return parsed as FieldType;

    const map: { [key: string]: FieldType } = {
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
    return map[type as string] ?? FieldType.Text;
  }

  buildConversationalFields() {
    this.chatFields = [];

    for (const field of this.allFields) {
      // Skip non-interactive fields
      if (
        field.fieldType === FieldType.Section ||
        field.fieldType === FieldType.Label ||
        field.fieldType === FieldType.PageBreak
      ) {
        continue;
      }

      const validators = [];
      if (field.isRequired) validators.push(Validators.required);

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

      let control: FormControl | FormGroup;

      if (field.fieldType === FieldType.Address) {
        const subFields = this.getAddressSubFields(field);
        const subGroup: { [key: string]: FormControl } = {};
        for (const sub of subFields) {
          subGroup[sub.key] = new FormControl('', sub.required ? [Validators.required] : []);
        }
        control = new FormGroup(subGroup);
      } else {
        let defaultValue: any = field.defaultValue || '';
        if (field.fieldType === FieldType.Checkbox) {
          defaultValue = [];
        } else if (field.fieldType === FieldType.Number || field.fieldType === FieldType.Decimal) {
          defaultValue = field.defaultValue ? parseFloat(field.defaultValue) : null;
        } else if (
          field.fieldType === FieldType.MultiSelect ||
          field.fieldType === FieldType.Tags
        ) {
          defaultValue = [];
        }
        control = new FormControl(defaultValue, validators);
      }

      this.chatFields.push({
        field,
        control,
        answered: false,
      });
    }

    // Auto-hide key hint after 5s
    setTimeout(() => (this.showKeyHint = false), 5000);
  }

  getOptions(field: FormField): { value: string; label: string }[] {
    return this.formsService.parseOptions(field.optionsJson);
  }

  getAddressSubFields(
    field: FormField,
  ): { key: string; label: string; type: string; required: boolean; width: number }[] {
    try {
      const subs = field.optionsJson ? JSON.parse(field.optionsJson) : [];
      if (Array.isArray(subs) && subs.length > 0 && subs[0].key) return subs;
    } catch {}
    return [
      { key: 'street', label: 'Đường / Số nhà', type: 'text', required: true, width: 100 },
      { key: 'ward', label: 'Phường/Xã', type: 'text', required: false, width: 50 },
      { key: 'district', label: 'Quận/Huyện', type: 'text', required: false, width: 50 },
      { key: 'province', label: 'Tỉnh/Thành phố', type: 'text', required: true, width: 50 },
      { key: 'country', label: 'Quốc gia', type: 'text', required: false, width: 50 },
    ];
  }

  getDisplayAnswer(cf: ConversationalField): string {
    const value = cf.control.value;
    const ft = cf.field.fieldType;

    if (value === null || value === undefined || value === '') return '(bỏ trống)';

    switch (ft) {
      case FieldType.Dropdown:
      case FieldType.Radio: {
        const opts = this.getOptions(cf.field);
        const opt = opts.find((o) => o.value === value);
        return opt?.label ?? value;
      }
      case FieldType.Checkbox:
      case FieldType.MultiSelect: {
        if (Array.isArray(value) && value.length > 0) {
          const opts = this.getOptions(cf.field);
          return value
            .map((v: string) => {
              const opt = opts.find((o) => o.value === v);
              return opt?.label ?? v;
            })
            .join(', ');
        }
        return typeof value === 'boolean' ? (value ? 'Có' : 'Không') : '(bỏ trống)';
      }
      case FieldType.Rating:
        return '⭐'.repeat(value);
      case FieldType.Date:
        return value ? new Date(value).toLocaleDateString('vi-VN') : '(bỏ trống)';
      case FieldType.DateTime:
        return value ? new Date(value).toLocaleString('vi-VN') : '(bỏ trống)';
      case FieldType.Address: {
        const parts = Object.values(value || {}).filter((v) => v) as string[];
        return parts.length > 0 ? parts.join(', ') : '(bỏ trống)';
      }
      case FieldType.FileUpload:
        return this.fileValues[cf.field.id]?.name ?? '(chưa chọn file)';
      default:
        return String(value);
    }
  }

  // === Navigation ===
  next() {
    const current = this.currentChatField;
    if (!current) return;

    // Validate
    if (current.control instanceof FormControl) {
      current.control.markAsTouched();
      if (current.control.invalid) return;
    } else if (current.control instanceof FormGroup) {
      Object.values(current.control.controls).forEach((c) => c.markAsTouched());
      if (current.control.invalid) return;
    }

    // Mark answered
    current.answered = true;
    current.answer = this.getDisplayAnswer(current);

    // Animate transition
    this.animationState = 'exiting';
    setTimeout(() => {
      if (this.currentIndex < this.chatFields.length - 1) {
        this.currentIndex++;
        this.animationState = 'entering';
        setTimeout(() => {
          this.animationState = 'active';
          this.focusCurrentInput();
        }, 50);
      } else {
        // All fields answered — show review
        this.showReview = true;
        this.animationState = 'active';
      }
    }, 300);
  }

  prev() {
    if (this.showReview) {
      this.showReview = false;
      this.animationState = 'active';
      setTimeout(() => this.focusCurrentInput(), 100);
      return;
    }
    if (this.currentIndex > 0) {
      this.animationState = 'exiting';
      setTimeout(() => {
        this.currentIndex--;
        this.chatFields[this.currentIndex].answered = false;
        this.animationState = 'entering';
        setTimeout(() => {
          this.animationState = 'active';
          this.focusCurrentInput();
        }, 50);
      }, 300);
    }
  }

  goToField(index: number) {
    if (index < 0 || index >= this.chatFields.length) return;
    this.showReview = false;
    this.currentIndex = index;
    // Un-mark subsequent fields
    for (let i = index; i < this.chatFields.length; i++) {
      this.chatFields[i].answered = false;
    }
    this.animationState = 'active';
    setTimeout(() => this.focusCurrentInput(), 100);
  }

  private focusCurrentInput() {
    setTimeout(() => {
      const el = document.querySelector(
        '.conv-field-input input, .conv-field-input textarea, .conv-field-input select',
      ) as HTMLElement;
      el?.focus();
    }, 100);
  }

  // === Checkbox helpers ===
  isCheckboxChecked(cf: ConversationalField, optValue: string): boolean {
    const val = cf.control.value;
    return Array.isArray(val) && val.includes(optValue);
  }

  onCheckboxChange(cf: ConversationalField, optValue: string, event: Event) {
    event.preventDefault();
    const ctrl = cf.control as FormControl;
    let current = ctrl.value || [];
    if (!Array.isArray(current)) current = [];

    if (current.includes(optValue)) {
      ctrl.setValue(current.filter((v: string) => v !== optValue));
    } else {
      ctrl.setValue([...current, optValue]);
    }
  }

  setRating(cf: ConversationalField, value: number) {
    (cf.control as FormControl).setValue(value);
  }

  onFileChange(event: Event, cf: ConversationalField) {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) {
      this.fileValues[cf.field.id] = input.files[0];
      (cf.control as FormControl).setValue(input.files[0].name);
    }
  }

  hasError(cf: ConversationalField): boolean {
    if (cf.control instanceof FormControl) {
      return cf.control.invalid && cf.control.touched;
    }
    return cf.control.invalid && Object.values(cf.control.controls).some((c) => c.touched);
  }

  getErrors(cf: ConversationalField): string[] {
    const ctrl = cf.control;
    const errors: string[] = [];
    if (ctrl instanceof FormControl) {
      if (!ctrl.errors || !ctrl.touched) return [];
      if (ctrl.errors['required']) errors.push('Trường này là bắt buộc');
      if (ctrl.errors['minlength'])
        errors.push(`Tối thiểu ${ctrl.errors['minlength'].requiredLength} ký tự`);
      if (ctrl.errors['maxlength'])
        errors.push(`Tối đa ${ctrl.errors['maxlength'].requiredLength} ký tự`);
      if (ctrl.errors['min']) errors.push(`Giá trị tối thiểu là ${ctrl.errors['min'].min}`);
      if (ctrl.errors['max']) errors.push(`Giá trị tối đa là ${ctrl.errors['max'].max}`);
      if (ctrl.errors['pattern']) errors.push('Giá trị không đúng định dạng');
      if (ctrl.errors['email']) errors.push('Email không hợp lệ');
    }
    return errors;
  }

  // === Submission ===
  async submit() {
    this.isSubmitting = true;
    const fieldValues: FieldValueRequest[] = [];

    for (const cf of this.chatFields) {
      const field = cf.field;
      const value = cf.control.value;
      const fv: FieldValueRequest = { formFieldId: field.id };
      const details: FieldValueDetailRequest[] = [];

      switch (field.fieldType) {
        case FieldType.Number:
        case FieldType.Decimal:
        case FieldType.Rating:
          fv.numericValue = value != null ? parseFloat(value) : undefined;
          break;
        case FieldType.Date:
        case FieldType.DateTime:
          fv.dateValue = value ? new Date(value) : undefined;
          break;
        case FieldType.Checkbox:
        case FieldType.MultiSelect: {
          fv.jsonValue = JSON.stringify(value || []);
          const opts = this.getOptions(field);
          if (Array.isArray(value)) {
            value.forEach((v: string) => {
              const opt = opts.find((o) => o.value === v);
              details.push({
                value: opt?.value ?? v,
                label: opt?.label ?? v,
                conceptId: (opt as any)?.conceptId,
              });
            });
          }
          break;
        }
        case FieldType.Dropdown:
        case FieldType.Radio: {
          fv.textValue = value?.toString() || '';
          if (value) {
            const opts = this.getOptions(field);
            const opt = opts.find((o) => o.value === value);
            if (opt)
              details.push({
                value: opt.value,
                label: opt.label,
                conceptId: (opt as any)?.conceptId,
              });
          }
          break;
        }
        case FieldType.FileUpload: {
          const file = this.fileValues[field.id];
          if (file) {
            try {
              const uploadResult = await lastValueFrom(this.formsService.uploadFile(file));
              fv.textValue = uploadResult.url;
              fv.jsonValue = JSON.stringify(uploadResult);
            } catch {
              this.isSubmitting = false;
              alert(`Lỗi tải file cho trường "${field.label}"`);
              return;
            }
          }
          break;
        }
        case FieldType.Address: {
          fv.jsonValue = JSON.stringify(value);
          const parts = Object.values(value || {}).filter((v) => v) as string[];
          fv.textValue = parts.join(', ');
          break;
        }
        case FieldType.Tags: {
          fv.jsonValue = JSON.stringify(value || []);
          break;
        }
        default:
          fv.textValue = value?.toString() || '';
      }

      if (details.length > 0) fv.details = details;
      fieldValues.push(fv);
    }

    const request = {
      formTemplateId: this.templateId,
      submittedByUserId: null as string | null,
      fieldValues,
    };

    this.formsService.submitResponse(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.isComplete = true;
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error || 'Có lỗi xảy ra, vui lòng thử lại.';
        alert(typeof msg === 'string' ? msg : JSON.stringify(msg));
      },
    });
  }

  cancel() {
    this.router.navigate(['/forms']);
  }

  goToForms() {
    this.router.navigate(['/forms']);
  }

  goToResponses() {
    this.router.navigate(['/forms/responses']);
  }
}
