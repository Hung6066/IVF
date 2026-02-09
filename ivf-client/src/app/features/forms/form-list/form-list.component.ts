import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FormsService, FormCategory, FormTemplate, FieldTypeLabels } from '../forms.service';

@Component({
  selector: 'app-form-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './form-list.component.html',
  styleUrls: ['./form-list.component.scss'],
})
export class FormListComponent implements OnInit {
  private readonly formsService = inject(FormsService);
  private readonly router = inject(Router);

  categories: FormCategory[] = [];
  templates: FormTemplate[] = [];
  filteredTemplates: FormTemplate[] = [];
  selectedCategoryId: string | null = null;
  openMenuId: string | null = null;
  showCategoryModal = false;
  newCategory = { name: '', description: '', iconName: '' };

  // Template Library
  showLibraryModal = false;
  libraryTemplates: FormTemplate[] = [];
  duplicatingId: string | null = null;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.formsService.getCategories().subscribe((cats) => (this.categories = cats));
    this.formsService.getTemplates(undefined, undefined, true).subscribe((templates) => {
      this.templates = templates;
      this.filterTemplates();
    });
  }

  selectCategory(categoryId: string | null) {
    this.selectedCategoryId = categoryId;
    this.filterTemplates();
  }

  filterTemplates() {
    if (this.selectedCategoryId) {
      this.filteredTemplates = this.templates.filter(
        (t) => t.categoryId === this.selectedCategoryId,
      );
    } else {
      this.filteredTemplates = [...this.templates];
    }
  }

  createTemplate() {
    this.router.navigate(['/forms/builder']);
  }

  editTemplate(id: string) {
    this.openMenuId = null;
    this.router.navigate(['/forms/builder', id]);
  }

  previewTemplate(id: string) {
    this.openMenuId = null;
    this.router.navigate(['/forms/fill', id]);
  }

  chatMode(id: string) {
    this.openMenuId = null;
    this.router.navigate(['/forms/chat', id]);
  }

  publishTemplate(id: string) {
    this.formsService.publishTemplate(id).subscribe(() => this.loadData());
    this.openMenuId = null;
  }

  unpublishTemplate(id: string) {
    this.formsService.unpublishTemplate(id).subscribe(() => this.loadData());
    this.openMenuId = null;
  }

  deleteTemplate(id: string) {
    if (confirm('Bạn có chắc chắn muốn xóa biểu mẫu này?')) {
      this.formsService.deleteTemplate(id).subscribe(() => this.loadData());
    }
    this.openMenuId = null;
  }

  duplicateTemplate(id: string) {
    this.formsService.duplicateTemplate(id).subscribe(() => this.loadData());
    this.openMenuId = null;
  }

  toggleMenu(id: string) {
    this.openMenuId = this.openMenuId === id ? null : id;
  }

  saveCategory() {
    this.formsService.createCategory(this.newCategory).subscribe(() => {
      this.loadData();
      this.showCategoryModal = false;
      this.newCategory = { name: '', description: '', iconName: '' };
    });
  }

  openLibrary() {
    const libCategory = this.categories.find((c) => c.name === '📚 Thư viện mẫu');
    if (libCategory) {
      this.libraryTemplates = this.templates.filter((t) => t.categoryId === libCategory.id);
    } else {
      this.libraryTemplates = [];
    }
    this.showLibraryModal = true;
  }

  useTemplate(id: string) {
    this.duplicatingId = id;
    this.formsService.duplicateTemplate(id).subscribe({
      next: (newTemplate) => {
        this.duplicatingId = null;
        this.showLibraryModal = false;
        this.loadData();
        this.router.navigate(['/forms/builder', newTemplate.id]);
      },
      error: () => {
        this.duplicatingId = null;
      },
    });
  }
}
