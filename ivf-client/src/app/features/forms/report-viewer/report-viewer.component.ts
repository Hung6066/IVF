import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  FormsService,
  ReportData,
  ReportType,
  ReportConfiguration,
  ReportColumnConfig,
  ConditionalFormatRule,
  ConditionalFormatStyle,
  GroupAggregation,
} from '../forms.service';

@Component({
  selector: 'app-report-viewer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './report-viewer.component.html',
  styleUrls: ['./report-viewer.component.scss'],
})
export class ReportViewerComponent implements OnInit {
  private readonly formsService = inject(FormsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  ReportType = ReportType;
  reportId = '';
  reportData: ReportData | null = null;
  filterFrom = '';
  filterTo = '';
  isExportingPdf = false;

  /** Parsed report configuration */
  config: ReportConfiguration | null = null;

  private readonly colors = [
    '#10b981',
    '#3b82f6',
    '#f59e0b',
    '#ef4444',
    '#8b5cf6',
    '#ec4899',
    '#06b6d4',
  ];

  ngOnInit() {
    this.route.params.subscribe((params) => {
      if (params['id']) {
        this.reportId = params['id'];
        this.reload();
      }
    });
  }

  reload() {
    const from = this.filterFrom ? new Date(this.filterFrom) : undefined;
    const to = this.filterTo ? new Date(this.filterTo) : undefined;

    this.formsService.generateReport(this.reportId, from, to).subscribe((data) => {
      this.reportData = data;
      this.parseConfig();
    });
  }

  private parseConfig() {
    this.config = null;
    if (!this.reportData?.template?.configurationJson) return;
    try {
      const parsed = JSON.parse(this.reportData.template.configurationJson);
      if (parsed && typeof parsed === 'object' && parsed.columns) {
        this.config = parsed;
      }
    } catch {
      // no config
    }
  }

  getColumns(): string[] {
    if (!this.reportData?.data?.length) return [];
    const allKeys = Object.keys(this.reportData.data[0]);

    // If config has visible columns defined, use that order and filter
    if (this.config?.columns?.length) {
      const visibleKeys = this.config.columns.filter((c) => c.visible).map((c) => c.fieldKey);
      // Only include keys that actually exist in data
      return visibleKeys.filter((k) => allKeys.includes(k));
    }

    return allKeys;
  }

  getColumnLabel(key: string): string {
    if (this.config?.columns?.length) {
      const col = this.config.columns.find((c) => c.fieldKey === key);
      if (col?.label) return col.label;
    }
    const defaults: Record<string, string> = {
      patientName: 'Bệnh nhân',
      submittedAt: 'Ngày nộp',
      status: 'Trạng thái',
      responseId: 'ID',
    };
    return defaults[key] || key;
  }

  formatValue(value: any): string {
    if (value === null || value === undefined) return '-';
    if (value instanceof Date) return new Date(value).toLocaleDateString('vi-VN');
    return value.toString();
  }

  getAverages(): { key: string; value: number }[] {
    if (!this.reportData?.summary?.fieldValueAverages) return [];
    return Object.entries(this.reportData.summary.fieldValueAverages)
      .filter(([, v]) => v != null)
      .map(([key, value]) => ({ key, value: value! }))
      .slice(0, 4);
  }

  getValueCounts(): { key: string; count: number }[] {
    if (!this.reportData?.summary?.fieldValueCounts) return [];
    return Object.entries(this.reportData.summary.fieldValueCounts)
      .map(([key, count]) => ({ key: key.split(':')[1] || key, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 12);
  }

  getChartData(): { label: string; value: number; percentage: number; color: string }[] {
    const counts = this.getValueCounts();
    const total = counts.reduce((sum, c) => sum + c.count, 0);
    const max = Math.max(...counts.map((c) => c.count));

    return counts.map((c, i) => ({
      label: c.key,
      value: c.count,
      percentage:
        this.reportData?.template.reportType === ReportType.PieChart
          ? (c.count / total) * 100
          : (c.count / max) * 100,
      color: this.colors[i % this.colors.length],
    }));
  }

  openConfig() {
    this.router.navigate(['/forms/reports', this.reportId, 'config']);
  }

  // ===== Conditional Formatting =====

  getRowStyles(row: Record<string, any>): Record<string, string> {
    if (!this.config?.conditionalFormats?.length) return {};
    for (const rule of this.config.conditionalFormats) {
      if (rule.applyTo !== 'row') continue;
      if (this.evaluateRule(rule, row)) return this.buildStyleObj(rule.style);
    }
    return {};
  }

  getCellStyles(row: Record<string, any>, fieldKey: string): Record<string, string> {
    if (!this.config?.conditionalFormats?.length) return {};
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
      case 'eq':
        return cellVal.toLowerCase() === ruleVal.toLowerCase();
      case 'neq':
        return cellVal.toLowerCase() !== ruleVal.toLowerCase();
      case 'contains':
        return cellVal.toLowerCase().includes(ruleVal.toLowerCase());
      case 'gt':
        return parseFloat(cellVal) > parseFloat(ruleVal);
      case 'lt':
        return parseFloat(cellVal) < parseFloat(ruleVal);
      case 'gte':
        return parseFloat(cellVal) >= parseFloat(ruleVal);
      case 'lte':
        return parseFloat(cellVal) <= parseFloat(ruleVal);
      case 'empty':
        return !cellVal || cellVal === '-';
      case 'notEmpty':
        return !!cellVal && cellVal !== '-';
      default:
        return false;
    }
  }

  private buildStyleObj(style: ConditionalFormatStyle): Record<string, string> {
    const s: Record<string, string> = {};
    if (style.backgroundColor) s['background-color'] = style.backgroundColor;
    if (style.textColor) s['color'] = style.textColor;
    if (style.fontWeight && style.fontWeight !== 'normal') s['font-weight'] = style.fontWeight;
    if (style.fontStyle && style.fontStyle !== 'normal') s['font-style'] = style.fontStyle;
    return s;
  }

  // ===== Grouping =====

  get isGrouped(): boolean {
    return !!this.config?.groupBy && !!this.reportData?.data?.length;
  }

  getGroupedData(): { groupValue: string; rows: Record<string, any>[] }[] | null {
    if (!this.config?.groupBy || !this.reportData?.data?.length) return null;
    const groupKey = this.config.groupBy;
    const groups = new Map<string, Record<string, any>[]>();
    for (const row of this.reportData.data) {
      const gv = (row[groupKey] ?? '-').toString();
      if (!groups.has(gv)) groups.set(gv, []);
      groups.get(gv)!.push(row);
    }
    return Array.from(groups.entries()).map(([groupValue, rows]) => ({ groupValue, rows }));
  }

  getGroupLabel(): string {
    if (!this.config?.groupBy) return '';
    return this.getColumnLabel(this.config.groupBy);
  }

  computeGroupAggregation(agg: GroupAggregation, rows: Record<string, any>[]): string {
    const values = rows.map((r) => r[agg.fieldKey]).filter((v) => v != null);
    switch (agg.type) {
      case 'count':
        return values.length.toString();
      case 'sum': {
        const s = values.reduce((acc, v) => acc + (parseFloat(v?.toString() ?? '0') || 0), 0);
        return s.toFixed(2);
      }
      case 'avg': {
        const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n));
        return nums.length ? (nums.reduce((a, n) => a + n, 0) / nums.length).toFixed(2) : '-';
      }
      case 'min': {
        const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n));
        return nums.length ? Math.min(...nums).toString() : '-';
      }
      case 'max': {
        const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n));
        return nums.length ? Math.max(...nums).toString() : '-';
      }
      default:
        return '';
    }
  }

  // ===== Footer aggregation =====

  get hasFooterAggregations(): boolean {
    return (
      !!this.config?.showFooterAggregations &&
      !!this.config?.columns?.some((c) => c.aggregation && c.aggregation !== 'none')
    );
  }

  computeColumnAggregation(colKey: string): string {
    const col = this.config?.columns?.find((c) => c.fieldKey === colKey);
    if (!col?.aggregation || col.aggregation === 'none' || !this.reportData?.data?.length)
      return '';
    const values = this.reportData.data.map((r) => r[colKey]).filter((v) => v != null);
    switch (col.aggregation) {
      case 'count':
        return 'Đếm: ' + values.length;
      case 'sum': {
        const sum = values.reduce((s, v) => s + (parseFloat(v?.toString() ?? '0') || 0), 0);
        return 'Tổng: ' + sum.toFixed(2);
      }
      case 'avg': {
        const nums = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n));
        return nums.length
          ? 'TB: ' + (nums.reduce((s, n) => s + n, 0) / nums.length).toFixed(2)
          : '';
      }
      case 'min': {
        const mins = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n));
        return mins.length ? 'Min: ' + Math.min(...mins) : '';
      }
      case 'max': {
        const maxs = values.map((v) => parseFloat(v?.toString() ?? '0')).filter((n) => !isNaN(n));
        return maxs.length ? 'Max: ' + Math.max(...maxs) : '';
      }
      default:
        return '';
    }
  }

  exportCSV() {
    if (!this.reportData?.data?.length) return;

    const headers = this.getColumns();
    const headerLabels = headers.map((h) => this.getColumnLabel(h));
    const rows = this.reportData.data.map((row) =>
      headers.map((h) => this.formatValue(row[h])).join(','),
    );
    const csv = [headerLabels.join(','), ...rows].join('\n');

    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.reportData.template.name}.csv`;
    link.click();
  }

  print() {
    window.print();
  }

  exportPdf(sign = false) {
    if (!this.reportId || this.isExportingPdf) return;
    this.isExportingPdf = true;

    const from = this.filterFrom ? new Date(this.filterFrom) : undefined;
    const to = this.filterTo ? new Date(this.filterTo) : undefined;

    this.formsService.exportReportPdf(this.reportId, from, to, undefined, sign).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${this.reportData?.template?.name || 'Report'}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
        this.isExportingPdf = false;
      },
      error: (err) => {
        this.isExportingPdf = false;
        if (err.error instanceof Blob) {
          const reader = new FileReader();
          reader.onload = () => {
            try {
              const json = JSON.parse(reader.result as string);
              alert(json.error || json.detail || 'Lỗi khi xuất PDF');
            } catch {
              alert('Lỗi khi xuất PDF. Vui lòng thử lại.');
            }
          };
          reader.readAsText(err.error);
        } else if (err.error?.error) {
          alert(err.error.error);
        } else {
          alert('Lỗi khi xuất PDF. Vui lòng thử lại.');
        }
      },
    });
  }
}
