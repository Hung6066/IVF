import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormsService, FormCategory, CreateCategoryRequest } from '../forms.service';

@Component({
    selector: 'app-category-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
        <div class="category-management">
            <header class="page-header">
                <div class="header-content">
                    <h1>📁 Quản lý Danh mục</h1>
                    <p>Tạo và quản lý danh mục biểu mẫu</p>
                </div>
                <button class="btn btn-primary" (click)="openModal()">
                    ➕ Thêm danh mục
                </button>
            </header>

            <div class="categories-grid">
                @for (cat of categories; track cat.id) {
                    <div class="category-card" [class.inactive]="!cat.isActive">
                        <div class="card-header">
                            <span class="icon">{{ cat.iconName || '📁' }}</span>
                            <div class="card-actions">
                                <button class="btn-icon" (click)="edit(cat)" title="Sửa">✏️</button>
                                <button class="btn-icon danger" (click)="delete(cat)" title="Xóa">🗑️</button>
                            </div>
                        </div>
                        <h3>{{ cat.name }}</h3>
                        <p class="description">{{ cat.description || 'Không có mô tả' }}</p>
                        <div class="card-footer">
                            <span class="template-count">{{ cat.templateCount || 0 }} biểu mẫu</span>
                            <span class="status" [class.active]="cat.isActive">
                                {{ cat.isActive ? '✅ Hoạt động' : '⏸️ Tạm dừng' }}
                            </span>
                        </div>
                    </div>
                }

                @if (categories.length === 0) {
                    <div class="empty-state">
                        <span class="empty-icon">📂</span>
                        <h3>Chưa có danh mục nào</h3>
                        <p>Tạo danh mục đầu tiên để tổ chức biểu mẫu</p>
                        <button class="btn btn-primary" (click)="openModal()">Tạo danh mục</button>
                    </div>
                }
            </div>

            <!-- Modal -->
            @if (showModal) {
                <div class="modal-overlay" (click)="closeModal()">
                    <div class="modal" (click)="$event.stopPropagation()">
                        <div class="modal-header">
                            <h3>{{ editingCategory ? 'Sửa danh mục' : 'Thêm danh mục mới' }}</h3>
                            <button class="close-btn" (click)="closeModal()">✕</button>
                        </div>
                        <div class="modal-body">
                            <div class="form-group">
                                <label>Tên danh mục *</label>
                                <input type="text" [(ngModel)]="formData.name" placeholder="VD: Phòng Xét nghiệm">
                            </div>
                            <div class="form-group">
                                <label>Mô tả</label>
                                <textarea [(ngModel)]="formData.description" placeholder="Mô tả danh mục..."></textarea>
                            </div>
                            <div class="form-group">
                                <label>Icon (emoji)</label>
                                <div class="icon-picker">
                                    @for (icon of commonIcons; track icon) {
                                        <button 
                                            type="button" 
                                            class="icon-option"
                                            [class.selected]="formData.iconName === icon"
                                            (click)="formData.iconName = icon">
                                            {{ icon }}
                                        </button>
                                    }
                                </div>
                            </div>
                            <div class="form-group">
                                <label>Thứ tự hiển thị</label>
                                <input type="number" [(ngModel)]="formData.displayOrder" min="0">
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button class="btn btn-secondary" (click)="closeModal()">Hủy</button>
                            <button class="btn btn-primary" (click)="save()" [disabled]="!formData.name">
                                {{ editingCategory ? 'Cập nhật' : 'Tạo mới' }}
                            </button>
                        </div>
                    </div>
                </div>
            }
        </div>
    `,
    styles: [`
        .category-management {
            padding: 24px;
            max-width: 1200px;
            margin: 0 auto;
        }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 24px;
            background: linear-gradient(135deg, #06b6d4 0%, #0891b2 100%);
            border-radius: 16px;
            color: white;
            margin-bottom: 24px;
        }

        .page-header h1 { margin: 0 0 8px; }
        .page-header p { margin: 0; opacity: 0.9; }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 8px;
        }

        .btn-primary { background: white; color: #0891b2; }
        .btn-secondary { background: #e2e8f0; color: #475569; }

        .categories-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 20px;
        }

        .category-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            transition: all 0.2s;
        }

        .category-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 16px rgba(0,0,0,0.12);
        }

        .category-card.inactive { opacity: 0.6; }

        .card-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            margin-bottom: 12px;
        }

        .card-header .icon {
            font-size: 32px;
        }

        .card-actions {
            display: flex;
            gap: 4px;
        }

        .btn-icon {
            background: none;
            border: none;
            padding: 6px;
            cursor: pointer;
            border-radius: 6px;
            font-size: 14px;
        }

        .btn-icon:hover { background: #f1f5f9; }
        .btn-icon.danger:hover { background: #fee2e2; }

        .category-card h3 {
            margin: 0 0 8px;
            font-size: 18px;
            color: #1e293b;
        }

        .description {
            color: #64748b;
            font-size: 14px;
            margin: 0 0 16px;
            line-height: 1.5;
        }

        .card-footer {
            display: flex;
            justify-content: space-between;
            font-size: 12px;
            color: #94a3b8;
            padding-top: 12px;
            border-top: 1px solid #f1f5f9;
        }

        .status.active { color: #22c55e; }

        .empty-state {
            grid-column: 1 / -1;
            text-align: center;
            padding: 60px;
            background: #f8fafc;
            border-radius: 12px;
        }

        .empty-icon { font-size: 64px; display: block; margin-bottom: 16px; }

        /* Modal */
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
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 20px 24px;
            border-bottom: 1px solid #e2e8f0;
        }

        .modal-header h3 { margin: 0; }

        .close-btn {
            background: none;
            border: none;
            font-size: 20px;
            cursor: pointer;
        }

        .modal-body { padding: 24px; }

        .form-group { margin-bottom: 20px; }

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
        }

        .icon-picker {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
        }

        .icon-option {
            width: 40px;
            height: 40px;
            border: 2px solid #e2e8f0;
            border-radius: 8px;
            background: white;
            cursor: pointer;
            font-size: 20px;
            transition: all 0.2s;
        }

        .icon-option:hover { border-color: #06b6d4; }
        .icon-option.selected { border-color: #06b6d4; background: #ecfeff; }

        .modal-footer {
            display: flex;
            justify-content: flex-end;
            gap: 12px;
            padding: 16px 24px;
            border-top: 1px solid #e2e8f0;
        }
    `]
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
