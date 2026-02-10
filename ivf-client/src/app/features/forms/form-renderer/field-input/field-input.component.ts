import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnDestroy,
  AfterViewInit,
  inject,
  ViewChild,
  ElementRef,
  HostBinding,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup } from '@angular/forms';
import { Subscription } from 'rxjs';
import { FormsService, FormField, FieldType, LinkedDataValue } from '../../forms.service';
import { ConceptService } from '../../services/concept.service';
import {
  getFieldColSpan,
  getFieldHeight,
  getAddressSubFields,
  getSliderConfig,
  getRepeaterConfig,
} from '../../shared/form-field.utils';

@Component({
  selector: 'app-field-input',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './field-input.component.html',
  styleUrls: ['./field-input.component.scss'],
})
export class FieldInputComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly formsService = inject(FormsService);
  private readonly conceptService = inject(ConceptService);

  @Input() field!: FormField;
  @Input() form!: FormGroup;
  @Input() linkedDataInfo: LinkedDataValue | null = null;
  @Input() isLinked = false;

  @Output() fileChanged = new EventEmitter<{ fieldId: string; file: File }>();
  @Output() linkedValueCleared = new EventEmitter<string>();

  @ViewChild('mentionEditor') mentionEditorRef?: ElementRef<HTMLDivElement>;
  @ViewChild('signatureCanvas') signatureCanvasRef?: ElementRef<HTMLCanvasElement>;

  FieldType = FieldType;

  private valueSub?: Subscription;
  private isUpdatingFromEditor = false;

  // ===== Host Bindings =====
  @HostBinding('style.gridColumn')
  get gridColumn(): string {
    return 'span ' + getFieldColSpan(this.field);
  }

  @HostBinding('class.has-error')
  get hostHasError(): boolean {
    return this.hasError();
  }

  // ===== Signature =====
  private signatureCtx: CanvasRenderingContext2D | null = null;
  private signatureIsDrawing = false;

  // ===== Lookup =====
  lookupSearchQuery = '';
  lookupResults: { value: string; label: string }[] = [];
  lookupActive = false;

  // ===== Repeater =====
  repeaterRows: any[] = [];
  private repeaterInitialized = false;

  // ===== Tags / Mention =====
  mentionData: { text: string; mentions: any[] } = { text: '', mentions: [] };
  showMentionDropdown = false;
  mentionSearchResults: { id: string; code: string; name: string }[] = [];
  private mentionSearchStart = -1;

  // ===== Lifecycle =====
  ngOnInit() {
    if (this.field.fieldType === FieldType.Tags) {
      const control = this.form.get(this.field.id);
      if (control) {
        this.valueSub = control.valueChanges.subscribe(() => {
          if (!this.isUpdatingFromEditor) {
            setTimeout(() => this.updateTagsEditorFromValue());
          }
        });
      }
    }
    if (this.field.fieldType === FieldType.Repeater) {
      this.initRepeater();
    }
  }

  ngAfterViewInit() {
    if (this.field.fieldType === FieldType.Tags) {
      setTimeout(() => this.updateTagsEditorFromValue());
    }
  }

  ngOnDestroy() {
    this.valueSub?.unsubscribe();
  }

  // ===== Shared Util Wrappers =====
  getOptions(field: FormField) {
    return this.formsService.parseOptions(field.optionsJson);
  }

  getHeight(): string {
    return getFieldHeight(this.field);
  }

  getAddrSubFields() {
    return getAddressSubFields(this.field);
  }

  getSliderCfg() {
    return getSliderConfig(this.field);
  }

  getRepeaterCfg() {
    return getRepeaterConfig(this.field);
  }

  // ===== Validation =====
  hasError(): boolean {
    const control = this.form.get(this.field.id);
    return control ? control.invalid && control.touched : false;
  }

  getFieldErrors(): string[] {
    const control = this.form.get(this.field.id);
    if (!control || !control.errors || !control.touched) return [];
    const errors: string[] = [];
    const rules = this.formsService.parseValidationRules(this.field.validationRulesJson);
    const msgMap = new Map(rules.filter((r) => r.message).map((r) => [r.type, r.message!]));

    if (control.errors['required']) errors.push(msgMap.get('required') || 'Trường này là bắt buộc');
    if (control.errors['minlength'])
      errors.push(
        msgMap.get('minLength') || `Tối thiểu ${control.errors['minlength'].requiredLength} ký tự`,
      );
    if (control.errors['maxlength'])
      errors.push(
        msgMap.get('maxLength') || `Tối đa ${control.errors['maxlength'].requiredLength} ký tự`,
      );
    if (control.errors['min'])
      errors.push(msgMap.get('min') || `Giá trị tối thiểu là ${control.errors['min'].min}`);
    if (control.errors['max'])
      errors.push(msgMap.get('max') || `Giá trị tối đa là ${control.errors['max'].max}`);
    if (control.errors['pattern'])
      errors.push(msgMap.get('pattern') || 'Giá trị không đúng định dạng');
    if (control.errors['email']) errors.push(msgMap.get('email') || 'Email không hợp lệ');
    return errors;
  }

  // ===== Rating =====
  setRating(value: number) {
    this.form.get(this.field.id)?.setValue(value);
  }

  // ===== Checkbox =====
  isCheckboxChecked(optionValue: string): boolean {
    const value = this.form.get(this.field.id)?.value;
    return Array.isArray(value) && value.includes(optionValue);
  }

  onCheckboxChange(optionValue: string, event: Event) {
    const checkbox = event.target as HTMLInputElement;
    const control = this.form.get(this.field.id);
    if (!control) return;
    let current = control.value || [];
    if (!Array.isArray(current)) current = [];
    if (checkbox.checked) {
      if (!current.includes(optionValue)) control.setValue([...current, optionValue]);
    } else {
      control.setValue(current.filter((v: string) => v !== optionValue));
    }
  }

  // ===== File Upload =====
  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) {
      this.fileChanged.emit({ fieldId: this.field.id, file: input.files[0] });
    }
  }

  // ===== Slider =====
  onSliderChange(event: Event) {
    const val = Number((event.target as HTMLInputElement).value);
    this.form.get(this.field.id)?.setValue(val);
  }

  getSliderDisplayValue(): string {
    const val = this.form.get(this.field.id)?.value;
    const config = this.getSliderCfg();
    return `${val ?? config.min}${config.unit ? ' ' + config.unit : ''}`;
  }

  // ===== Signature =====
  initSignatureCanvas() {
    setTimeout(() => {
      const canvas = this.signatureCanvasRef?.nativeElement;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      this.signatureCtx = ctx;
      ctx.strokeStyle = '#1e293b';
      ctx.lineWidth = 2;
      ctx.lineCap = 'round';

      canvas.addEventListener('mousedown', (e) => this.startDraw(e));
      canvas.addEventListener('mousemove', (e) => this.continueDraw(e));
      canvas.addEventListener('mouseup', () => this.endDraw());
      canvas.addEventListener('mouseleave', () => this.endDraw());
      canvas.addEventListener('touchstart', (e) => {
        e.preventDefault();
        this.startDraw(e.touches[0]);
      });
      canvas.addEventListener('touchmove', (e) => {
        e.preventDefault();
        this.continueDraw(e.touches[0]);
      });
      canvas.addEventListener('touchend', () => this.endDraw());
    }, 100);
  }

  private startDraw(e: MouseEvent | Touch) {
    this.signatureIsDrawing = true;
    const canvas = this.signatureCanvasRef?.nativeElement;
    if (!canvas || !this.signatureCtx) return;
    const rect = canvas.getBoundingClientRect();
    this.signatureCtx.beginPath();
    this.signatureCtx.moveTo(e.clientX - rect.left, e.clientY - rect.top);
  }

  private continueDraw(e: MouseEvent | Touch) {
    if (!this.signatureIsDrawing || !this.signatureCtx) return;
    const canvas = this.signatureCanvasRef?.nativeElement;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    this.signatureCtx.lineTo(e.clientX - rect.left, e.clientY - rect.top);
    this.signatureCtx.stroke();
  }

  private endDraw() {
    if (!this.signatureIsDrawing) return;
    this.signatureIsDrawing = false;
    const canvas = this.signatureCanvasRef?.nativeElement;
    if (canvas) {
      this.form.get(this.field.id)?.setValue(canvas.toDataURL('image/png'));
    }
  }

  clearSignature() {
    const canvas = this.signatureCanvasRef?.nativeElement;
    if (canvas && this.signatureCtx) {
      this.signatureCtx.clearRect(0, 0, canvas.width, canvas.height);
    }
    this.form.get(this.field.id)?.setValue('');
  }

  // ===== Lookup =====
  searchLookupOptions() {
    const query = (this.lookupSearchQuery || '').toLowerCase();
    const options = this.getOptions(this.field);
    if (!query || query.length < 1) {
      this.lookupResults = options.slice(0, 10);
      return;
    }
    this.lookupResults = options
      .filter(
        (opt) => opt.label.toLowerCase().includes(query) || opt.value.toLowerCase().includes(query),
      )
      .slice(0, 10);
  }

  selectLookupOption(opt: { value: string; label: string }) {
    this.form.get(this.field.id)?.setValue(opt.value);
    this.lookupSearchQuery = opt.label;
    this.lookupResults = [];
    this.lookupActive = false;
  }

  getLookupDisplayLabel(): string {
    const val = this.form.get(this.field.id)?.value;
    if (!val) return '';
    const opts = this.getOptions(this.field);
    return opts.find((o) => o.value === val)?.label || val;
  }

  onLookupFocus() {
    this.lookupActive = true;
    this.searchLookupOptions();
  }

  onLookupBlur() {
    this.lookupActive = false;
  }

  // ===== Repeater =====
  initRepeater() {
    if (this.repeaterInitialized) return;
    this.repeaterInitialized = true;
    const config = this.getRepeaterCfg();
    this.repeaterRows = [];
    for (let i = 0; i < (config.minRows || 1); i++) {
      this.addRepeaterRow();
    }
  }

  addRepeaterRow() {
    const config = this.getRepeaterCfg();
    if (this.repeaterRows.length >= config.maxRows) return;
    const row: any = {};
    for (const col of config.fields) {
      row[col.key] = '';
    }
    this.repeaterRows.push(row);
    this.updateRepeaterFormValue();
  }

  removeRepeaterRow(index: number) {
    const config = this.getRepeaterCfg();
    if (this.repeaterRows.length <= (config.minRows || 0)) return;
    this.repeaterRows.splice(index, 1);
    this.updateRepeaterFormValue();
  }

  onRepeaterCellChange() {
    this.updateRepeaterFormValue();
  }

  private updateRepeaterFormValue() {
    const control = this.form.get(this.field.id);
    if (control) {
      control.setValue(JSON.stringify(this.repeaterRows));
    }
  }

  // ===== Tags / Mention =====
  onContentEditableInput(event: Event) {
    const editor = event.target as HTMLDivElement;
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) return;

    const range = selection.getRangeAt(0);
    const preCaretRange = range.cloneRange();
    preCaretRange.selectNodeContents(editor);
    preCaretRange.setEnd(range.endContainer, range.endOffset);
    const textBeforeCursor = preCaretRange.toString();

    const atIndex = textBeforeCursor.lastIndexOf('@');
    if (atIndex !== -1) {
      const afterAt = textBeforeCursor.substring(atIndex + 1);
      if (!afterAt.includes(' ') && afterAt.length <= 30) {
        this.mentionSearchStart = atIndex;
        this.searchConcepts(afterAt);
        this.showMentionDropdown = true;
        return;
      }
    }

    this.showMentionDropdown = false;
    this.syncEditorToForm(editor);
  }

  onContentEditableKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      this.showMentionDropdown = false;
    }
    if (
      (event.key === 'Tab' || event.key === 'Enter') &&
      this.showMentionDropdown &&
      this.mentionSearchResults.length > 0
    ) {
      event.preventDefault();
      this.insertMentionTag(this.mentionSearchResults[0]);
    }
  }

  onMentionBlur() {
    setTimeout(() => (this.showMentionDropdown = false), 200);
  }

  private searchConcepts(query: string) {
    const options = this.getOptions(this.field);
    if (options.length > 0) {
      const q = (query || '').toLowerCase();
      const filtered = q
        ? options.filter(
            (opt) => opt.label.toLowerCase().includes(q) || opt.value.toLowerCase().includes(q),
          )
        : options;
      this.mentionSearchResults = filtered.slice(0, 10).map((opt) => ({
        id: (opt as any).conceptId || opt.value,
        code: opt.value,
        name: opt.label,
      }));
    } else {
      this.conceptService.searchConcepts(query || '', undefined, 10).subscribe({
        next: (result) => {
          this.mentionSearchResults = result.concepts.map((c) => ({
            id: c.id,
            code: c.code,
            name: c.display,
          }));
        },
        error: () => {
          this.mentionSearchResults = [];
        },
      });
    }
  }

  insertMentionTag(concept: { id: string; code: string; name: string }) {
    const editor = this.mentionEditorRef?.nativeElement;
    if (!editor) return;

    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) return;

    // Remove @query text
    const range = selection.getRangeAt(0);
    const textNode = range.startContainer;
    if (textNode.nodeType === Node.TEXT_NODE) {
      const text = textNode.textContent || '';
      const atIndex = text.lastIndexOf('@');
      if (atIndex !== -1) {
        textNode.textContent = text.substring(0, atIndex);
      }
    }

    // Create tag span
    const tagSpan = document.createElement('span');
    tagSpan.className = 'inline-tag';
    tagSpan.contentEditable = 'false';
    tagSpan.setAttribute('data-concept-id', concept.id);
    tagSpan.setAttribute('data-concept-code', concept.code);
    tagSpan.textContent = concept.name;
    tagSpan.style.cssText =
      'background-color: #e7f3ff; color: #1877f2; padding: 0 2px; border-radius: 3px; font-weight: 500; cursor: default;';

    // Insert tag at end
    const newRange = document.createRange();
    newRange.selectNodeContents(editor);
    newRange.collapse(false);
    newRange.insertNode(tagSpan);

    // Add trailing space and position cursor
    const space = document.createTextNode(' ');
    tagSpan.after(space);
    const cursorRange = document.createRange();
    cursorRange.setStartAfter(space);
    cursorRange.collapse(true);
    selection.removeAllRanges();
    selection.addRange(cursorRange);

    // Track mention
    this.mentionData.mentions.push({
      text: concept.name,
      conceptId: concept.id,
      code: concept.code,
      start: 0,
      end: 0,
    });

    this.showMentionDropdown = false;
    this.isUpdatingFromEditor = true;
    this.syncEditorToForm(editor);
    this.isUpdatingFromEditor = false;
    editor.focus();
  }

  private syncEditorToForm(editor: HTMLDivElement) {
    const control = this.form.get(this.field.id);
    if (!control) return;

    const tagIds: string[] = [];
    editor.querySelectorAll('.inline-tag').forEach((el) => {
      const id = el.getAttribute('data-concept-id');
      if (id) tagIds.push(id);
    });

    this.isUpdatingFromEditor = true;
    control.setValue(
      JSON.stringify({
        text: editor.textContent || '',
        tagIds: [...new Set(tagIds)],
        html: editor.innerHTML,
      }),
    );
    this.isUpdatingFromEditor = false;
  }

  updateTagsEditorFromValue() {
    const control = this.form.get(this.field.id);
    if (!control) return;

    const editor = this.mentionEditorRef?.nativeElement;
    if (!editor) return;

    const val = control.value;
    if (!val) {
      editor.innerHTML = '';
      return;
    }

    try {
      const data = typeof val === 'string' ? JSON.parse(val) : val;
      if (data && data.html) {
        editor.innerHTML = data.html;
        if (data.mentions) {
          this.mentionData = { text: data.text, mentions: data.mentions };
        }
      } else if (Array.isArray(data)) {
        const options = this.getOptions(this.field);
        let html = '';
        data.forEach((item: any) => {
          const tagValue = typeof item === 'string' ? item : item.value;
          const opt =
            options.find((o) => o.value === tagValue) ||
            options.find((o) => (o as any).conceptId === tagValue);
          const label = opt?.label || tagValue;
          const conceptId =
            (opt as any)?.conceptId || (typeof item === 'object' ? item.conceptId : null);
          html += `<span class="mention-tag" contenteditable="false" data-concept-id="${conceptId || ''}" data-value="${tagValue}">${label}</span> `;
        });
        editor.innerHTML = html;
      } else {
        editor.innerText = val && val !== 'undefined' ? String(val) : '';
      }
    } catch {
      editor.innerText = val && val !== 'undefined' ? String(val) : '';
    }
  }

  // ===== Linked Data =====
  onClearLinkedValue() {
    this.linkedValueCleared.emit(this.field.id);
  }
}
