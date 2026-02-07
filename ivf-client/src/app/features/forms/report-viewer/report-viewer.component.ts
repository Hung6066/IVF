import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { FormsService, ReportData, ReportType } from '../forms.service';

@Component({
    selector: 'app-report-viewer',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
        <div class="report-viewer-container">
            @if (reportData) {
                <header class="report-header">
                    <div class="header-content">
                        <h1>{{ reportData.template.name }}</h1>
                        <p>{{ reportData.template.description }}</p>
                    </div>
                    <div class="header-actions">
                        <button class="btn btn-secondary" (click)="exportCSV()">📥 Xuất CSV</button>
                        <button class="btn btn-secondary" (click)="print()">🖨️ In</button>
                    </div>
                </header>

                <!-- Filters -->
                <div class="filters-bar">
                    <div class="filter-group">
                        <label>Từ ngày:</label>
                        <input type="date" [(ngModel)]="filterFrom" (change)="reload()">
                    </div>
                    <div class="filter-group">
                        <label>Đến ngày:</label>
                        <input type="date" [(ngModel)]="filterTo" (change)="reload()">
                    </div>
                    <button class="btn btn-primary" (click)="reload()">🔄 Tải lại</button>
                </div>

                <!-- Summary Cards -->
                @if (reportData.summary) {
                    <div class="summary-cards">
                        <div class="summary-card">
                            <span class="card-icon">📊</span>
                            <div class="card-content">
                                <h3>{{ reportData.summary.totalResponses }}</h3>
                                <p>Tổng phản hồi</p>
                            </div>
                        </div>
                        @for (avg of getAverages(); track avg.key) {
                            <div class="summary-card">
                                <span class="card-icon">📈</span>
                                <div class="card-content">
                                    <h3>{{ avg.value | number:'1.2-2' }}</h3>
                                    <p>TB {{ avg.key }}</p>
                                </div>
                            </div>
                        }
                    </div>
                }

                <!-- Report Content -->
                <div class="report-content">
                    @switch (reportData.template.reportType) {
                        @case (ReportType.Table) {
                            <div class="table-wrapper">
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            @for (col of getColumns(); track col) {
                                                <th>{{ col }}</th>
                                            }
                                        </tr>
                                    </thead>
                                    <tbody>
                                        @for (row of reportData.data; track $index) {
                                            <tr>
                                                @for (col of getColumns(); track col) {
                                                    <td>{{ formatValue(row[col]) }}</td>
                                                }
                                            </tr>
                                        }
                                    </tbody>
                                </table>
                            </div>
                        }
                        @case (ReportType.BarChart) {
                            <div class="chart-container">
                                <div class="simple-bar-chart">
                                    @for (item of getChartData(); track item.label) {
                                        <div class="bar-item">
                                            <span class="bar-label">{{ item.label }}</span>
                                            <div class="bar-track">
                                                <div class="bar-fill" [style.width.%]="item.percentage"></div>
                                            </div>
                                            <span class="bar-value">{{ item.value }}</span>
                                        </div>
                                    }
                                </div>
                            </div>
                        }
                        @case (ReportType.PieChart) {
                            <div class="chart-container">
                                <div class="pie-legend">
                                    @for (item of getChartData(); track item.label) {
                                        <div class="legend-item">
                                            <span class="legend-color" [style.background]="item.color"></span>
                                            <span>{{ item.label }}: {{ item.value }} ({{ item.percentage | number:'1.1-1' }}%)</span>
                                        </div>
                                    }
                                </div>
                            </div>
                        }
                        @case (ReportType.Summary) {
                            <div class="summary-report">
                                <div class="stat-grid">
                                    @for (stat of getValueCounts(); track stat.key) {
                                        <div class="stat-item">
                                            <span class="stat-value">{{ stat.count }}</span>
                                            <span class="stat-label">{{ stat.key }}</span>
                                        </div>
                                    }
                                </div>
                            </div>
                        }
                        @default {
                            <div class="table-wrapper">
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            @for (col of getColumns(); track col) {
                                                <th>{{ col }}</th>
                                            }
                                        </tr>
                                    </thead>
                                    <tbody>
                                        @for (row of reportData.data; track $index) {
                                            <tr>
                                                @for (col of getColumns(); track col) {
                                                    <td>{{ formatValue(row[col]) }}</td>
                                                }
                                            </tr>
                                        }
                                    </tbody>
                                </table>
                            </div>
                        }
                    }
                </div>
            } @else {
                <div class="loading">
                    <div class="spinner"></div>
                    <p>Đang tải báo cáo...</p>
                </div>
            }
        </div>
    `,
    styles: [`
        .report-viewer-container {
            padding: 24px;
            max-width: 1200px;
            margin: 0 auto;
        }

        .report-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 24px;
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            border-radius: 16px;
            color: white;
            margin-bottom: 24px;
        }

        .report-header h1 {
            margin: 0 0 8px;
        }

        .report-header p {
            margin: 0;
            opacity: 0.9;
        }

        .header-actions {
            display: flex;
            gap: 12px;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
            display: inline-flex;
            align-items: center;
            gap: 8px;
        }

        .btn-primary {
            background: #10b981;
            color: white;
        }

        .btn-secondary {
            background: rgba(255,255,255,0.2);
            color: white;
        }

        .filters-bar {
            display: flex;
            gap: 24px;
            align-items: center;
            padding: 16px 20px;
            background: white;
            border-radius: 12px;
            margin-bottom: 24px;
        }

        .filter-group {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .filter-group label {
            color: #64748b;
            font-size: 14px;
        }

        .filter-group input {
            padding: 8px 12px;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 16px;
            margin-bottom: 24px;
        }

        .summary-card {
            display: flex;
            align-items: center;
            gap: 16px;
            padding: 20px;
            background: white;
            border-radius: 12px;
        }

        .card-icon {
            font-size: 32px;
        }

        .card-content h3 {
            margin: 0;
            font-size: 28px;
            color: #10b981;
        }

        .card-content p {
            margin: 4px 0 0;
            color: #64748b;
            font-size: 13px;
        }

        .report-content {
            background: white;
            border-radius: 12px;
            padding: 24px;
        }

        .table-wrapper {
            overflow-x: auto;
        }

        .data-table {
            width: 100%;
            border-collapse: collapse;
        }

        .data-table th {
            background: #f8fafc;
            padding: 12px 16px;
            text-align: left;
            font-weight: 600;
            color: #475569;
            font-size: 13px;
        }

        .data-table td {
            padding: 12px 16px;
            border-top: 1px solid #f1f5f9;
        }

        .chart-container {
            padding: 20px;
        }

        .simple-bar-chart {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .bar-item {
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .bar-label {
            width: 120px;
            font-size: 13px;
            color: #374151;
        }

        .bar-track {
            flex: 1;
            height: 24px;
            background: #f1f5f9;
            border-radius: 4px;
            overflow: hidden;
        }

        .bar-fill {
            height: 100%;
            background: linear-gradient(90deg, #10b981, #059669);
            border-radius: 4px;
            transition: width 0.5s ease;
        }

        .bar-value {
            width: 60px;
            text-align: right;
            font-weight: 600;
            color: #10b981;
        }

        .pie-legend {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .legend-item {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .legend-color {
            width: 16px;
            height: 16px;
            border-radius: 4px;
        }

        .summary-report .stat-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
            gap: 16px;
        }

        .stat-item {
            text-align: center;
            padding: 20px;
            background: #f8fafc;
            border-radius: 8px;
        }

        .stat-value {
            display: block;
            font-size: 28px;
            font-weight: 700;
            color: #10b981;
        }

        .stat-label {
            display: block;
            margin-top: 4px;
            color: #64748b;
            font-size: 12px;
        }

        .loading {
            text-align: center;
            padding: 80px;
        }

        .spinner {
            width: 48px;
            height: 48px;
            border: 4px solid #e2e8f0;
            border-top-color: #10b981;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin: 0 auto 16px;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        /* Responsive */
        @media (max-width: 768px) {
            .report-viewer-container {
                padding: 12px;
            }

            .report-header {
                flex-direction: column;
                gap: 16px;
                padding: 16px;
            }

            .report-header h1 {
                font-size: 20px;
            }

            .filters-bar {
                flex-direction: column;
                align-items: stretch;
            }

            .filter-group {
                width: 100%;
            }

            .filter-group input {
                flex: 1;
            }

            .summary-cards {
                grid-template-columns: repeat(2, 1fr);
            }

            .data-table {
                font-size: 13px;
            }

            .data-table th,
            .data-table td {
                padding: 8px 10px;
            }

            .bar-label {
                width: 80px;
                font-size: 11px;
            }
        }

        @media print {
            .header-actions, .filters-bar {
                display: none !important;
            }

            .report-header {
                -webkit-print-color-adjust: exact;
            }

            .report-content {
                box-shadow: none;
                border: 1px solid #e2e8f0;
            }
        }
    `]
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
