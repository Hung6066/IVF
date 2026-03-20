import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DrugCatalogService,
  CreateDrugRequest,
  UpdateDrugRequest,
} from '../../../core/services/drug-catalog.service';
import { DrugCatalogDto, DrugCategory } from '../../../core/models/clinical-management.models';

@Component({
  selector: 'app-drug-catalog-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './drug-catalog-list.component.html',
  styleUrls: ['./drug-catalog-list.component.scss'],
})
export class DrugCatalogListComponent implements OnInit {
  private service = inject(DrugCatalogService);

  loading = signal(false);
  saving = signal(false);
  error = signal('');
  items = signal<DrugCatalogDto[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = 20;

  searchQuery = '';
  filterCategory = '';
  showForm = signal(false);
  editingItem = signal<DrugCatalogDto | null>(null);

  categories: DrugCategory[] = [
    'Gonadotropin',
    'GnRH',
    'Progesterone',
    'Estrogen',
    'Trigger',
    'Antibiotic',
    'Supplement',
    'Other',
  ];
  categoryLabels: Record<DrugCategory, string> = {
    Gonadotropin: 'Gonadotropin',
    GnRH: 'GnRH',
    Progesterone: 'Progesterone',
    Estrogen: 'Estrogen',
    Trigger: 'Kích trứng rụng',
    Antibiotic: 'Kháng sinh',
    Supplement: 'Bổ sung',
    Other: 'Khác',
  };

  form: CreateDrugRequest = {
    code: '',
    name: '',
    genericName: '',
    category: 'Other',
    unit: '',
    activeIngredient: '',
    defaultDosage: '',
    notes: '',
  };

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service
      .search(
        this.searchQuery || undefined,
        (this.filterCategory as DrugCategory) || undefined,
        undefined,
        this.page(),
        this.pageSize,
      )
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.totalCount);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  search() {
    this.page.set(1);
    this.load();
  }

  openCreate() {
    this.editingItem.set(null);
    this.form = {
      code: '',
      name: '',
      genericName: '',
      category: 'Other',
      unit: '',
      activeIngredient: '',
      defaultDosage: '',
      notes: '',
    };
    this.showForm.set(true);
  }

  openEdit(item: DrugCatalogDto) {
    this.editingItem.set(item);
    this.form = {
      code: item.code,
      name: item.name,
      genericName: item.genericName,
      category: item.category,
      unit: item.unit,
      activeIngredient: item.activeIngredient ?? '',
      defaultDosage: item.defaultDosage ?? '',
      notes: item.notes ?? '',
    };
    this.showForm.set(true);
  }

  save() {
    this.saving.set(true);
    this.error.set('');
    const editing = this.editingItem();
    const req = editing
      ? this.service.update(editing.id, {
          name: this.form.name,
          genericName: this.form.genericName,
          category: this.form.category,
          unit: this.form.unit,
          activeIngredient: this.form.activeIngredient,
          defaultDosage: this.form.defaultDosage,
          notes: this.form.notes,
        })
      : this.service.create(this.form);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi lưu thuốc');
        this.saving.set(false);
      },
    });
  }

  toggleActive(item: DrugCatalogDto) {
    this.service.toggleActive(item.id, !item.isActive).subscribe({ next: () => this.load() });
  }

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize);
  }
  prevPage() {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.load();
    }
  }
  nextPage() {
    if (this.page() < this.totalPages) {
      this.page.update((p) => p + 1);
      this.load();
    }
  }
}
