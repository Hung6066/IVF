import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AmendmentService } from '../../../core/services/amendment.service';
import { FormFieldValue, FormField } from '../forms.service';
import { CreateAmendmentRequest, FieldChangeRequest } from '../../../core/models/amendment.models';

export interface EditableField {
  field: FormField;
  currentValue: FormFieldValue | null;
  newTextValue?: string;
  newNumericValue?: number;
  newDateValue?: string;
  newBooleanValue?: boolean;
  newJsonValue?: string;
  isChanged: boolean;
}

@Component({
  selector: 'app-amendment-request',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './amendment-request.component.html',
  styleUrls: ['./amendment-request.component.scss'],
})
export class AmendmentRequestComponent {
  @Input() formResponseId!: string;
  @Input() fields: FormField[] = [];
  @Input() fieldValues: FormFieldValue[] = [];
  @Output() closed = new EventEmitter<void>();
  @Output() submitted = new EventEmitter<void>();

  reason = '';
  editableFields: EditableField[] = [];
  submitting = signal(false);
  error = signal<string | null>(null);

  constructor(private amendmentService: AmendmentService) {}

  ngOnInit() {
    this.editableFields = this.fields.map((field) => {
      const currentValue = this.fieldValues.find((fv) => fv.formFieldId === field.id) || null;
      return {
        field,
        currentValue,
        newTextValue: currentValue?.textValue ?? undefined,
        newNumericValue: currentValue?.numericValue ?? undefined,
        newDateValue: currentValue?.dateValue
          ? this.formatDateForInput(currentValue.dateValue)
          : undefined,
        newBooleanValue: currentValue?.booleanValue ?? undefined,
        newJsonValue: currentValue?.jsonValue ?? undefined,
        isChanged: false,
      };
    });
  }

  markChanged(ef: EditableField) {
    const cv = ef.currentValue;
    ef.isChanged =
      ef.newTextValue !== (cv?.textValue ?? undefined) ||
      ef.newNumericValue !== (cv?.numericValue ?? undefined) ||
      ef.newDateValue !== (cv?.dateValue ? this.formatDateForInput(cv.dateValue) : undefined) ||
      ef.newBooleanValue !== (cv?.booleanValue ?? undefined) ||
      ef.newJsonValue !== (cv?.jsonValue ?? undefined);
  }

  getChangedFields(): EditableField[] {
    return this.editableFields.filter((ef) => ef.isChanged);
  }

  submit() {
    const changedFields = this.getChangedFields();

    if (!this.reason.trim()) {
      this.error.set('Vui lòng nhập lý do chỉnh sửa.');
      return;
    }

    if (changedFields.length === 0) {
      this.error.set('Chưa có thay đổi nào. Vui lòng sửa ít nhất một trường.');
      return;
    }

    this.error.set(null);
    this.submitting.set(true);

    const fieldChanges: FieldChangeRequest[] = changedFields.map((ef) => ({
      formFieldId: ef.field.id,
      newTextValue: ef.newTextValue,
      newNumericValue: ef.newNumericValue,
      newDateValue: ef.newDateValue,
      newBooleanValue: ef.newBooleanValue,
      newJsonValue: ef.newJsonValue,
    }));

    const request: CreateAmendmentRequest = {
      reason: this.reason.trim(),
      fieldChanges,
    };

    this.amendmentService.createAmendment(this.formResponseId, request).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitted.emit();
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(err.error?.error || 'Không thể tạo yêu cầu chỉnh sửa.');
      },
    });
  }

  close() {
    this.closed.emit();
  }

  private formatDateForInput(date: Date | string): string {
    const d = new Date(date);
    return d.toISOString().split('T')[0];
  }

  getCurrentDisplayValue(ef: EditableField): string {
    const cv = ef.currentValue;
    if (!cv) return '(trống)';
    if (cv.textValue) return cv.textValue;
    if (cv.numericValue != null) return cv.numericValue.toString();
    if (cv.dateValue) return new Date(cv.dateValue).toLocaleDateString('vi-VN');
    if (cv.booleanValue != null) return cv.booleanValue ? 'Có' : 'Không';
    if (cv.jsonValue) {
      try {
        const parsed = JSON.parse(cv.jsonValue);
        if (Array.isArray(parsed)) {
          return parsed.map((item: any) => item.label || item.value || item).join(', ');
        }
        return JSON.stringify(parsed);
      } catch {
        return cv.jsonValue;
      }
    }
    return '(trống)';
  }
}
