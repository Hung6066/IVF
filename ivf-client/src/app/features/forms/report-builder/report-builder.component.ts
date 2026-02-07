import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FormsService, FormTemplate, ReportTemplate, ReportType } from '../forms.service';

@Component({
    selector: 'app-report-builder',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './report-builder.component.html',
    styleUrls: ['./report-builder.component.scss']
})
export class ReportBuilderComponent implements OnInit {
    private readonly formsService = inject(FormsService);
    private readonly router = inject(Router);

    templates: FormTemplate[] = [];
    reports: ReportTemplate[] = [];
    showCreateModal = false;

    newReport = {
        formTemplateId: '',
        name: '',
        description: '',
        reportType: ReportType.Table
    };

    reportTypes = [
        { value: ReportType.Table, icon: '📋', label: 'Bảng' },
        { value: ReportType.BarChart, icon: '📊', label: 'Cột' },
        { value: ReportType.LineChart, icon: '📈', label: 'Đường' },
        { value: ReportType.PieChart, icon: '🥧', label: 'Tròn' },
        { value: ReportType.Summary, icon: '📝', label: 'Tổng hợp' }
    ];

    ngOnInit() {
        this.formsService.getTemplates(undefined, true).subscribe(t => {
            this.templates = t;
            this.loadAllReports();
        });
    }

    loadAllReports() {
        const allReports: ReportTemplate[] = [];
        for (const template of this.templates) {
            this.formsService.getReportTemplates(template.id).subscribe(reports => {
                allReports.push(...reports);
                this.reports = [...allReports];
            });
        }
    }

    getReportTypeIcon(type: ReportType): string {
        const icons: { [key: number]: string } = {
            [ReportType.Table]: '📋',
            [ReportType.BarChart]: '📊',
            [ReportType.LineChart]: '📈',
            [ReportType.PieChart]: '🥧',
            [ReportType.Summary]: '📝'
        };
        return icons[type] || '📊';
    }

    getReportTypeLabel(type: ReportType): string {
        const labels: { [key: number]: string } = {
            [ReportType.Table]: 'Bảng',
            [ReportType.BarChart]: 'Biểu đồ cột',
            [ReportType.LineChart]: 'Biểu đồ đường',
            [ReportType.PieChart]: 'Biểu đồ tròn',
            [ReportType.Summary]: 'Báo cáo tổng hợp'
        };
        return labels[type] || '';
    }

    createReport() {
        this.formsService.createReportTemplate({
            formTemplateId: this.newReport.formTemplateId,
            name: this.newReport.name,
            description: this.newReport.description,
            reportType: this.newReport.reportType,
            configurationJson: '{}',
            createdByUserId: 'current-user'
        }).subscribe(report => {
            this.reports.push(report);
            this.showCreateModal = false;
            this.newReport = { formTemplateId: '', name: '', description: '', reportType: ReportType.Table };
        });
    }

    viewReport(report: ReportTemplate) {
        this.router.navigate(['/forms/reports', report.id]);
    }

    deleteReport(report: ReportTemplate) {
        if (confirm('Xóa báo cáo này?')) {
            this.formsService.deleteReportTemplate(report.id).subscribe(() => {
                this.reports = this.reports.filter(r => r.id !== report.id);
            });
        }
    }
}
