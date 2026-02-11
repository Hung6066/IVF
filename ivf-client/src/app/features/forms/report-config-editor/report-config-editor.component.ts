import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  FormsService,
  FormField,
  ReportTemplate,
  ReportData,
  ReportType,
  ReportConfiguration,
  ReportColumnConfig,
  ReportFilterConfig,
  ReportChartConfig,
  ConditionalFormatRule,
  ConditionalFormatStyle,
  CalculatedFieldConfig,
  GroupSummaryConfig,
  GroupAggregation,
  FieldType,
  FieldTypeLabels,
} from '../forms.service';

@Component({
  selector: 'app-report-config-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule],
  templateUrl: './report-config-editor.component.html',
  styleUrls: ['./report-config-editor.component.scss'],
})
export class ReportConfigEditorComponent implements OnInit {
  private readonly formsService = inject(FormsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  ReportType = ReportType;
  reportId = '';
  report: ReportTemplate | null = null;
  formFields: FormField[] = [];
  config: ReportConfiguration = this.getDefaultConfig();
  isSaving = false;
  activeTab:
    | 'columns'
    | 'filters'
    | 'chart'
    | 'formatting'
    | 'computed'
    | 'page'
    | 'header'
    | 'preview' = 'columns';
  isDirty = false;

  // Preview data
  previewData: ReportData | null = null;
  isLoadingPreview = false;

  // Formatting editor
  editingRule: ConditionalFormatRule | null = null;

  /** Fields that are data-bearing (exclude layout-only types) */
  get dataFields(): FormField[] {
    const layoutTypes = [FieldType.Section, FieldType.Label, FieldType.PageBreak];
    return this.formFields.filter((f) => !layoutTypes.includes(f.fieldType));
  }

  get isChartType(): boolean {
    return (
      this.report?.reportType === ReportType.BarChart ||
      this.report?.reportType === ReportType.PieChart ||
      this.report?.reportType === ReportType.LineChart
    );
  }

  get visibleColumns(): ReportColumnConfig[] {
    return this.config.columns.filter((c) => c.visible);
  }

  /** All field keys available (system + data + calculated) */
  get allFieldKeys(): { key: string; label: string }[] {
    const keys: { key: string; label: string }[] = [
      { key: 'patientName', label: 'Benh nhan' },
      { key: 'submittedAt', label: 'Ngay nop' },
      { key: 'status', label: 'Trang thai' },
    ];
    for (const f of this.dataFields) {
      keys.push({ key: f.fieldKey, label: f.label });
    }
    for (const cf of this.config.calculatedFields ?? []) {
      keys.push({ key: cf.fieldKey, label: cf.label });
    }
    return keys;
  }

  ngOnInit() {
    this.route.params.subscribe((params) => {
      if (params['id']) {
        this.reportId = params['id'];
        this.loadReport();
      }
    });
  }

  private loadReport() {
    this.formsService.getReportTemplateById(this.reportId).subscribe((report) => {
      this.report = report;
      this.parseExistingConfig(report.configurationJson);

      this.formsService.getFieldsByTemplate(report.formTemplateId).subscribe((fields) => {
        this.formFields = fields.sort((a, b) => a.displayOrder - b.displayOrder);
        this.syncColumnsWithFields();
      });
    });
  }

  private getDefaultConfig(): ReportConfiguration {
    return {
      columns: [],
      page: {
        size: 'A4',
        orientation: 'landscape',
        margins: { top: 30, right: 30, bottom: 30, left: 30 },
      },
      header: { title: '', subtitle: '', showLogo: true, showDate: true },
      footer: { text: '', showPageNumber: true },
      filters: [],
      sortBy: '',
      sortDirection: 'asc',
      chart: {
        categoryField: '',
        valueField: '',
        aggregation: 'count',
        showLegend: true,
        showValues: true,
        maxItems: 12,
      },
      conditionalFormats: [],
      calculatedFields: [],
      showFooterAggregations: false,
      groupSummary: { showGroupHeaders: true, showGroupFooters: true, aggregations: [] },
    };
  }

  private parseExistingConfig(json: string) {
    try {
      const parsed = JSON.parse(json);
      if (parsed && typeof parsed === 'object' && parsed.columns) {
        this.config = { ...this.getDefaultConfig(), ...parsed };
        if (!this.config.conditionalFormats) this.config.conditionalFormats = [];
        if (!this.config.calculatedFields) this.config.calculatedFields = [];
        if (!this.config.groupSummary) {
          this.config.groupSummary = { showGroupHeaders: true, showGroupFooters: true, aggregations: [] };
        }
      }
    } catch { /* Keep default config */ }
  }

  private syncColumnsWithFields() {
    const existingByKey = new Map(this.config.columns.map((c) => [c.fieldKey, c]));

    const systemColumns: ReportColumnConfig[] = [
      existingByKey.get('patientName') ?? { fieldKey: 'patientName', label: 'Benh nhan', visible: true, format: 'text' },
      existingByKey.get('submittedAt') ?? { fieldKey: 'submittedAt', label: 'Ngay nop', visible: true, format: 'datetime' },
      existingByKey.get('status') ?? { fieldKey: 'status', label: 'Trang thai', visible: true, format: 'text' },
    ];

    const addedKeys = new Set(systemColumns.map((c) => c.fieldKey));

    const fieldColumns: ReportColumnConfig[] = this.dataFields
      .filter((f) => !addedKeys.has(f.fieldKey))
      .reduce<ReportColumnConfig[]>((acc, f) => {
        if (acc.some((c) => c.fieldKey === f.fieldKey)) return acc;
        acc.push(existingByKey.get(f.fieldKey) ?? {
          fieldKey: f.fieldKey, label: f.label, visible: this.config.columns.length === 0, format: this.inferFormat(f.fieldType),
        });
        return acc;
      }, []);

    const calcColumns: ReportColumnConfig[] = (this.config.calculatedFields ?? [])
      .filter((cf) => !addedKeys.has(cf.fieldKey) && !fieldColumns.some((c) => c.fieldKey === cf.fieldKey))
      .map((cf) => existingByKey.get(cf.fieldKey) ?? { fieldKey: cf.fieldKey, label: cf.label, visible: true, format: cf.format ?? 'text' });

    this.config.columns = [...systemColumns, ...fieldColumns, ...calcColumns];

    if (this.isChartType && !this.config.chart?.categoryField && this.dataFields.length > 0) {
      const choiceField = this.dataFields.find(
        (f) => f.fieldType === FieldType.Dropdown || f.fieldType === FieldType.Radio || f.fieldType === FieldType.Checkbox,
      );
      if (choiceField && this.config.chart) {
        this.config.chart.categoryField = choiceField.fieldKey;
      }
    }
  }

  private inferFormat(fieldType: FieldType): ReportColumnConfig['format'] {
    switch (fieldType) {
      case FieldType.Number: case FieldType.Slider: case FieldType.Rating: case FieldType.Decimal: return 'number';
      case FieldType.Date: return 'date';
      case FieldType.DateTime: return 'datetime';
      default: return 'text';
    }
  }

  getFieldTypeLabel(fieldType: FieldType): string { return FieldTypeLabels[fieldType] || ''; }

  // ===== CDK Drag and Drop =====

  dropColumn(event: CdkDragDrop<ReportColumnConfig[]>) {
    moveItemInArray(this.config.columns, event.previousIndex, event.currentIndex);
    this.markDirty();
  }

  toggleColumn(col: ReportColumnConfig) { col.visible = !col.visible; this.markDirty(); }

  toggleAllColumns(visible: boolean) { this.config.columns.forEach((c) => (c.visible = visible)); this.markDirty(); }

  // ===== Filters =====

  addFilter() {
    if (this.dataFields.length === 0) return;
    this.config.filters.push({ fieldKey: this.dataFields[0].fieldKey, operator: 'eq', value: '' });
    this.markDirty();
  }

  removeFilter(index: number) { this.config.filters.splice(index, 1); this.markDirty(); }

  // ===== Conditional Formatting =====

  addConditionalFormat() {
    const rule: ConditionalFormatRule = {
      id: crypto.randomUUID(),
      name: 'Quy tac ' + ((this.config.conditionalFormats?.length ?? 0) + 1),
      fieldKey: this.allFieldKeys[0]?.key ?? '',
      operator: 'eq',
      value: '',
      applyTo: 'row',
      style: { backgroundColor: '#fef3c7', textColor: '#92400e', fontWeight: 'normal', fontStyle: 'normal' },
    };
    if (!this.config.conditionalFormats) this.config.conditionalFormats = [];
    this.config.conditionalFormats.push(rule);
    this.editingRule = rule;
    this.markDirty();
  }

  removeConditionalFormat(index: number) {
    const removed = this.config.conditionalFormats?.splice(index, 1);
    if (removed?.[0] === this.editingRule) this.editingRule = null;
    this.markDirty();
  }

  selectRule(rule: ConditionalFormatRule) { this.editingRule = rule; }

  getOperatorLabel(op: string): string {
    const labels: Record<string, string> = { eq: '=', neq: '!=', gt: '>', lt: '<', gte: '>=', lte: '<=', contains: 'Chua', empty: 'Trong', notEmpty: 'Khong trong' };
    return labels[op] || op;
  }

  readonly presetColors = [
    { bg: '#dcfce7', text: '#166534', label: 'Xanh la' },
    { bg: '#fef3c7', text: '#92400e', label: 'Vang' },
    { bg: '#fee2e2', text: '#991b1b', label: 'Do' },
    { bg: '#dbeafe', text: '#1e40af', label: 'Xanh duong' },
    { bg: '#f3e8ff', text: '#6b21a8', label: 'Tim' },
    { bg: '#f1f5f9', text: '#475569', label: 'Xam' },
  ];

  applyPresetColor(rule: ConditionalFormatRule, preset: { bg: string; text: string }) {
    rule.style.backgroundColor = preset.bg;
    rule.style.textColor = preset.text;
    this.markDirty();
  }

  // ===== Calculated Fields =====

  addCalculatedField() {
    const key = 'calc_' + Date.now();
    const field: CalculatedFieldConfig = { fieldKey: key, label: 'Truong tinh toan moi', expression: '', format: 'text' };
    if (!this.config.calculatedFields) this.config.calculatedFields = [];
    this.config.calculatedFields.push(field);
    this.config.columns.push({ fieldKey: key, label: field.label, visible: true, format: field.format });
    this.markDirty();
  }

  removeCalculatedField(index: number) {
    const field = this.config.calculatedFields?.[index];
    if (field) {
      this.config.calculatedFields?.splice(index, 1);
      const colIdx = this.config.columns.findIndex((c) => c.fieldKey === field.fieldKey);
      if (colIdx >= 0) this.config.columns.splice(colIdx, 1);
    }
    this.markDirty();
  }

  // ===== Grouping =====

  addGroupAggregation() {
    if (!this.config.groupSummary) {
      this.config.groupSummary = { showGroupHeaders: true, showGroupFooters: true, aggregations: [] };
    }
    const numericField = this.dataFields.find(
      (f) => f.fieldType === FieldType.Number || f.fieldType === FieldType.Decimal || f.fieldType === FieldType.Slider || f.fieldType === FieldType.Rating,
    );
    this.config.groupSummary.aggregations.push({ fieldKey: numericField?.fieldKey ?? this.dataFields[0]?.fieldKey ?? '', type: 'count', label: '' });
    this.markDirty();
  }

  removeGroupAggregation(index: number) { this.config.groupSummary?.aggregations.splice(index, 1); this.markDirty(); }

  // ===== Live Preview =====

  loadPreview() {
    if (!this.reportId || this.isLoadingPreview) return;
    this.isLoadingPreview = true;

    const configJson = JSON.stringify(this.config);
    if (this.report) {
      this.formsService.updateReportTemplate(this.reportId, {
        name: this.report.name, description: this.report.description, reportType: this.report.reportType, configurationJson: configJson,
      }).subscribe({
        next: () => {
          this.isDirty = false;
          this.formsService.generateReport(this.reportId).subscribe({
            next: (data) => { this.previewData = data; this.isLoadingPreview = false; },
            error: () => { this.isLoadingPreview = false; },
          });
        },
        error: () => { this.isLoadingPreview = false; },
      });
    }
  }

  getPreviewColumns(): string[] {
    if (!this.previewData?.data?.length) return [];
    const allKeys = Object.keys(this.previewData.data[0]);
    if (this.config.columns.length) {
      return this.config.columns.filter((c) => c.visible).map((c) => c.fieldKey).filter((k) => allKeys.includes(k));
    }
    return allKeys;
  }

  getPreviewColumnLabel(key: string): string {
    const col = this.config.columns.find((c) => c.fieldKey === key);
    if (col?.label) return col.label;
    const defaults: Record<string, string> = { patientName: 'Benh nhan', submittedAt: 'Ngay nop', status: 'Trang thai' };
    return defaults[key] || key;
  }

  formatPreviewValue(value: any): string {
    if (value === null || value === undefined) return '-';
    return value.toString();
  }

  getRowStyles(row: Record<string, any>): Record<string, string> {
    if (!this.config.conditionalFormats?.length) return {};
    for (const rule of this.config.conditionalFormats) {
      if (rule.applyTo !== 'row') continue;
      if (this.evaluateRule(rule, row)) return this.buildStyleObj(rule.style);
    }
    return {};
  }

  getCellStyles(row: Record<string, any>, fieldKey: string): Record<string, string> {
    if (!this.config.conditionalFormats?.length) return {};
    for (const rule of this.config.conditionalFormats) {
      if (rule.applyTo !== 'cell' || rule.fieldKey !== fieldKey) continue;
      if (this.evaluateRule(rule, row)) return this.buildStyleObj(rule.style);
    }
    return {};
  }

  private evaluateRule(rule: ConditionalFormatRule, row: Record<string, any>): boolean {
    const cellVal = (row[rule.fieldKey] ?? '').toString();
    const ruleVal = rule.value;
    switch (rule.operator) {
      case 'eq': return cellVal.toLowerCase() === ruleVal.toLowerCase();
      case 'neq': return cellVal.toLowerCase() !== ruleVal.toLowerCase();
      case 'contains': return cellVal.toLowerCase().includes(ruleVal.toLowerCase());
      case 'gt': return parseFloat(cellVal) > parseFloat(ruleVal);
      case 'lt': return parseFloat(cellVal) < parseFloat(ruleVal);
      case 'gte': return parseFloat(cellVal) >= parseFloat(ruleVal);
      case 'lte': return parseFloat(cellVal) <= parseFloat(ruleVal);
      case 'empty': return !cellVal || cellVal === '-';
      case 'notEmpty': return !!cellVal && cellVal !== '-';
      default: return false;
    }
  }

  buildStyleObj(style: ConditionalFormatStyle): Record<string, string> {
    const s: Record<string, string> = {};
    if (style.backgroundColor) s['background-color'] = style.backgroundColor;
    if (style.textColor) s['color'] = style.textColor;
    if (style.fontWeight && style.fontWeight !== 'normal') s['font-weight'] = style.fontWeight;
    if (style.fontStyle && style.fontStyle !== 'normal') s['font-style'] = style.fontStyle;
    return s;
  }

  computeAggregation(col: ReportColumnConfig): string {
    if (!this.previewData?.data?.length || !col.aggregation || col.aggregation === 'none') return '';
    const values = this.previewData.data.map((r) => r[col.fieldKey]).filter((v) => v != null);
    switch (col.aggregation) {
      case 'count': return 'Dem: ' + values.length;
      case 'sum': { const sum = values.reduce((s, v) => s + (parseFloat(v?.toString() ?? '0') || 0), 0); return 'Tong: ' + sum.toFixed(2); }
      case 'avg': { const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n)); return nums.length ? 'TB: ' + (nums.reduce((s, n) => s + n, 0) / nums.length).toFixed(2) : ''; }
      case 'min': { const mins = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n)); return mins.length ? 'Min: ' + Math.min(...mins) : ''; }
      case 'max': { const maxs = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n)); return maxs.length ? 'Max: ' + Math.max(...maxs) : ''; }
      default: return '';
    }
  }

  getGroupedData(): { groupValue: string; rows: Record<string, any>[] }[] | null {
    if (!this.config.groupBy || !this.previewData?.data?.length) return null;
    const groupKey = this.config.groupBy;
    const groups = new Map<string, Record<string, any>[]>();
    for (const row of this.previewData.data) {
      const gv = (row[groupKey] ?? '-').toString();
      if (!groups.has(gv)) groups.set(gv, []);
      groups.get(gv)!.push(row);
    }
    return Array.from(groups.entries()).map(([groupValue, rows]) => ({ groupValue, rows }));
  }

  computeGroupAggregation(agg: GroupAggregation, rows: Record<string, any>[]): string {
    const values = rows.map((r) => r[agg.fieldKey]).filter((v) => v != null);
    switch (agg.type) {
      case 'count': return values.length.toString();
      case 'sum': { const s = values.reduce((acc, v) => acc + (parseFloat(v?.toString() ?? '0') || 0), 0); return s.toFixed(2); }
      case 'avg': { const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n)); return nums.length ? (nums.reduce((a, n) => a + n, 0) / nums.length).toFixed(2) : '-'; }
      case 'min': { const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n)); return nums.length ? Math.min(...nums).toString() : '-'; }
      case 'max': { const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n)); return nums.length ? Math.max(...nums).toString() : '-'; }
      default: return '';
    }
  }

  markDirty() { this.isDirty = true; }

  save() {
    if (!this.report || this.isSaving) return;
    this.isSaving = true;
    this.formsService.updateReportTemplate(this.reportId, {
      name: this.report.name, description: this.report.description, reportType: this.report.reportType, configurationJson: JSON.stringify(this.config),
    }).subscribe({ next: () => { this.isSaving = false; this.isDirty = false; }, error: () => { this.isSaving = false; alert('Loi khi luu cau hinh.'); } });
  }

  saveAndPreview() {
    if (!this.report) return;
    this.isSaving = true;
    this.formsService.updateReportTemplate(this.reportId, {
      name: this.report.name, description: this.report.description, reportType: this.report.reportType, configurationJson: JSON.stringify(this.config),
    }).subscribe({ next: () => { this.isSaving = false; this.isDirty = false; this.router.navigate(['/forms/reports', this.reportId]); }, error: () => { this.isSaving = false; alert('Loi khi luu cau hinh.'); } });
  }

  goBack() {
    if (this.isDirty && !confirm('Ban co thay doi chua luu. Thoat khong?')) return;
    this.router.navigate(['/forms/reports', this.reportId]);
  }

  getReportTypeName(): string {
    if (!this.report) return '';
    const labels: Record<number, string> = {
      [ReportType.Table]: 'Bang du lieu', [ReportType.BarChart]: 'Bieu do cot', [ReportType.LineChart]: 'Bieu do duong', [ReportType.PieChart]: 'Bieu do tron', [ReportType.Summary]: 'Tong hop',
    };
    return labels[this.report.reportType] || '';
  }
}
