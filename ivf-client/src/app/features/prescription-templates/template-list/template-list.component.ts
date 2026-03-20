import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  PrescriptionTemplateService,
  CreatePrescriptionTemplateRequest,
  TemplateItemInput,
} from '../../../core/services/prescription-template.service';
import {
  PrescriptionTemplateDto,
  PrescriptionCycleType,
} from '../../../core/models/clinical-management.models';

@Component({
  selector: 'app-template-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './template-list.component.html',
  styleUrls: ['./template-list.component.scss'],
})
export class TemplateListComponent implements OnInit {
  private service = inject(PrescriptionTemplateService);

  loading = signal(false);
  saving = signal(false);
  error = signal('');
  items = signal<PrescriptionTemplateDto[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = 20;

  searchQuery = '';
  filterCycleType = '';
  showForm = signal(false);
  editingItem = signal<PrescriptionTemplateDto | null>(null);
  expandedId = signal<string | null>(null);

  cycleTypes: PrescriptionCycleType[] = ['IVF', 'IUI', 'FET', 'General'];
  cycleTypeLabels: Record<PrescriptionCycleType, string> = {
    IVF: 'IVF/ICSI',
    IUI: 'IUI',
    FET: 'FET',
    General: 'Tổng quát',
  };

  form: CreatePrescriptionTemplateRequest = {
    name: '',
    cycleType: 'IVF',
    createdByDoctorId: '',
    description: '',
    items: [],
  };

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service
      .search(
        this.searchQuery || undefined,
        (this.filterCycleType as PrescriptionCycleType) || undefined,
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
      name: '',
      cycleType: 'IVF',
      createdByDoctorId: '',
      description: '',
      items: [this.newItem()],
    };
    this.showForm.set(true);
  }

  openEdit(tpl: PrescriptionTemplateDto) {
    this.editingItem.set(tpl);
    this.form = {
      name: tpl.name,
      cycleType: tpl.cycleType,
      createdByDoctorId: tpl.createdByDoctorId,
      description: tpl.description ?? '',
      items: tpl.items.map((i) => ({
        drugName: i.drugName,
        drugCode: i.drugCode ?? '',
        dosage: i.dosage,
        frequency: i.frequency,
        duration: i.duration,
        sortOrder: i.sortOrder,
      })),
    };
    this.showForm.set(true);
  }

  newItem(): TemplateItemInput {
    return {
      drugName: '',
      drugCode: '',
      dosage: '',
      frequency: '',
      duration: '',
      sortOrder: this.form.items.length + 1,
    };
  }
  addItem() {
    this.form.items.push(this.newItem());
  }
  removeItem(i: number) {
    this.form.items.splice(i, 1);
  }

  save() {
    this.saving.set(true);
    this.error.set('');
    const editing = this.editingItem();
    const req = editing
      ? this.service.update(editing.id, {
          name: this.form.name,
          cycleType: this.form.cycleType,
          description: this.form.description,
          items: this.form.items,
        })
      : this.service.create(this.form);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi lưu mẫu toa');
        this.saving.set(false);
      },
    });
  }

  toggleActive(tpl: PrescriptionTemplateDto) {
    this.service.toggleActive(tpl.id).subscribe({ next: () => this.load() });
  }

  toggleExpand(id: string) {
    this.expandedId.set(this.expandedId() === id ? null : id);
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
