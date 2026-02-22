import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  PermissionDefinitionService,
  PermissionDefinitionAdmin,
} from '../../../core/services/permission-definition.service';

@Component({
  selector: 'app-permission-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './permission-config.component.html',
  styleUrls: ['./permission-config.component.scss'],
})
export class PermissionConfigComponent implements OnInit {
  items = signal<PermissionDefinitionAdmin[]>([]);
  loading = signal(false);
  saving = signal(false);

  // Modal
  showModal = false;
  editingItem: PermissionDefinitionAdmin | null = null;
  formData = this.getEmptyForm();

  // Filter
  groupFilter = '';

  constructor(private permDefService: PermissionDefinitionService) {}

  ngOnInit() {
    this.loadItems();
  }

  loadItems() {
    this.loading.set(true);
    this.permDefService.getAll().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  get groups(): string[] {
    const set = new Set(this.items().map((i) => i.groupCode));
    return Array.from(set);
  }

  get filteredItems(): PermissionDefinitionAdmin[] {
    if (!this.groupFilter) return this.items();
    return this.items().filter((i) => i.groupCode === this.groupFilter);
  }

  get groupedItems(): {
    groupCode: string;
    groupName: string;
    groupIcon: string;
    items: PermissionDefinitionAdmin[];
  }[] {
    const map = new Map<
      string,
      {
        groupCode: string;
        groupName: string;
        groupIcon: string;
        items: PermissionDefinitionAdmin[];
      }
    >();
    for (const item of this.filteredItems) {
      if (!map.has(item.groupCode)) {
        map.set(item.groupCode, {
          groupCode: item.groupCode,
          groupName: item.groupDisplayName,
          groupIcon: item.groupIcon,
          items: [],
        });
      }
      map.get(item.groupCode)!.items.push(item);
    }
    return Array.from(map.values()).sort((a, b) => {
      const aSort = a.items[0]?.groupSortOrder ?? 0;
      const bSort = b.items[0]?.groupSortOrder ?? 0;
      return aSort - bSort;
    });
  }

  // ─── Modal ───

  openAdd() {
    this.editingItem = null;
    this.formData = this.getEmptyForm();
    this.showModal = true;
  }

  openEdit(item: PermissionDefinitionAdmin) {
    this.editingItem = item;
    this.formData = {
      code: item.code,
      displayName: item.displayName,
      groupCode: item.groupCode,
      groupDisplayName: item.groupDisplayName,
      groupIcon: item.groupIcon,
      sortOrder: item.sortOrder,
      groupSortOrder: item.groupSortOrder,
    };
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
    this.editingItem = null;
  }

  save() {
    this.saving.set(true);

    if (this.editingItem) {
      this.permDefService
        .update(this.editingItem.id, {
          displayName: this.formData.displayName,
          groupCode: this.formData.groupCode,
          groupDisplayName: this.formData.groupDisplayName,
          groupIcon: this.formData.groupIcon,
          sortOrder: this.formData.sortOrder,
          groupSortOrder: this.formData.groupSortOrder,
        })
        .subscribe({
          next: () => {
            this.saving.set(false);
            this.closeModal();
            this.loadItems();
          },
          error: () => this.saving.set(false),
        });
    } else {
      this.permDefService
        .create({
          code: this.formData.code,
          displayName: this.formData.displayName,
          groupCode: this.formData.groupCode,
          groupDisplayName: this.formData.groupDisplayName,
          groupIcon: this.formData.groupIcon,
          sortOrder: this.formData.sortOrder,
          groupSortOrder: this.formData.groupSortOrder,
        })
        .subscribe({
          next: () => {
            this.saving.set(false);
            this.closeModal();
            this.loadItems();
          },
          error: (err) => {
            this.saving.set(false);
            if (err.status === 409) {
              alert('Mã quyền đã tồn tại!');
            }
          },
        });
    }
  }

  toggleActive(item: PermissionDefinitionAdmin) {
    this.permDefService.toggle(item.id).subscribe({
      next: (res) => {
        item.isActive = res.isActive;
        this.items.update((list) => [...list]);
      },
    });
  }

  deleteItem(item: PermissionDefinitionAdmin) {
    if (!confirm(`Xoá quyền "${item.displayName}" (${item.code})?`)) return;
    this.permDefService.delete(item.id).subscribe({
      next: () => this.loadItems(),
    });
  }

  private getEmptyForm() {
    return {
      code: '',
      displayName: '',
      groupCode: '',
      groupDisplayName: '',
      groupIcon: '',
      sortOrder: 1,
      groupSortOrder: 1,
    };
  }
}
