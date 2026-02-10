import { Component, Output, EventEmitter, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FieldType, FormCategory } from '../../forms.service';

export interface FieldTypeOption {
  type: FieldType;
  icon: string;
  label: string;
}

@Component({
  selector: 'app-field-palette',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './field-palette.component.html',
  styleUrls: ['./field-palette.component.scss'],
})
export class FieldPaletteComponent {
  @Input() categories: FormCategory[] = [];
  @Input() selectedCategoryId = '';
  @Output() fieldAdded = new EventEmitter<FieldType>();
  @Output() fieldDragStart = new EventEmitter<{ event: DragEvent; type: FieldType }>();
  @Output() categoryChanged = new EventEmitter<string>();

  fieldTypes: FieldTypeOption[] = [
    { type: FieldType.Text, icon: '📝', label: 'Văn bản' },
    { type: FieldType.TextArea, icon: '📄', label: 'Văn bản dài' },
    { type: FieldType.Number, icon: '🔢', label: 'Số nguyên' },
    { type: FieldType.Decimal, icon: '🔣', label: 'Số thập phân' },
    { type: FieldType.Date, icon: '📅', label: 'Ngày' },
    { type: FieldType.DateTime, icon: '📅⏰', label: 'Ngày giờ' },
    { type: FieldType.Time, icon: '⏰', label: 'Giờ' },
    { type: FieldType.Dropdown, icon: '📋', label: 'Dropdown' },
    { type: FieldType.MultiSelect, icon: '☑📋', label: 'Chọn nhiều' },
    { type: FieldType.Radio, icon: '⭕', label: 'Radio' },
    { type: FieldType.Checkbox, icon: '☑️', label: 'Checkbox' },
    { type: FieldType.Tags, icon: '🏷️', label: 'Tags' },
    { type: FieldType.Rating, icon: '⭐', label: 'Đánh giá' },
    { type: FieldType.Section, icon: '➖', label: 'Phân đoạn' },
    { type: FieldType.Label, icon: '🏷', label: 'Nhãn' },
    { type: FieldType.FileUpload, icon: '📎', label: 'Tải file' },
    { type: FieldType.PageBreak, icon: '📄', label: 'Ngắt trang' },
    { type: FieldType.Address, icon: '🏠', label: 'Địa chỉ' },
    { type: FieldType.Hidden, icon: '👁️‍🗨️', label: 'Ẩn' },
    { type: FieldType.Slider, icon: '🖊️', label: 'Thanh trượt' },
    { type: FieldType.Calculated, icon: '🧮', label: 'Tính toán' },
    { type: FieldType.RichText, icon: '📝', label: 'Rich Text' },
    { type: FieldType.Signature, icon: '✍️', label: 'Chữ ký' },
    { type: FieldType.Lookup, icon: '🔍', label: 'Tra cứu' },
    { type: FieldType.Repeater, icon: '🔁', label: 'Lặp nhóm' },
  ];

  onAddField(type: FieldType) {
    this.fieldAdded.emit(type);
  }

  onDragStart(event: DragEvent, type: FieldType) {
    this.fieldDragStart.emit({ event, type });
    event.dataTransfer?.setData('fieldType', type.toString());
  }

  onCategoryChange(event: Event) {
    this.categoryChanged.emit((event.target as HTMLSelectElement).value);
  }
}
