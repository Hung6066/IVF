import { Component, Input, Output, EventEmitter, HostBinding } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FieldType, FormField } from '../../forms.service';
import { getFieldColSpan, getFieldHeight, parseFieldOptions } from '../../shared/form-field.utils';

@Component({
  selector: 'app-field-preview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './field-preview.component.html',
  styleUrls: ['./field-preview.component.scss'],
})
export class FieldPreviewComponent {
  @Input({ required: true }) field!: FormField;
  @Input() isSelected = false;

  @HostBinding('class.selected') get selectedClass() {
    return this.isSelected;
  }

  @Output() selected = new EventEmitter<void>();
  @Output() deleted = new EventEmitter<void>();
  @Output() cloned = new EventEmitter<void>();

  FieldType = FieldType;

  get colSpan(): number {
    return getFieldColSpan(this.field);
  }

  get fieldHeight(): string {
    return getFieldHeight(this.field);
  }

  getOptions(): { value: string; label: string }[] {
    return parseFieldOptions(this.field.optionsJson);
  }

  getSubFields(): { key: string; label: string; width: number }[] {
    try {
      const subs = this.field.optionsJson ? JSON.parse(this.field.optionsJson) : [];
      return Array.isArray(subs) && subs.length > 0 && subs[0].key ? subs : [];
    } catch {
      return [];
    }
  }

  onDelete(event: Event): void {
    event.stopPropagation();
    this.deleted.emit();
  }

  onClone(event: Event): void {
    event.stopPropagation();
    this.cloned.emit();
  }
}
