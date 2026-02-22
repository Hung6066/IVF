import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MenuService, MenuItemAdmin } from '../../../core/services/menu.service';
import { PermissionDefinitionService } from '../../../core/services/permission-definition.service';

@Component({
  selector: 'app-menu-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './menu-config.component.html',
  styleUrls: ['./menu-config.component.scss'],
})
export class MenuConfigComponent implements OnInit {
  items = signal<MenuItemAdmin[]>([]);
  loading = signal(false);
  saving = signal(false);

  // Modal state
  showModal = false;
  editingItem: MenuItemAdmin | null = null;
  formData: any = this.getEmptyForm();

  // Loaded dynamically from the API
  availablePermissions: string[] = [];

  constructor(
    private menuService: MenuService,
    private permDefService: PermissionDefinitionService,
  ) {}

  ngOnInit() {
    this.loadItems();
    this.loadPermissions();
  }

  loadPermissions() {
    this.permDefService.loadPermissionGroups().subscribe({
      next: (groups) => {
        this.availablePermissions = groups.flatMap((g) => g.permissions.map((p) => p.code));
      },
    });
  }

  loadItems() {
    this.loading.set(true);
    this.menuService.getAllItems().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  getEmptyForm() {
    return {
      section: '',
      sectionHeader: '',
      icon: '📄',
      label: '',
      route: '',
      permission: '',
      adminOnly: false,
      sortOrder: 100,
      isActive: true,
    };
  }

  // ── Grouped display ────────────────────────────────
  get groupedItems(): { section: string; items: MenuItemAdmin[] }[] {
    const groups = new Map<string, MenuItemAdmin[]>();
    for (const item of this.items()) {
      const key = item.section ?? '(Menu chính)';
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(item);
    }
    // Sort within groups by sortOrder
    const result: { section: string; items: MenuItemAdmin[] }[] = [];
    for (const [section, items] of groups) {
      result.push({ section, items: items.sort((a, b) => a.sortOrder - b.sortOrder) });
    }
    return result;
  }

  // ── Modal ──────────────────────────────────────────
  openModal(item?: MenuItemAdmin) {
    if (item) {
      this.editingItem = item;
      this.formData = {
        section: item.section ?? '',
        sectionHeader: item.sectionHeader ?? '',
        icon: item.icon,
        label: item.label,
        route: item.route,
        permission: item.permission ?? '',
        adminOnly: item.adminOnly,
        sortOrder: item.sortOrder,
        isActive: item.isActive,
      };
    } else {
      this.editingItem = null;
      this.formData = this.getEmptyForm();
    }
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
    this.editingItem = null;
  }

  saveItem() {
    this.saving.set(true);
    const data = {
      section: this.formData.section || null,
      sectionHeader: this.formData.sectionHeader || null,
      icon: this.formData.icon,
      label: this.formData.label,
      route: this.formData.route,
      permission: this.formData.permission || null,
      adminOnly: this.formData.adminOnly,
      sortOrder: this.formData.sortOrder,
      isActive: this.formData.isActive,
    };

    if (this.editingItem) {
      this.menuService.updateItem(this.editingItem.id, data as any).subscribe({
        next: () => {
          this.saving.set(false);
          this.closeModal();
          this.loadItems();
        },
        error: () => this.saving.set(false),
      });
    } else {
      this.menuService.createItem(data as any).subscribe({
        next: () => {
          this.saving.set(false);
          this.closeModal();
          this.loadItems();
        },
        error: () => this.saving.set(false),
      });
    }
  }

  // ── Actions ────────────────────────────────────────
  toggleActive(item: MenuItemAdmin) {
    this.menuService.toggleItem(item.id).subscribe({
      next: (res) => {
        item.isActive = res.isActive;
        this.items.update((list) => [...list]);
      },
    });
  }

  deleteItem(item: MenuItemAdmin) {
    if (!confirm(`Xoá menu "${item.label}"?`)) return;
    this.menuService.deleteItem(item.id).subscribe({
      next: () => this.loadItems(),
    });
  }

  moveUp(item: MenuItemAdmin, groupItems: MenuItemAdmin[]) {
    const idx = groupItems.indexOf(item);
    if (idx <= 0) return;
    this.swapOrder(groupItems[idx - 1], item);
  }

  moveDown(item: MenuItemAdmin, groupItems: MenuItemAdmin[]) {
    const idx = groupItems.indexOf(item);
    if (idx >= groupItems.length - 1) return;
    this.swapOrder(item, groupItems[idx + 1]);
  }

  private swapOrder(a: MenuItemAdmin, b: MenuItemAdmin) {
    const tmpOrder = a.sortOrder;
    a.sortOrder = b.sortOrder;
    b.sortOrder = tmpOrder;

    this.menuService
      .reorder([
        { id: a.id, sortOrder: a.sortOrder },
        { id: b.id, sortOrder: b.sortOrder },
      ])
      .subscribe({
        next: () => this.loadItems(),
      });
  }

  formatPermission(perm: string): string {
    return perm.replace(/([A-Z])/g, ' $1').trim();
  }
}
