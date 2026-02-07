import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormsService, FormCategory, CreateCategoryRequest } from '../forms.service';

@Component({
    selector: 'app-category-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './category-management.component.html',
    styleUrls: ['./category-management.component.scss']
})
export class CategoryManagementComponent implements OnInit {
    private readonly formsService = inject(FormsService);

    categories: FormCategory[] = [];
    showModal = false;
    editingCategory: FormCategory | null = null;
    formData: CreateCategoryRequest = { name: '', description: '', iconName: '📁', displayOrder: 0 };

    commonIcons = ['📁', '🧪', '💉', '🏥', '📋', '💊', '🔬', '👨‍⚕️', '👩‍⚕️', '❤️', '🩺', '📊', '📈', '🗂️', '📝'];

    ngOnInit() {
        this.loadCategories();
    }

    loadCategories() {
        this.formsService.getCategories(false).subscribe(cats => this.categories = cats);
    }

    openModal() {
        this.editingCategory = null;
        this.formData = { name: '', description: '', iconName: '📁', displayOrder: this.categories.length };
        this.showModal = true;
    }

    closeModal() {
        this.showModal = false;
        this.editingCategory = null;
    }

    edit(cat: FormCategory) {
        this.editingCategory = cat;
        this.formData = {
            name: cat.name,
            description: cat.description || '',
            iconName: cat.iconName || '📁',
            displayOrder: cat.displayOrder
        };
        this.showModal = true;
    }

    save() {
        if (this.editingCategory) {
            this.formsService.updateCategory(this.editingCategory.id, this.formData).subscribe(() => {
                this.loadCategories();
                this.closeModal();
            });
        } else {
            this.formsService.createCategory(this.formData).subscribe(() => {
                this.loadCategories();
                this.closeModal();
            });
        }
    }

    delete(cat: FormCategory) {
        if (confirm(`Xóa danh mục "${cat.name}"? Các biểu mẫu trong danh mục này sẽ không bị xóa.`)) {
            this.formsService.deleteCategory(cat.id).subscribe(() => this.loadCategories());
        }
    }
}
