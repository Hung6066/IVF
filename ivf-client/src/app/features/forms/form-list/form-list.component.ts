import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FormsService, FormCategory, FormTemplate, FieldTypeLabels } from '../forms.service';

@Component({
    selector: 'app-form-list',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
        <div class="form-list-container">
            <header class="page-header">
                <div class="header-content">
                    <h1>📝 Quản lý Biểu mẫu</h1>
                    <p class="subtitle">Tạo và quản lý biểu mẫu động cho các phòng ban</p>
                </div>
                <div class="header-actions">
                    <button class="btn btn-primary" (click)="createTemplate()">
                        <span class="icon">➕</span>
                        Tạo biểu mẫu mới
                    </button>
                </div>
            </header>

            <!-- Categories Section -->
            <section class="categories-section">
                <div class="section-header">
                    <h2>Danh mục</h2>
                    <button class="btn btn-sm btn-secondary" (click)="showCategoryModal = true">
                        Thêm danh mục
                    </button>
                </div>
                <div class="category-tabs">
                    <button 
                        class="category-tab"
                        [class.active]="!selectedCategoryId"
                        (click)="selectCategory(null)">
                        Tất cả
                    </button>
                    @for (category of categories; track category.id) {
                        <button 
                            class="category-tab"
                            [class.active]="selectedCategoryId === category.id"
                            (click)="selectCategory(category.id)">
                            {{ category.iconName || '📁' }} {{ category.name }}
                            @if (category.templateCount) {
                                <span class="badge">{{ category.templateCount }}</span>
                            }
                        </button>
                    }
                </div>
            </section>

            <!-- Templates Grid -->
            <section class="templates-section">
                <div class="templates-grid">
                    @for (template of filteredTemplates; track template.id) {
                        <div class="template-card" [class.published]="template.isPublished">
                            <div class="template-header">
                                <div class="template-status" [class.published]="template.isPublished">
                                    {{ template.isPublished ? '✅ Đã xuất bản' : '📝 Bản nháp' }}
                                </div>
                                <div class="template-menu">
                                    <button class="menu-btn" (click)="toggleMenu(template.id)">⋮</button>
                                    @if (openMenuId === template.id) {
                                        <div class="dropdown-menu">
                                            <button (click)="editTemplate(template.id)">✏️ Chỉnh sửa</button>
                                            <button (click)="previewTemplate(template.id)">👁️ Xem trước</button>
                                            @if (!template.isPublished) {
                                                <button (click)="publishTemplate(template.id)">📤 Xuất bản</button>
                                            } @else {
                                                <button (click)="unpublishTemplate(template.id)">📥 Gỡ xuất bản</button>
                                            }
                                            <button class="danger" (click)="deleteTemplate(template.id)">🗑️ Xóa</button>
                                        </div>
                                    }
                                </div>
                            </div>
                            <div class="template-body" (click)="editTemplate(template.id)">
                                <h3>{{ template.name }}</h3>
                                <p class="description">{{ template.description || 'Không có mô tả' }}</p>
                                <div class="template-meta">
                                    <span class="category">{{ template.categoryName }}</span>
                                    <span class="version">v{{ template.version }}</span>
                                </div>
                            </div>
                            <div class="template-footer">
                                <span class="field-count">
                                    {{ template.fields?.length || 0 }} trường
                                </span>
                                <span class="created-date">
                                    {{ template.createdAt | date:'dd/MM/yyyy' }}
                                </span>
                            </div>
                        </div>
                    }

                    <!-- Empty State -->
                    @if (filteredTemplates.length === 0) {
                        <div class="empty-state">
                            <div class="empty-icon">📄</div>
                            <h3>Chưa có biểu mẫu nào</h3>
                            <p>Bắt đầu bằng cách tạo một biểu mẫu mới</p>
                            <button class="btn btn-primary" (click)="createTemplate()">
                                Tạo biểu mẫu đầu tiên
                            </button>
                        </div>
                    }
                </div>
            </section>

            <!-- Quick Access -->
            <section class="quick-access">
                <h3>Truy cập nhanh</h3>
                <div class="quick-links">
                    <a routerLink="/forms/responses" class="quick-link">
                        <span class="icon">📊</span>
                        <span>Xem phản hồi</span>
                    </a>
                    <a routerLink="/forms/reports" class="quick-link">
                        <span class="icon">📈</span>
                        <span>Báo cáo</span>
                    </a>
                </div>
            </section>

            <!-- Category Modal -->
            @if (showCategoryModal) {
                <div class="modal-overlay" (click)="showCategoryModal = false">
                    <div class="modal" (click)="$event.stopPropagation()">
                        <div class="modal-header">
                            <h3>Thêm danh mục mới</h3>
                            <button class="close-btn" (click)="showCategoryModal = false">✕</button>
                        </div>
                        <div class="modal-body">
                            <div class="form-group">
                                <label>Tên danh mục *</label>
                                <input type="text" [(ngModel)]="newCategory.name" placeholder="VD: Phòng Xét nghiệm">
                            </div>
                            <div class="form-group">
                                <label>Mô tả</label>
                                <textarea [(ngModel)]="newCategory.description" placeholder="Mô tả danh mục..."></textarea>
                            </div>
                            <div class="form-group">
                                <label>Icon (emoji)</label>
                                <input type="text" [(ngModel)]="newCategory.iconName" placeholder="🧪">
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button class="btn btn-secondary" (click)="showCategoryModal = false">Hủy</button>
                            <button class="btn btn-primary" (click)="saveCategory()" [disabled]="!newCategory.name">
                                Lưu danh mục
                            </button>
                        </div>
                    </div>
                </div>
            }
        </div>
    `,
    styles: [`
        .form-list-container {
            padding: 24px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 32px;
            padding: 24px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 16px;
            color: white;
        }

        .page-header h1 {
            margin: 0;
            font-size: 28px;
        }

        .subtitle {
            margin: 8px 0 0;
            opacity: 0.9;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            transition: all 0.2s;
        }

        .btn-primary {
            background: white;
            color: #667eea;
        }

        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        .btn-secondary {
            background: #e2e8f0;
            color: #475569;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 13px;
        }

        .categories-section {
            margin-bottom: 24px;
        }

        .section-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 16px;
        }

        .section-header h2 {
            margin: 0;
            font-size: 18px;
            color: #1e293b;
        }

        .category-tabs {
            display: flex;
            gap: 8px;
            flex-wrap: wrap;
        }

        .category-tab {
            padding: 8px 16px;
            border: 1px solid #e2e8f0;
            border-radius: 20px;
            background: white;
            cursor: pointer;
            transition: all 0.2s;
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .category-tab:hover {
            border-color: #667eea;
            color: #667eea;
        }

        .category-tab.active {
            background: #667eea;
            border-color: #667eea;
            color: white;
        }

        .badge {
            background: rgba(255,255,255,0.2);
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 12px;
        }

        .templates-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 20px;
        }

        .template-card {
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            overflow: hidden;
            transition: all 0.3s;
            border: 2px solid transparent;
        }

        .template-card:hover {
            transform: translateY(-4px);
            box-shadow: 0 8px 24px rgba(0,0,0,0.12);
        }

        .template-card.published {
            border-color: #22c55e;
        }

        .template-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px 16px;
            background: #f8fafc;
        }

        .template-status {
            font-size: 12px;
            color: #64748b;
        }

        .template-status.published {
            color: #22c55e;
        }

        .menu-btn {
            background: none;
            border: none;
            font-size: 18px;
            cursor: pointer;
            padding: 4px 8px;
            border-radius: 4px;
        }

        .menu-btn:hover {
            background: #e2e8f0;
        }

        .dropdown-menu {
            position: absolute;
            right: 0;
            top: 100%;
            background: white;
            border-radius: 8px;
            box-shadow: 0 4px 16px rgba(0,0,0,0.1);
            z-index: 100;
            min-width: 160px;
        }

        .dropdown-menu button {
            width: 100%;
            padding: 10px 16px;
            border: none;
            background: none;
            text-align: left;
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .dropdown-menu button:hover {
            background: #f1f5f9;
        }

        .dropdown-menu button.danger {
            color: #ef4444;
        }

        .template-body {
            padding: 20px;
            cursor: pointer;
        }

        .template-body h3 {
            margin: 0 0 8px;
            font-size: 18px;
            color: #1e293b;
        }

        .description {
            color: #64748b;
            font-size: 14px;
            margin: 0 0 12px;
            line-height: 1.5;
        }

        .template-meta {
            display: flex;
            gap: 12px;
        }

        .category {
            font-size: 12px;
            padding: 4px 8px;
            background: #e0e7ff;
            color: #4f46e5;
            border-radius: 4px;
        }

        .version {
            font-size: 12px;
            color: #94a3b8;
        }

        .template-footer {
            padding: 12px 20px;
            border-top: 1px solid #f1f5f9;
            display: flex;
            justify-content: space-between;
            font-size: 12px;
            color: #94a3b8;
        }

        .empty-state {
            grid-column: 1 / -1;
            text-align: center;
            padding: 60px 20px;
            background: #f8fafc;
            border-radius: 12px;
        }

        .empty-icon {
            font-size: 64px;
            margin-bottom: 16px;
        }

        .empty-state h3 {
            margin: 0 0 8px;
            color: #1e293b;
        }

        .empty-state p {
            color: #64748b;
            margin: 0 0 24px;
        }

        .quick-access {
            margin-top: 32px;
            padding: 24px;
            background: #f8fafc;
            border-radius: 12px;
        }

        .quick-access h3 {
            margin: 0 0 16px;
            font-size: 16px;
            color: #475569;
        }

        .quick-links {
            display: flex;
            gap: 16px;
        }

        .quick-link {
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 12px 20px;
            background: white;
            border-radius: 8px;
            text-decoration: none;
            color: #1e293b;
            transition: all 0.2s;
        }

        .quick-link:hover {
            background: #667eea;
            color: white;
        }

        .quick-link .icon {
            font-size: 20px;
        }

        /* Modal Styles */
        .modal-overlay {
            position: fixed;
            inset: 0;
            background: rgba(0,0,0,0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1000;
        }

        .modal {
            background: white;
            border-radius: 16px;
            width: 100%;
            max-width: 500px;
            max-height: 90vh;
            overflow: hidden;
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 20px 24px;
            border-bottom: 1px solid #e2e8f0;
        }

        .modal-header h3 {
            margin: 0;
        }

        .close-btn {
            background: none;
            border: none;
            font-size: 20px;
            cursor: pointer;
            color: #64748b;
        }

        .modal-body {
            padding: 24px;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: #374151;
        }

        .form-group input,
        .form-group textarea {
            width: 100%;
            padding: 10px 12px;
            border: 1px solid #d1d5db;
            border-radius: 8px;
            font-size: 14px;
        }

        .form-group textarea {
            min-height: 80px;
            resize: vertical;
        }

        .modal-footer {
            display: flex;
            justify-content: flex-end;
            gap: 12px;
            padding: 16px 24px;
            border-top: 1px solid #e2e8f0;
        }

        .template-menu {
            position: relative;
        }
    `]
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

    ngOnInit() {
        this.loadData();
    }

    loadData() {
        this.formsService.getCategories().subscribe(cats => this.categories = cats);
        this.formsService.getTemplates(undefined, undefined, true).subscribe(templates => {
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
            this.filteredTemplates = this.templates.filter(t => t.categoryId === this.selectedCategoryId);
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
}
