import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnChanges,
  SimpleChanges,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  FormsService,
  FormTemplate,
  FormField,
  LinkedFieldSource,
  CreateLinkedFieldSourceRequest,
  FieldType,
  FieldTypeLabels,
} from '../forms.service';

@Component({
  selector: 'app-linked-field-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './linked-field-config.component.html',
  styleUrls: ['./linked-field-config.component.scss'],
})
export class LinkedFieldConfigComponent implements OnInit, OnChanges {
  private formsService = inject(FormsService);

  @Input() templateId = '';
  @Input() fields: FormField[] = [];
  @Input() isOpen = false;
  @Output() closed = new EventEmitter<void>();

  // Existing links
  linkedSources: LinkedFieldSource[] = [];

  // All templates for source selection
  allTemplates: FormTemplate[] = [];
  sourceFields: FormField[] = [];

  // New link form
  showAddForm = false;
  newLink = {
    targetFieldId: '',
    sourceTemplateId: '',
    sourceFieldId: '',
    flowType: 2, // Suggest
    priority: 0,
    description: '',
  };

  // Edit mode
  editingLinkId: string | null = null;

  // Loading state
  isLoading = false;
  isSaving = false;

  // Flow type options
  flowTypes = [
    { value: 1, label: 'Tự động điền', icon: '⚡', desc: 'Auto-fill - tự ghi vào form' },
    { value: 2, label: 'Gợi ý', icon: '💡', desc: 'Suggest - hiện gợi ý, user xác nhận' },
    { value: 3, label: 'Tham chiếu', icon: '👁️', desc: 'Reference - chỉ hiển thị, không sửa' },
    { value: 4, label: 'Sao chép', icon: '📋', desc: 'Copy - sao chép khi submit' },
  ];

  // Field type labels for display
  fieldTypeLabels = FieldTypeLabels;

