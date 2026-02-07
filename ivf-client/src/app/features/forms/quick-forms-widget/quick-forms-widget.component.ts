import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsService, FormTemplate } from '../forms.service';

@Component({
    selector: 'app-quick-forms-widget',
    standalone: true,
    imports: [CommonModule],
    templateUrl: './quick-forms-widget.component.html',
    styleUrls: ['./quick-forms-widget.component.scss']
})
export class QuickFormsWidgetComponent implements OnInit {
    private formsService = inject(FormsService);
    private router = inject(Router);

    @Input() categoryFilter?: string; // Filter forms by category
    @Input() maxForms = 6; // Maximum number of forms to display
    @Input() title = 'Biểu mẫu nhanh';
    @Input() showViewAll = true;

    templates: FormTemplate[] = [];
    filteredForms: FormTemplate[] = [];
    loading = true;

    ngOnInit() {
        this.loadForms();
    }

    loadForms() {
        this.loading = true;
        this.formsService.getTemplates().subscribe({
            next: (templates) => {
                this.templates = templates;
                this.filterForms();
                this.loading = false;
            },
            error: (err) => {
                console.error('Error loading forms:', err);
                this.loading = false;
            }
        });
    }

    filterForms() {
        let forms = this.templates;

        // Filter by category if specified
        if (this.categoryFilter) {
            forms = forms.filter(f => f.categoryId === this.categoryFilter);
        }

        // Limit to maxForms
        this.filteredForms = forms.slice(0, this.maxForms);
    }

    getFormIcon(form: FormTemplate): string {
        // Return icon based on form name or category
        const name = form.name.toLowerCase();
        if (name.includes('checklist')) return '✓';
        if (name.includes('equipment')) return '🔧';
        if (name.includes('treatment') || name.includes('procedure')) return '💉';
        if (name.includes('patient') || name.includes('care')) return '👤';
        if (name.includes('lab') || name.includes('embryo')) return '🧪';
        if (name.includes('ultrasound')) return '📡';
        if (name.includes('report')) return '📊';
        if (name.includes('registration') || name.includes('intake')) return '📝';
        return '📋';
    }

    openForm(form: FormTemplate) {
        this.router.navigate(['/forms/fill', form.id]);
    }

    viewAllForms() {
        this.router.navigate(['/forms']);
    }
}
