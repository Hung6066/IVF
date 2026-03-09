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

  // Quick Group Modal
  showQuickGroupModal = false;
  quickGroupData = this.getEmptyQuickGroup();

  // Inline quick-add per group
  inlineAdd: Record<string, string> = {};

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

  quickAddToGroup(group: {
    groupCode: string;
    groupName: string;
    groupIcon: string;
    items: PermissionDefinitionAdmin[];
  }) {
    const code = this.inlineAdd[group.groupCode + '_code']?.trim();
    const name = this.inlineAdd[group.groupCode + '_name']?.trim();
    if (!code || !name) return;

    const maxSort = group.items.reduce((max, i) => Math.max(max, i.sortOrder), 0);
    const groupSortOrder = group.items[0]?.groupSortOrder ?? 1;

    this.saving.set(true);
    this.permDefService
      .create({
        code,
        displayName: name,
        groupCode: group.groupCode,
        groupDisplayName: group.groupName,
        groupIcon: group.groupIcon,
        sortOrder: maxSort + 1,
        groupSortOrder,
      })
      .subscribe({
        next: () => {
          this.inlineAdd[group.groupCode + '_code'] = '';
          this.inlineAdd[group.groupCode + '_name'] = '';
          this.saving.set(false);
          this.loadItems();
        },
        error: (err) => {
          this.saving.set(false);
          if (err.status === 409) alert('Mã quyền đã tồn tại!');
        },
      });
  }

  // ─── Quick Group ───

  openQuickGroup() {
    this.quickGroupData = this.getEmptyQuickGroup();
    this.showQuickGroupModal = true;
  }

  closeQuickGroup() {
    this.showQuickGroupModal = false;
  }

  addQuickPermission() {
    this.quickGroupData.permissions.push({ code: '', displayName: '' });
  }

  removeQuickPermission(index: number) {
    this.quickGroupData.permissions.splice(index, 1);
  }

  autoGenerateGroupCode() {
    const name = this.quickGroupData.groupDisplayName;
    if (!name) return;
    // Simple Vietnamese-to-code: remove diacritics, lowercase, replace spaces with _
    this.quickGroupData.groupCode = name
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/gi, 'd')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '_')
      .replace(/^_|_$/g, '');
  }

  applyTemplate(type: 'crud' | 'full' | 'report') {
    const prefix = this.quickGroupData.groupCode
      ? this.quickGroupData.groupCode.charAt(0).toUpperCase() +
        this.quickGroupData.groupCode
          .slice(1)
          .replace(/_([a-z])/g, (_, c: string) => c.toUpperCase())
      : 'Item';
    const groupName = this.quickGroupData.groupDisplayName || 'mục';

    const templates: Record<string, { code: string; displayName: string }[]> = {
      crud: [
        { code: `View${prefix}`, displayName: `Xem ${groupName}` },
        { code: `Manage${prefix}`, displayName: `Quản lý ${groupName}` },
      ],
      full: [
        { code: `View${prefix}`, displayName: `Xem ${groupName}` },
        { code: `Create${prefix}`, displayName: `Tạo ${groupName}` },
        { code: `Edit${prefix}`, displayName: `Sửa ${groupName}` },
        { code: `Delete${prefix}`, displayName: `Xoá ${groupName}` },
      ],
      report: [
        { code: `View${prefix}`, displayName: `Xem ${groupName}` },
        { code: `Export${prefix}`, displayName: `Xuất ${groupName}` },
      ],
    };

    this.quickGroupData.permissions = templates[type].map((t, i) => ({
      code: t.code,
      displayName: t.displayName,
    }));
  }

  isQuickGroupValid(): boolean {
    if (!this.quickGroupData.groupCode || !this.quickGroupData.groupDisplayName) return false;
    return this.quickGroupData.permissions.some((p) => p.code && p.displayName);
  }

  saveQuickGroup() {
    if (!this.isQuickGroupValid()) return;
    this.saving.set(true);

    const validPerms = this.quickGroupData.permissions.filter((p) => p.code && p.displayName);
    let completed = 0;
    let errors = 0;

    for (let i = 0; i < validPerms.length; i++) {
      const perm = validPerms[i];
      this.permDefService
        .create({
          code: perm.code,
          displayName: perm.displayName,
          groupCode: this.quickGroupData.groupCode,
          groupDisplayName: this.quickGroupData.groupDisplayName,
          groupIcon: this.quickGroupData.groupIcon,
          sortOrder: i + 1,
          groupSortOrder: this.quickGroupData.groupSortOrder,
        })
        .subscribe({
          next: () => {
            completed++;
            if (completed + errors === validPerms.length) {
              this.saving.set(false);
              if (errors === 0) {
                this.showQuickGroupModal = false;
                this.loadItems();
              } else {
                alert(`Tạo xong ${completed}/${validPerms.length} quyền (${errors} lỗi`);
                this.loadItems();
              }
            }
          },
          error: () => {
            errors++;
            if (completed + errors === validPerms.length) {
              this.saving.set(false);
              alert(`Tạo xong ${completed}/${validPerms.length} quyền (${errors} lỗi)`);
              this.loadItems();
            }
          },
        });
    }
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

  private getEmptyQuickGroup() {
    const maxGroupSort = this.items().reduce((max, i) => Math.max(max, i.groupSortOrder), 0);
    return {
      groupCode: '',
      groupDisplayName: '',
      groupIcon: '📋',
      groupSortOrder: maxGroupSort + 1,
      permissions: [{ code: '', displayName: '' }] as { code: string; displayName: string }[],
    };
  }
}
