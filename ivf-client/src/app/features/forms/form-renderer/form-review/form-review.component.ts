import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup } from '@angular/forms';
import { FormsService, FormField, FieldType } from '../../forms.service';
import { getSliderConfig } from '../../shared/form-field.utils';

@Component({
  selector: 'app-form-review',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './form-review.component.html',
  styleUrls: ['./form-review.component.scss'],
})
export class FormReviewComponent {
  private readonly formsService = inject(FormsService);

  @Input() fields: FormField[] = [];
  @Input() form!: FormGroup;
  @Input() fileValues: { [key: string]: File } = {};

  @Output() editClicked = new EventEmitter<void>();

  getReviewFields(): FormField[] {
    return this.fields.filter(
      (f) =>
        f.fieldType !== FieldType.Section &&
        f.fieldType !== FieldType.Label &&
        f.fieldType !== FieldType.PageBreak &&
        f.fieldType !== FieldType.Hidden,
    );
  }

  getReviewDisplayValue(field: FormField): string {
    const value = this.form.get(field.id)?.value;
    if (value === null || value === undefined || value === '') return '—';

    const opts = () => this.formsService.parseOptions(field.optionsJson);

    switch (field.fieldType) {
      case FieldType.Dropdown:
      case FieldType.Radio: {
        const opt = opts().find((o: any) => o.value === value);
        return opt?.label ?? value;
      }
      case FieldType.Checkbox: {
        const options = opts();
        if (options.length > 0 && Array.isArray(value)) {
          return value
            .map((v: string) => {
              const opt = options.find((o: any) => o.value === v);
              return opt?.label ?? v;
            })
            .join(', ');
        }
        return typeof value === 'boolean' ? (value ? 'Có' : 'Không') : String(value);
      }
      case FieldType.MultiSelect: {
        if (!Array.isArray(value)) return '—';
        const options = opts();
        return value
          .map((v: string) => {
            const opt = options.find((o: any) => o.value === v);
            return opt?.label ?? v;
          })
          .join(', ');
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
        const config = getSliderConfig(field);
        return `${value}${config.unit ? ' ' + config.unit : ''}`;
      }
      case FieldType.Signature:
        return value ? '✍️ Đã ký' : '—';
      case FieldType.Calculated:
        return this.form.get(field.id)?.value || '—';
      default:
        return String(value);
    }
  }

  onEdit() {
    this.editClicked.emit();
  }
}
