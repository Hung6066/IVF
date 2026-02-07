import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { FormsService, ReportData, ReportType } from '../forms.service';

@Component({
    selector: 'app-report-viewer',
    standalone: true,
    imports: [CommonModule, FormsModule, DatePipe],
    templateUrl: './report-viewer.component.html',
    styleUrls: ['./report-viewer.component.scss']
})
export class ReportViewerComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly route = inject(ActivatedRoute);

    ReportType = ReportType;
    reportId = '';
    reportData: ReportData | null = null;
    filterFrom = '';
    filterTo = '';

    private readonly colors = ['#10b981', '#3b82f6', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#06b6d4'];

    ngOnInit() {
        this.route.params.subscribe(params => {
            if (params['id']) {
                this.reportId = params['id'];
                this.reload();
            }
        });
    }

    reload() {
        const from = this.filterFrom ? new Date(this.filterFrom) : undefined;
        const to = this.filterTo ? new Date(this.filterTo) : undefined;

        this.formsService.generateReport(this.reportId, from, to).subscribe(data => {
            this.reportData = data;
        });
    }

    getColumns(): string[] {
        if (!this.reportData?.data?.length) return [];
        return Object.keys(this.reportData.data[0]);
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
        const max = Math.max(...counts.map(c => c.count));

        return counts.map((c, i) => ({
            label: c.key,
            value: c.count,
            percentage: this.reportData?.template.reportType === ReportType.PieChart
                ? (c.count / total) * 100
                : (c.count / max) * 100,
            color: this.colors[i % this.colors.length]
        }));
    }

    exportCSV() {
        if (!this.reportData?.data?.length) return;

        const headers = this.getColumns();
        const rows = this.reportData.data.map(row =>
            headers.map(h => this.formatValue(row[h])).join(',')
        );
        const csv = [headers.join(','), ...rows].join('\n');

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
}
