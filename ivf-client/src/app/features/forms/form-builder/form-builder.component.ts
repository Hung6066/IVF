import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormsService, FormCategory, FormTemplate, FormField, FieldType, FieldTypeLabels, CreateFieldRequest } from '../forms.service';

@Component({
    selector: 'app-form-builder',
    standalone: true,
    imports: [CommonModule, FormsModule, DragDropModule],
    template: `
        <div class="form-builder-container">
            <!-- Header -->
            <header class="builder-header">
                <div class="header-left">
                    <button class="back-btn" (click)="goBack()">← Quay lại</button>
                    <input 
                        type="text" 
                        class="form-name-input"
                        [(ngModel)]="formName"
                        placeholder="Tên biểu mẫu..."
                        (blur)="saveFormSettings()">
                </div>
                <div class="header-right">
                    <button class="btn btn-secondary" (click)="preview()">👁️ Xem trước</button>
                    @if (templateId && !template?.isPublished) {
                        <button class="btn btn-success" (click)="publish()">📤 Xuất bản</button>
                    }
                    <button class="btn btn-primary" (click)="save()">💾 Lưu</button>
                </div>
            </header>

            <div class="builder-content">
                <!-- Field Palette -->
                <aside class="field-palette">
                    <h3>Các loại trường</h3>
                    <div class="field-types">
                        @for (fieldType of fieldTypes; track fieldType.type) {
                            <button 
                                class="field-type-btn"
                                draggable="true"
                                (dragstart)="onDragStart($event, fieldType.type)"
                                (click)="addField(fieldType.type)">
                                <span class="field-icon">{{ fieldType.icon }}</span>
                                <span>{{ fieldType.label }}</span>
                            </button>
                        }
                    </div>

                    <div class="palette-section">
                        <h4>Cài đặt biểu mẫu</h4>
                        <div class="form-group">
                            <label>Danh mục</label>
                            <select [(ngModel)]="selectedCategoryId" (change)="saveFormSettings()">
                                @for (cat of categories; track cat.id) {
                                    <option [value]="cat.id">{{ cat.name }}</option>
                                }
                            </select>
                        </div>
                        <div class="form-group">
                            <label>Mô tả</label>
                            <textarea 
                                [(ngModel)]="formDescription" 
                                placeholder="Mô tả biểu mẫu..."
                                (blur)="saveFormSettings()"></textarea>
                        </div>
                    </div>
                </aside>

                <!-- Form Canvas -->
                <main class="form-canvas">
                    <div class="canvas-header">
                        <h2>{{ formName || 'Biểu mẫu mới' }}</h2>
                        <p>{{ formDescription || 'Kéo thả các trường từ bên trái để xây dựng biểu mẫu' }}</p>
                    </div>

                    <div 
                        class="fields-container grid-layout"
                        cdkDropList
                        (cdkDropListDropped)="onDropField($event)"
                        (dragover)="onDragOver($event)"
                        (drop)="onDrop($event)">
                        
                        @if (fields.length === 0) {
                            <div class="empty-canvas">
                                <div class="drop-zone">
                                    <span class="drop-icon">📥</span>
                                    <p>Kéo trường vào đây hoặc click vào loại trường bên trái</p>
                                </div>
                            </div>
                        }

                        @for (field of fields; track field.id || field.fieldKey; let i = $index) {
                            <div 
                                class="field-item"
                                cdkDrag
                                [style.grid-column]="'span ' + getFieldColSpan(field)"
                                [class.selected]="selectedField?.id === field.id || selectedField?.fieldKey === field.fieldKey"
                                [class.col-1]="getFieldColSpan(field) === 1"
                                [class.col-2]="getFieldColSpan(field) === 2"
                                [class.col-3]="getFieldColSpan(field) === 3"
                                [class.col-4]="getFieldColSpan(field) === 4"
                                (click)="selectField(field)">
                                <div class="field-header">
                                    <div class="drag-handle" cdkDragHandle>⋮⋮</div>
                                    <span class="col-indicator" title="Chiều rộng cột">{{ getFieldColSpan(field) }}/4</span>
                                    <button class="delete-btn" (click)="deleteField(field, $event)">🗑️</button>
                                </div>
                                <div class="field-preview">
                                    <label>
                                        {{ field.label }}
                                        @if (field.isRequired) {
                                            <span class="required">*</span>
                                        }
                                    </label>
                                    @switch (field.fieldType) {
                                        @case (FieldType.Text) {
                                            <input 
                                                type="text" 
                                                [placeholder]="field.placeholder || ''" 
                                                [style.height]="getFieldHeight(field)"
                                                disabled>
                                        }
                                        @case (FieldType.TextArea) {
                                            <textarea 
                                                [placeholder]="field.placeholder || ''" 
                                                [style.height]="getFieldHeight(field)"
                                                disabled></textarea>
                                        }
                                        @case (FieldType.Number) {
                                            <input type="number" [placeholder]="field.placeholder || ''" disabled>
                                        }
                                        @case (FieldType.Date) {
                                            <input type="date" disabled>
                                        }
                                        @case (FieldType.Dropdown) {
                                            <select disabled>
                                                <option>-- Chọn --</option>
                                            </select>
                                        }
                                        @case (FieldType.Radio) {
                                            <div class="radio-preview">
                                                @for (opt of getOptions(field); track opt.value) {
                                                    <label><input type="radio" disabled> {{ opt.label }}</label>
                                                }
                                            </div>
                                        }
                                        @case (FieldType.Checkbox) {
                                            <label class="checkbox-preview">
                                                <input type="checkbox" disabled>
                                                <span>{{ field.placeholder || 'Checkbox' }}</span>
                                            </label>
                                        }
                                        @case (FieldType.Rating) {
                                            <div class="rating-preview">⭐⭐⭐⭐⭐</div>
                                        }
                                        @case (FieldType.Section) {
                                            <hr class="section-divider">
                                        }
                                        @default {
                                            <input type="text" disabled [placeholder]="getFieldTypeLabel(field.fieldType)">
                                        }
                                    }
                                    @if (field.helpText) {
                                        <small class="help-text">{{ field.helpText }}</small>
                                    }
                                </div>
                            </div>
                        }
                    </div>
                </main>

                <!-- Field Properties Panel -->
                @if (selectedField) {
                    <aside class="properties-panel">
                        <div class="panel-header">
                            <h3>Thuộc tính trường</h3>
                            <button class="close-btn" (click)="selectedField = null">✕</button>
                        </div>

                        <div class="panel-body">
                            <div class="form-group">
                                <label>Nhãn</label>
                                <input type="text" [(ngModel)]="selectedField.label" (blur)="updateField()">
                            </div>

                            <div class="form-group">
                                <label>Key (định danh)</label>
                                <input type="text" [(ngModel)]="selectedField.fieldKey" (blur)="updateField()">
                            </div>

                            <div class="form-group">
                                <label>Placeholder</label>
                                <input type="text" [(ngModel)]="selectedField.placeholder" (blur)="updateField()">
                            </div>

                            <div class="form-group">
                                <label>Gợi ý</label>
                                <input type="text" [(ngModel)]="selectedField.helpText" (blur)="updateField()">
                            </div>

                            <div class="form-group">
                                <label>Giá trị mặc định</label>
                                <input type="text" [(ngModel)]="selectedField.defaultValue" (blur)="updateField()">
                            </div>

                            <div class="form-group checkbox-group">
                                <label>
                                    <input type="checkbox" [(ngModel)]="selectedField.isRequired" (change)="updateField()">
                                    Bắt buộc
                                </label>
                            </div>

                            @if (hasOptions(selectedField.fieldType)) {
                                <div class="form-group">
                                    <label>Các lựa chọn</label>
                                    <textarea 
                                        [ngModel]="getOptionsText(selectedField)"
                                        (blur)="setOptionsFromText($event, selectedField)"
                                        placeholder="Mỗi dòng một lựa chọn"></textarea>
                                    <small>Mỗi dòng một lựa chọn (value|label)</small>
                                </div>
                            }

                            <div class="section-divider">
                                <span>📐 Bố cục</span>
                            </div>

                            <div class="layout-controls">
                                <div class="form-group">
                                    <label>Chiều rộng</label>
                                    <div class="width-buttons">
                                        <button 
                                            type="button"
                                            [class.active]="selectedFieldColSpan === '1'"
                                            (click)="setColSpan('1')"
                                            title="25%">¼</button>
                                        <button 
                                            type="button"
                                            [class.active]="selectedFieldColSpan === '2'"
                                            (click)="setColSpan('2')"
                                            title="50%">½</button>
                                        <button 
                                            type="button"
                                            [class.active]="selectedFieldColSpan === '3'"
                                            (click)="setColSpan('3')"
                                            title="75%">¾</button>
                                        <button 
                                            type="button"
                                            [class.active]="selectedFieldColSpan === '4'"
                                            (click)="setColSpan('4')"
                                            title="100%">1</button>
                                    </div>
                                </div>

                                <div class="form-group">
                                    <label>Chiều cao</label>
                                    <select [(ngModel)]="selectedFieldHeight" (change)="updateFieldLayout()">
                                        <option value="auto">Tự động</option>
                                        <option value="small">Nhỏ (60px)</option>
                                        <option value="medium">Vừa (100px)</option>
                                        <option value="large">Lớn (150px)</option>
                                        <option value="xlarge">Rất lớn (200px)</option>
                                    </select>
                                </div>
                            </div>
                        </div>
                    </aside>
                }
            </div>
        </div>
    `,
    styles: [`
        .form-builder-container {
            height: 100vh;
            display: flex;
            flex-direction: column;
            background: #f1f5f9;
        }

        .builder-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px 24px;
            background: white;
            border-bottom: 1px solid #e2e8f0;
        }

        .header-left {
            display: flex;
            align-items: center;
            gap: 16px;
        }

        .back-btn {
            background: none;
            border: none;
            color: #667eea;
            cursor: pointer;
            font-size: 14px;
        }

        .form-name-input {
            border: none;
            font-size: 18px;
            font-weight: 600;
            padding: 8px;
            border-radius: 4px;
            width: 300px;
        }

        .form-name-input:focus {
            outline: none;
            background: #f1f5f9;
        }

        .header-right {
            display: flex;
            gap: 12px;
        }

        .btn {
            padding: 8px 16px;
            border: none;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 500;
        }

        .btn-primary { background: #667eea; color: white; }
        .btn-secondary { background: #e2e8f0; color: #475569; }
        .btn-success { background: #22c55e; color: white; }

        .builder-content {
            display: flex;
            flex: 1;
            overflow: hidden;
        }

        .field-palette {
            width: 280px;
            background: white;
            border-right: 1px solid #e2e8f0;
            padding: 20px;
            overflow-y: auto;
        }

        .field-palette h3 {
            margin: 0 0 16px;
            font-size: 14px;
            color: #64748b;
            text-transform: uppercase;
        }

        .field-types {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 8px;
            margin-bottom: 24px;
        }

        .field-type-btn {
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 12px 8px;
            background: #f8fafc;
            border: 1px dashed #cbd5e1;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.2s;
        }

        .field-type-btn:hover {
            border-color: #667eea;
            background: #eef2ff;
        }

        .field-icon {
            font-size: 24px;
            margin-bottom: 4px;
        }

        .field-type-btn span:last-child {
            font-size: 11px;
            color: #64748b;
        }

        .palette-section {
            padding-top: 20px;
            border-top: 1px solid #e2e8f0;
        }

        .palette-section h4 {
            margin: 0 0 12px;
            font-size: 13px;
            color: #64748b;
        }

        .form-canvas {
            flex: 1;
            padding: 24px;
            overflow-y: auto;
        }

        .canvas-header {
            text-align: center;
            margin-bottom: 24px;
        }

        .canvas-header h2 {
            margin: 0 0 8px;
            color: #1e293b;
        }

        .canvas-header p {
            margin: 0;
            color: #64748b;
        }

        .fields-container {
            max-width: 900px;
            margin: 0 auto;
            min-height: 400px;
        }

        .fields-container.grid-layout {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 12px;
            align-items: start;
        }

        .empty-canvas {
            padding: 60px 20px;
            grid-column: span 4;
        }

        .drop-zone {
            border: 2px dashed #cbd5e1;
            border-radius: 12px;
            padding: 60px 40px;
            text-align: center;
            background: white;
        }

        .drop-icon {
            font-size: 48px;
            display: block;
            margin-bottom: 12px;
        }

        .field-item {
            display: flex;
            flex-direction: column;
            gap: 8px;
            background: white;
            padding: 12px;
            border-radius: 8px;
            border: 2px solid transparent;
            cursor: pointer;
            transition: all 0.2s;
            min-height: 80px;
        }

        .field-item:hover {
            border-color: #e2e8f0;
        }

        .field-item.selected {
            border-color: #667eea;
            box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
        }

        .field-item.col-1 { background: linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%); }
        .field-item.col-2 { background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%); }
        .field-item.col-3 { background: linear-gradient(135deg, #fefce8 0%, #fef3c7 100%); }
        .field-item.col-4 { background: white; }

        .field-header {
            display: flex;
            align-items: center;
            gap: 8px;
            padding-bottom: 8px;
            border-bottom: 1px solid #f1f5f9;
        }

        .col-indicator {
            background: #e2e8f0;
            color: #64748b;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 10px;
            font-weight: 600;
        }

        .drag-handle {
            cursor: grab;
            color: #94a3b8;
            padding: 4px;
        }

        .delete-btn {
            margin-left: auto;
            background: none;
            border: none;
            cursor: pointer;
            padding: 4px;
            opacity: 0.5;
            transition: opacity 0.2s;
        }

        .field-item:hover .delete-btn {
            opacity: 1;
        }

        .field-preview {
            flex: 1;
        }

        .field-preview label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: #374151;
        }

        .required {
            color: #ef4444;
        }

        .field-preview input,
        .field-preview select,
        .field-preview textarea {
            width: 100%;
            padding: 8px 12px;
            border: 1px solid #d1d5db;
            border-radius: 6px;
            background: #f9fafb;
        }

        .help-text {
            display: block;
            margin-top: 4px;
            color: #64748b;
            font-size: 12px;
        }

        .radio-preview label {
            display: inline-flex;
            align-items: center;
            gap: 4px;
            margin-right: 16px;
        }

        .rating-preview {
            font-size: 20px;
        }

        .section-divider {
            border: none;
            border-top: 2px solid #e2e8f0;
            margin: 8px 0;
        }

        .delete-btn {
            background: none;
            border: none;
            cursor: pointer;
            opacity: 0.5;
            padding: 4px;
        }

        .field-item:hover .delete-btn {
            opacity: 1;
        }

        .properties-panel {
            width: 320px;
            background: white;
            border-left: 1px solid #e2e8f0;
            display: flex;
            flex-direction: column;
        }

        .panel-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 16px 20px;
            border-bottom: 1px solid #e2e8f0;
        }

        .panel-header h3 {
            margin: 0;
            font-size: 16px;
        }

        .close-btn {
            background: none;
            border: none;
            font-size: 18px;
            cursor: pointer;
            color: #64748b;
        }

        .panel-body {
            flex: 1;
            padding: 20px;
            overflow-y: auto;
        }

        .form-group {
            margin-bottom: 16px;
        }

        .form-group label {
            display: block;
            margin-bottom: 6px;
            font-size: 13px;
            font-weight: 500;
            color: #374151;
        }

        .form-group input,
        .form-group select,
        .form-group textarea {
            width: 100%;
            padding: 8px 12px;
            border: 1px solid #d1d5db;
            border-radius: 6px;
            font-size: 14px;
        }

        .form-group textarea {
            min-height: 80px;
            resize: vertical;
        }

        .form-group small {
            display: block;
            margin-top: 4px;
            color: #64748b;
            font-size: 11px;
        }

        .checkbox-group label {
            display: flex;
            align-items: center;
            gap: 8px;
            cursor: pointer;
        }

        .section-divider {
            display: flex;
            align-items: center;
            margin: 20px 0 16px;
            padding-top: 16px;
            border-top: 1px solid #e2e8f0;
        }

        .section-divider span {
            font-size: 13px;
            font-weight: 600;
            color: #1e293b;
        }

        .layout-controls {
            display: flex;
            flex-direction: column;
            gap: 16px;
        }

        .width-buttons {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 8px;
        }

        .width-buttons button {
            padding: 10px;
            border: 2px solid #e2e8f0;
            background: white;
            border-radius: 6px;
            font-size: 18px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s;
            color: #64748b;
        }

        .width-buttons button:hover {
            border-color: #667eea;
            background: #f5f3ff;
        }

        .width-buttons button.active {
            border-color: #667eea;
            background: #667eea;
            color: white;
        }

        .cdk-drag-preview {
            box-shadow: 0 4px 16px rgba(0,0,0,0.2);
        }

        .cdk-drag-placeholder {
            opacity: 0.3;
        }

        /* Responsive Styles */
        @media (max-width: 1024px) {
            .builder-content {
                flex-direction: column;
            }

            .field-palette {
                width: 100%;
                border-right: none;
                border-bottom: 1px solid #e2e8f0;
                max-height: 200px;
            }

            .properties-panel {
                width: 100%;
                border-left: none;
                border-top: 1px solid #e2e8f0;
                max-height: 300px;
            }
        }

        @media (max-width: 768px) {
            .builder-header {
                flex-direction: column;
                gap: 12px;
                padding: 12px;
            }

            .header-left {
                width: 100%;
            }

            .form-name-input {
                width: 100%;
            }

            .header-right {
                width: 100%;
                justify-content: flex-end;
            }

            .field-palette {
                max-height: 160px;
                padding: 12px;
            }

            .field-types {
                grid-template-columns: repeat(5, 1fr);
                gap: 4px;
            }

            .field-type-btn {
                padding: 8px 4px;
            }

            .field-icon {
                font-size: 18px;
            }

            .field-type-btn span:last-child {
                display: none;
            }

            .palette-section {
                display: none;
            }

            .form-canvas {
                padding: 12px;
            }
        }

        @media (max-width: 480px) {
            .btn {
                padding: 6px 10px;
                font-size: 13px;
            }

            .field-types {
                grid-template-columns: repeat(4, 1fr);
            }

            .field-item {
                padding: 12px;
            }
        }
    `]
})
export class FormBuilderComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);

    FieldType = FieldType;
    templateId: string | null = null;
    template: FormTemplate | null = null;
    categories: FormCategory[] = [];
    fields: FormField[] = [];
    selectedField: FormField | null = null;

    formName = '';
    formDescription = '';
    selectedCategoryId = '';
    selectedFieldColSpan = '4';
    selectedFieldHeight = 'auto';

    fieldTypes = [
        { type: FieldType.Text, icon: '📝', label: 'Văn bản' },
        { type: FieldType.TextArea, icon: '📄', label: 'Văn bản dài' },
        { type: FieldType.Number, icon: '🔢', label: 'Số' },
        { type: FieldType.Date, icon: '📅', label: 'Ngày' },
        { type: FieldType.Dropdown, icon: '📋', label: 'Dropdown' },
        { type: FieldType.Radio, icon: '⭕', label: 'Radio' },
        { type: FieldType.Checkbox, icon: '☑️', label: 'Checkbox' },
        { type: FieldType.Rating, icon: '⭐', label: 'Đánh giá' },
        { type: FieldType.Section, icon: '➖', label: 'Phân đoạn' },
        { type: FieldType.FileUpload, icon: '📎', label: 'Tải file' }
    ];

    ngOnInit() {
        this.formsService.getCategories().subscribe(cats => {
            this.categories = cats;
            if (cats.length > 0 && !this.selectedCategoryId) {
                this.selectedCategoryId = cats[0].id;
            }
        });

        this.route.params.subscribe(params => {
            if (params['id']) {
                this.templateId = params['id'];
                this.loadTemplate();
            }
        });
    }

    loadTemplate() {
        if (!this.templateId) return;
        this.formsService.getTemplateById(this.templateId).subscribe(template => {
            this.template = template;
            this.formName = template.name;
            this.formDescription = template.description || '';
            this.selectedCategoryId = template.categoryId;
            this.fields = template.fields || [];
        });
    }

    goBack() {
        this.router.navigate(['/forms']);
    }

    save() {
        if (!this.templateId) {
            // Create new template
            this.formsService.createTemplate({
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
                    defaultValue: f.defaultValue,
                    helpText: f.helpText
                }))
            }).subscribe(template => {
                this.templateId = template.id;
                this.template = template;
                alert('Đã lưu biểu mẫu!');
            });
        } else {
            // Update existing
            this.formsService.updateTemplate(this.templateId, {
                name: this.formName,
                description: this.formDescription,
                categoryId: this.selectedCategoryId
            }).subscribe(() => {
                alert('Đã cập nhật biểu mẫu!');
            });
        }
    }

    saveFormSettings() {
        if (this.templateId) {
            this.formsService.updateTemplate(this.templateId, {
                name: this.formName,
                description: this.formDescription,
                categoryId: this.selectedCategoryId
            }).subscribe();
        }
    }

    publish() {
        if (this.templateId) {
            this.formsService.publishTemplate(this.templateId).subscribe(template => {
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

    addField(type: FieldType) {
        const newField: FormField = {
            id: '',
            fieldKey: `field_${this.fields.length + 1}`,
            label: this.getFieldTypeLabel(type),
            fieldType: type,
            displayOrder: this.fields.length,
            isRequired: false,
            placeholder: '',
            optionsJson: this.hasOptions(type) ? JSON.stringify([
                { value: 'opt1', label: 'Lựa chọn 1' },
                { value: 'opt2', label: 'Lựa chọn 2' }
            ]) : undefined
        };

        this.fields.push(newField);

        if (this.templateId) {
            this.formsService.addField(this.templateId, {
                fieldKey: newField.fieldKey,
                label: newField.label,
                fieldType: newField.fieldType,
                displayOrder: newField.displayOrder,
                isRequired: newField.isRequired,
                placeholder: newField.placeholder,
                optionsJson: newField.optionsJson
            }).subscribe(field => {
                const index = this.fields.findIndex(f => f.fieldKey === newField.fieldKey);
                if (index >= 0) this.fields[index] = field;
            });
        }
    }

    selectField(field: FormField) {
        this.selectedField = field;
        // Get colSpan and height from field's validation rules
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            this.selectedFieldColSpan = rules.colSpan?.toString() || '4';
            this.selectedFieldHeight = rules.height || 'auto';
        } catch {
            this.selectedFieldColSpan = '4';
            this.selectedFieldHeight = 'auto';
        }
    }

    updateField() {
        if (this.selectedField?.id && this.templateId) {
            this.formsService.updateField(this.selectedField.id, this.selectedField).subscribe();
        }
    }

    setColSpan(span: string) {
        this.selectedFieldColSpan = span;
        this.updateFieldLayout();
    }

    updateFieldLayout() {
        if (this.selectedField) {
            // Store colSpan and height in validationRulesJson
            try {
                const rules = this.selectedField.validationRulesJson
                    ? JSON.parse(this.selectedField.validationRulesJson)
                    : {};
                rules.colSpan = parseInt(this.selectedFieldColSpan);
                rules.height = this.selectedFieldHeight;
                this.selectedField.validationRulesJson = JSON.stringify(rules);
                this.updateField();
            } catch {
                // Ignore parse errors
            }
        }
    }

    getFieldColSpan(field: FormField): number {
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            return rules.colSpan || 4; // Default to full width (4 columns)
        } catch {
            return 4;
        }
    }

    getFieldHeight(field: FormField): string {
        try {
            const rules = field.validationRulesJson ? JSON.parse(field.validationRulesJson) : {};
            const height = rules.height || 'auto';
            const heightMap: { [key: string]: string } = {
                'auto': 'auto',
                'small': '60px',
                'medium': '100px',
                'large': '150px',
                'xlarge': '200px'
            };
            return heightMap[height] || 'auto';
        } catch {
            return 'auto';
        }
    }

    deleteField(field: FormField, event: Event) {
        event.stopPropagation();
        if (confirm('Xóa trường này?')) {
            this.fields = this.fields.filter(f => f !== field);
            if (field.id) {
                this.formsService.deleteField(field.id).subscribe();
            }
            if (this.selectedField === field) {
                this.selectedField = null;
            }
        }
    }

    onDropField(event: CdkDragDrop<FormField[]>) {
        moveItemInArray(this.fields, event.previousIndex, event.currentIndex);
        if (this.templateId) {
            const fieldIds = this.fields.filter(f => f.id).map(f => f.id);
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

    getFieldTypeLabel(type: FieldType): string {
        return FieldTypeLabels[type] || 'Trường mới';
    }

    hasOptions(type: FieldType): boolean {
        return [FieldType.Dropdown, FieldType.MultiSelect, FieldType.Radio].includes(type);
    }

    getOptions(field: FormField): { value: string; label: string }[] {
        return this.formsService.parseOptions(field.optionsJson);
    }

    getOptionsText(field: FormField): string {
        const options = this.getOptions(field);
        return options.map(o => `${o.value}|${o.label}`).join('\n');
    }

    setOptionsFromText(event: Event, field: FormField) {
        const text = (event.target as HTMLTextAreaElement).value;
        const options = text.split('\n').filter(line => line.trim()).map(line => {
            const parts = line.split('|');
            return {
                value: parts[0]?.trim() || parts[0],
                label: parts[1]?.trim() || parts[0]
            };
        });
        field.optionsJson = JSON.stringify(options);
        this.updateField();
    }
}
