import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-service-catalog',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="page-container">
      <header class="page-header">
        <div class="header-info">
          <h1>📋 Danh mục dịch vụ</h1>
          <p>Quản lý dịch vụ cho chỉ định khám và tính phí</p>
        </div>
        <button class="btn-primary" (click)="openModal()">➕ Thêm dịch vụ</button>
      </header>

      <div class="filters-bar">
        <input type="text" class="search-input" [(ngModel)]="searchQuery" (input)="onSearch()"
               placeholder="🔍 Tìm theo mã hoặc tên dịch vụ..." />
        <select class="filter-select" [(ngModel)]="categoryFilter" (change)="loadServices()">
          <option value="">Tất cả danh mục</option>
          @for (cat of categories(); track cat.name) {
            <option [value]="cat.name">{{ getCategoryLabel(cat.name) }}</option>
          }
        </select>
      </div>

      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>Mã</th>
              <th>Tên dịch vụ</th>
              <th>Danh mục</th>
              <th class="text-right">Đơn giá</th>
              <th>Đơn vị</th>
              <th>Trạng thái</th>
              <th class="text-center">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (svc of services(); track svc.id) {
              <tr [class.inactive]="!svc.isActive">
                <td><span class="code-badge">{{ svc.code }}</span></td>
                <td class="font-medium">{{ svc.name }}</td>
                <td><span class="category-badge" [attr.data-cat]="svc.category">{{ getCategoryLabel(svc.category) }}</span></td>
                <td class="text-right font-medium">{{ formatPrice(svc.unitPrice) }}</td>
                <td>{{ svc.unit }}</td>
                <td>
                  <span class="status-badge" [class.active]="svc.isActive">
                    {{ svc.isActive ? 'Hoạt động' : 'Ngừng' }}
                  </span>
                </td>
                <td class="text-center">
                  <div class="action-buttons">
                    <button class="btn-icon" title="Sửa" (click)="openModal(svc)">✏️</button>
                    <button class="btn-icon" [title]="svc.isActive ? 'Ngừng' : 'Kích hoạt'" (click)="toggleService(svc)">
                      {{ svc.isActive ? '🔒' : '🔓' }}
                    </button>
                  </div>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="empty-state">{{ loading() ? 'Đang tải...' : 'Không có dữ liệu' }}</td>
              </tr>
            }
          </tbody>
        </table>

        <div class="pagination">
          <span>Tổng: {{ total() }} dịch vụ</span>
          <div class="page-controls">
            <button class="btn-sm" [disabled]="page === 1" (click)="changePage(-1)">← Trước</button>
            <span class="page-info">Trang {{ page }}</span>
            <button class="btn-sm" [disabled]="services().length < pageSize" (click)="changePage(1)">Sau →</button>
          </div>
        </div>
      </div>

      <!-- Add/Edit Modal -->
      @if (showModal) {
        <div class="modal-backdrop" (click)="closeModal()">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h2>{{ editingService ? '✏️ Sửa dịch vụ' : '➕ Thêm dịch vụ' }}</h2>
              <button class="close-btn" (click)="closeModal()">×</button>
            </div>
            <form (ngSubmit)="saveService()">
              <div class="modal-body">
                <div class="form-grid">
                  <div class="form-group">
                    <label>Mã dịch vụ *</label>
                    <input class="form-control" [(ngModel)]="formData.code" name="code" [disabled]="!!editingService"
                           placeholder="VD: XN001" required />
                  </div>
                  <div class="form-group">
                    <label>Danh mục *</label>
                    <select class="form-control" [(ngModel)]="formData.category" name="category" required>
                      @for (cat of categories(); track cat.name) {
                        <option [value]="cat.name">{{ getCategoryLabel(cat.name) }}</option>
                      }
                    </select>
                  </div>
                  <div class="form-group full-width">
                    <label>Tên dịch vụ *</label>
                    <input class="form-control" [(ngModel)]="formData.name" name="name"
                           placeholder="Nhập tên dịch vụ" required />
                  </div>
                  <div class="form-group">
                    <label>Đơn giá (VNĐ) *</label>
                    <input class="form-control" type="number" [(ngModel)]="formData.unitPrice" name="unitPrice"
                           placeholder="0" required min="0" />
                  </div>
                  <div class="form-group">
                    <label>Đơn vị tính</label>
                    <input class="form-control" [(ngModel)]="formData.unit" name="unit" placeholder="lần" />
                  </div>
                  <div class="form-group full-width">
                    <label>Mô tả</label>
                    <input class="form-control" [(ngModel)]="formData.description" name="description"
                           placeholder="Mô tả chi tiết (tuỳ chọn)" />
                  </div>
                </div>
              </div>
              <div class="modal-footer">
                <button type="button" class="btn-ghost" (click)="closeModal()">Huỷ</button>
                <button type="submit" class="btn-primary" [disabled]="loading()">
                  {{ loading() ? 'Đang lưu...' : 'Lưu' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
    styles: [`
    .page-container { padding: 24px; background: #f8fafc; min-height: 100vh; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .header-info h1 { margin: 0; font-size: 1.5rem; color: #1e293b; }
    .header-info p { margin: 4px 0 0; color: #64748b; font-size: 0.9rem; }
    .btn-primary { background: linear-gradient(135deg, #6366f1, #8b5cf6); color: white; border: none; padding: 10px 20px; border-radius: 8px; cursor: pointer; font-weight: 500; }
    .btn-primary:hover { opacity: 0.9; }
    
    .filters-bar { display: flex; gap: 16px; margin-bottom: 16px; }
    .search-input { flex: 1; padding: 10px 16px; border: 1px solid #e2e8f0; border-radius: 8px; font-size: 0.95rem; }
    .filter-select { padding: 10px 16px; border: 1px solid #e2e8f0; border-radius: 8px; min-width: 180px; }
    
    .card { background: white; border-radius: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden; }
    .data-table { width: 100%; border-collapse: collapse; }
    .data-table th, .data-table td { padding: 12px 16px; text-align: left; border-bottom: 1px solid #f1f5f9; }
    .data-table th { background: #f8fafc; font-weight: 600; color: #64748b; font-size: 0.8rem; text-transform: uppercase; }
    .data-table tr:hover { background: #f8fafc; }
    .data-table tr.inactive { opacity: 0.5; }
    
    .code-badge { background: #e0e7ff; color: #4338ca; padding: 4px 8px; border-radius: 4px; font-size: 0.85rem; font-family: monospace; }
    .category-badge { padding: 4px 10px; border-radius: 12px; font-size: 0.8rem; background: #f1f5f9; color: #475569; }
    .category-badge[data-cat="LabTest"] { background: #fef3c7; color: #92400e; }
    .category-badge[data-cat="Ultrasound"] { background: #dbeafe; color: #1e40af; }
    .category-badge[data-cat="Procedure"] { background: #fce7f3; color: #9d174d; }
    .category-badge[data-cat="Medication"] { background: #dcfce7; color: #166534; }
    .category-badge[data-cat="IVF"] { background: #f3e8ff; color: #7e22ce; }
    
    .status-badge { padding: 4px 8px; border-radius: 12px; font-size: 0.75rem; background: #fee2e2; color: #991b1b; }
    .status-badge.active { background: #dcfce7; color: #166534; }
    
    .font-medium { font-weight: 500; }
    .text-right { text-align: right; }
    .text-center { text-align: center; }
    
    .action-buttons { display: flex; gap: 8px; justify-content: center; }
    .btn-icon { width: 32px; height: 32px; border-radius: 8px; border: none; background: transparent; cursor: pointer; font-size: 1rem; }
    .btn-icon:hover { background: #f1f5f9; }
    
    .empty-state { text-align: center; padding: 48px; color: #94a3b8; }
    
    .pagination { display: flex; justify-content: space-between; align-items: center; padding: 16px; border-top: 1px solid #f1f5f9; }
    .page-controls { display: flex; gap: 12px; align-items: center; }
    .btn-sm { padding: 6px 12px; border: 1px solid #e2e8f0; background: white; border-radius: 6px; cursor: pointer; }
    .btn-sm:disabled { opacity: 0.5; cursor: not-allowed; }
    .page-info { font-weight: 500; }
    
    /* Modal */
    .modal-backdrop { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.6); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal-card { background: white; border-radius: 16px; width: 100%; max-width: 550px; box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25); animation: slideUp 0.3s; }
    .modal-header { padding: 20px 24px; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; }
    .modal-header h2 { margin: 0; font-size: 1.2rem; }
    .close-btn { background: none; border: none; font-size: 1.5rem; cursor: pointer; color: #94a3b8; }
    .modal-body { padding: 24px; }
    .modal-footer { padding: 16px 24px; background: #f8fafc; border-top: 1px solid #e2e8f0; display: flex; justify-content: flex-end; gap: 12px; }
    
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-group.full-width { grid-column: span 2; }
    .form-group label { font-size: 0.85rem; font-weight: 500; color: #374151; }
    .form-control { padding: 10px 12px; border: 1px solid #e2e8f0; border-radius: 8px; font-size: 0.95rem; }
    .form-control:focus { border-color: #6366f1; outline: none; box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1); }
    .form-control:disabled { background: #f1f5f9; }
    
    .btn-ghost { background: transparent; border: 1px solid #e2e8f0; padding: 10px 20px; border-radius: 8px; cursor: pointer; }
    
    @keyframes slideUp { from { transform: translateY(20px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }
  `]
})
export class ServiceCatalogComponent implements OnInit {
    services = signal<any[]>([]);
    categories = signal<{ name: string; value: number }[]>([]);
    total = signal(0);
    loading = signal(false);

    searchQuery = '';
    categoryFilter = '';
    page = 1;
    pageSize = 20;

    showModal = false;
    editingService: any = null;
    formData: any = { code: '', name: '', category: 'LabTest', unitPrice: 0, unit: 'lần', description: '' };

    private searchTimeout?: ReturnType<typeof setTimeout>;

    categoryLabels: Record<string, string> = {
        'LabTest': '🧪 Xét nghiệm',
        'Ultrasound': '📷 Siêu âm',
        'Procedure': '💉 Thủ thuật',
        'Medication': '💊 Thuốc',
        'Consultation': '💬 Tư vấn',
        'IVF': '🧬 IVF/ICSI',
        'Andrology': '🔬 Nam khoa',
        'SpermBank': '🏦 Ngân hàng tinh trùng',
        'Other': '📦 Khác'
    };

    constructor(private api: ApiService) { }

    ngOnInit() {
        this.loadCategories();
        this.loadServices();
    }

    loadCategories() {
        this.api.getServiceCategories().subscribe({
            next: (cats) => this.categories.set(cats),
            error: () => { }
        });
    }

    loadServices() {
        this.loading.set(true);
        this.api.getServices(this.searchQuery || undefined, this.categoryFilter || undefined, this.page, this.pageSize).subscribe({
            next: (res) => {
                this.services.set(res.items);
                this.total.set(res.total);
                this.loading.set(false);
            },
            error: () => this.loading.set(false)
        });
    }

    onSearch() {
        clearTimeout(this.searchTimeout);
        this.searchTimeout = setTimeout(() => {
            this.page = 1;
            this.loadServices();
        }, 300);
    }

    changePage(delta: number) {
        this.page += delta;
        this.loadServices();
    }

    getCategoryLabel(cat: string): string {
        return this.categoryLabels[cat] || cat;
    }

    formatPrice(price: number): string {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(price);
    }

    openModal(service?: any) {
        this.editingService = service || null;
        if (service) {
            this.formData = { ...service };
        } else {
            this.formData = { code: '', name: '', category: 'LabTest', unitPrice: 0, unit: 'lần', description: '' };
        }
        this.showModal = true;
    }

    closeModal() {
        this.showModal = false;
        this.editingService = null;
    }

    saveService() {
        this.loading.set(true);
        if (this.editingService) {
            this.api.updateService(this.editingService.id, this.formData).subscribe({
                next: () => {
                    this.closeModal();
                    this.loadServices();
                    this.loading.set(false);
                },
                error: (err) => {
                    alert('Lỗi: ' + (err.error?.message || 'Không thể cập nhật'));
                    this.loading.set(false);
                }
            });
        } else {
            this.api.createService(this.formData).subscribe({
                next: () => {
                    this.closeModal();
                    this.loadServices();
                    this.loading.set(false);
                },
                error: (err) => {
                    alert('Lỗi: ' + (err.error?.message || 'Không thể tạo dịch vụ'));
                    this.loading.set(false);
                }
            });
        }
    }

    toggleService(service: any) {
        this.api.toggleService(service.id).subscribe({
            next: (res) => {
                service.isActive = res.isActive;
            },
            error: () => alert('Lỗi: Không thể thay đổi trạng thái')
        });
    }
}
