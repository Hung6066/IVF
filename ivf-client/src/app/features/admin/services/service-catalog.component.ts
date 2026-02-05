import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../../core/services/catalog.service';

@Component({
  selector: 'app-service-catalog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './service-catalog.component.html',
  styleUrls: ['./service-catalog.component.scss']
})
export class ServiceCatalogComponent implements OnInit {
  services = signal<any[]>([]);
  categories = signal<{ name: string; value: number }[]>([]);
  total = signal(0);
  loading = signal(false);

  search = '';
  categoryFilter = '';
  page = 1;
  pageSize = 20;

  showModal = false;
  editingService: any = null;
  formData: any = { code: '', name: '', category: 'KhamBenh', unitPrice: 0, unit: 'lần', description: '', isActive: true };

  private searchTimeout?: ReturnType<typeof setTimeout>;

  categoryLabels: Record<string, string> = {
    'KhamBenh': 'Khám bệnh',
    'XetNghiem': 'Xét nghiệm',
    'SieuAm': 'Siêu âm',
    'ThuThuat': 'Thủ thuật',
    'Thuoc': 'Thuốc',
    'VatTu': 'Vật tư'
  };

  constructor(private catalogService: CatalogService) { }

  ngOnInit() {
    this.loadCategories();
    this.loadServices();
  }

  loadCategories() {
    this.catalogService.getServiceCategories().subscribe({
      next: (cats) => this.categories.set(cats),
      error: () => { }
    });
  }

  loadServices() {
    this.loading.set(true);
    this.catalogService.getServices(this.search || undefined, this.categoryFilter || undefined, this.page, this.pageSize).subscribe({
      next: (res) => {
        this.services.set(res.items);
        this.total.set(res.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch() {
    // Keep for usage if needed, but template uses loadServices directly on enter
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
      this.formData = { code: '', name: '', category: 'KhamBenh', unitPrice: 0, unit: 'lần', description: '', isActive: true };
    }
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
    this.editingService = null;
  }

  saveService() {
    this.loading.set(true);
    // Ensure payload matches API expectations
    const payload = {
      ...this.formData,
      unitPrice: this.formData.unitPrice // Ensure this matches backend
    };

    if (this.editingService) {
      this.catalogService.updateService(this.editingService.id, payload).subscribe({
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
      this.catalogService.createService(payload).subscribe({
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

  toggleStatus(service: any) {
    this.catalogService.toggleService(service.id).subscribe({
      next: (res) => {
        service.isActive = res.isActive;
      },
      error: () => alert('Lỗi: Không thể thay đổi trạng thái')
    });
  }
}