  ngOnInit() {
    this.loadTemplates();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['isOpen'] && this.isOpen && this.templateId) {
      this.loadLinkedSources();
    }
  }

  loadTemplates() {
    this.formsService.getTemplates(undefined, false, true).subscribe({
      next: (templates) => {
        this.allTemplates = templates;
      },
    });
  }

  loadLinkedSources() {
    if (!this.templateId) return;
    this.isLoading = true;
    this.formsService.getLinkedFieldSources(this.templateId).subscribe({
      next: (sources) => {
        this.linkedSources = sources;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  onSourceTemplateChange() {
    this.newLink.sourceFieldId = '';
    this.sourceFields = [];
    if (!this.newLink.sourceTemplateId) return;

    const tmpl = this.allTemplates.find((t) => t.id === this.newLink.sourceTemplateId);
    if (tmpl?.fields) {
      this.sourceFields = tmpl.fields.filter((f) => this.isDataField(f.fieldType));
    } else {
      // Load fields from API if not included
      this.formsService.getFieldsByTemplate(this.newLink.sourceTemplateId).subscribe({
        next: (fields) => {
          this.sourceFields = fields.filter((f) => this.isDataField(f.fieldType));
        },
      });
    }
  }

  isDataField(fieldType: FieldType): boolean {
    return (
      fieldType !== FieldType.Section &&
      fieldType !== FieldType.Label &&
      fieldType !== FieldType.PageBreak
    );
  }

  getTargetFields(): FormField[] {
    return this.fields.filter((f) => this.isDataField(f.fieldType));
  }

  /** Fields that have a conceptId → auto-linked across forms */
  getConceptLinkedFields(): FormField[] {
    return this.fields.filter((f) => f.conceptId && this.isDataField(f.fieldType));
  }

  /** Fields WITHOUT conceptId that need manual configuration */
  getFieldsNeedingConfig(): FormField[] {
    return this.fields.filter((f) => !f.conceptId && this.isDataField(f.fieldType));
  }

  getAvailableSourceTemplates(): FormTemplate[] {
    // Exclude the current template
    return this.allTemplates.filter((t) => t.id !== this.templateId);
  }

  getFieldTypeLabel(fieldType: FieldType): string {
    return this.fieldTypeLabels[fieldType] || 'Khác';
  }

  getFlowTypeLabel(flowType: number): string {
    return this.flowTypes.find((f) => f.value === flowType)?.label || 'Gợi ý';
  }

  getFlowTypeIcon(flowType: number): string {
    return this.flowTypes.find((f) => f.value === flowType)?.icon || '💡';
  }

  // Group links by target field for display
  getLinkedSourcesByField(): {
    fieldId: string;
    fieldLabel: string;
    sources: LinkedFieldSource[];
  }[] {
    const groups = new Map<
      string,
      { fieldId: string; fieldLabel: string; sources: LinkedFieldSource[] }
    >();

    for (const source of this.linkedSources) {
      if (!groups.has(source.targetFieldId)) {
        groups.set(source.targetFieldId, {
          fieldId: source.targetFieldId,
          fieldLabel: source.targetFieldLabel,
          sources: [],
        });
      }
      groups.get(source.targetFieldId)!.sources.push(source);
    }

    return Array.from(groups.values());
  }

  openAddForm() {
    this.showAddForm = true;
    this.editingLinkId = null;
    this.newLink = {
      targetFieldId: '',
      sourceTemplateId: '',
      sourceFieldId: '',
      flowType: 2,
      priority: 0,
      description: '',
    };
    this.sourceFields = [];
  }

  cancelAdd() {
    this.showAddForm = false;
    this.editingLinkId = null;
  }

  saveLink() {
    if (
      !this.newLink.targetFieldId ||
      !this.newLink.sourceTemplateId ||
      !this.newLink.sourceFieldId
    ) {
      return;
    }

    this.isSaving = true;

    if (this.editingLinkId) {
      // Update existing link
      this.formsService
        .updateLinkedFieldSource(this.editingLinkId, {
          sourceTemplateId: this.newLink.sourceTemplateId,
          sourceFieldId: this.newLink.sourceFieldId,
          flowType: this.newLink.flowType,
          priority: this.newLink.priority,
          description: this.newLink.description || undefined,
        })
        .subscribe({
          next: () => {
            this.isSaving = false;
            this.showAddForm = false;
            this.editingLinkId = null;
            this.loadLinkedSources();
          },
          error: (err) => {
            this.isSaving = false;
            alert('Lỗi: ' + (err.error || 'Không thể cập nhật liên kết'));
          },
        });
    } else {
      // Create new link
      const request: CreateLinkedFieldSourceRequest = {
        targetFieldId: this.newLink.targetFieldId,
        sourceTemplateId: this.newLink.sourceTemplateId,
        sourceFieldId: this.newLink.sourceFieldId,
        flowType: this.newLink.flowType,
        priority: this.newLink.priority,
        description: this.newLink.description || undefined,
      };

      this.formsService.createLinkedFieldSource(request).subscribe({
        next: () => {
          this.isSaving = false;
          this.showAddForm = false;
          this.loadLinkedSources();
        },
        error: (err) => {
          this.isSaving = false;
          alert('Lỗi: ' + (err.error || 'Không thể tạo liên kết'));
        },
      });
    }
  }

  editLink(source: LinkedFieldSource) {
    this.editingLinkId = source.id;
    this.showAddForm = true;
    this.newLink = {
      targetFieldId: source.targetFieldId,
      sourceTemplateId: source.sourceTemplateId,
      sourceFieldId: source.sourceFieldId,
      flowType: source.flowType,
      priority: source.priority,
      description: source.description || '',
    };
    // Load source fields for the selected template
    this.onSourceTemplateChange();
  }

  deleteLink(source: LinkedFieldSource) {
    if (!confirm(`Xóa liên kết "${source.sourceFieldLabel}" → "${source.targetFieldLabel}"?`))
      return;

    this.formsService.deleteLinkedFieldSource(source.id).subscribe({
      next: () => {
        this.loadLinkedSources();
      },
      error: (err) => {
        alert('Lỗi: ' + (err.error || 'Không thể xóa liên kết'));
      },
    });
  }

  close() {
    this.isOpen = false;
    this.showAddForm = false;
    this.editingLinkId = null;
    this.closed.emit();
  }
}
