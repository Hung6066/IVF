import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { InventoryService } from '../../../core/services/inventory.service';
import { InventoryItemDto } from '../../../core/models/inventory.models';

@Component({
  selector: 'app-inventory-alerts',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './inventory-alerts.component.html',
  styleUrls: ['./inventory-alerts.component.scss'],
})
export class InventoryAlertsComponent implements OnInit {
  private inventoryService = inject(InventoryService);

  allAlerts = signal<InventoryItemDto[]>([]);
  loading = signal(false);
  filter = signal<'all' | 'low_stock' | 'expiring'>('all');

  filteredAlerts = computed(() => {
    const f = this.filter();
    const items = this.allAlerts();
    if (f === 'low_stock') return items.filter((a) => a.isLowStock);
    if (f === 'expiring') return items.filter((a) => a.isNearExpiry || a.isExpired);
    return items;
  });

  ngOnInit() {
    this.loadAlerts();
  }

  loadAlerts() {
    this.loading.set(true);
    this.inventoryService.getLowStockAlerts().subscribe({
      next: (data) => {
        this.allAlerts.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  setFilter(f: 'all' | 'low_stock' | 'expiring') {
    this.filter.set(f);
  }

  alertBadgeClass(item: InventoryItemDto): string {
    if (item.isExpired) return 'bg-red-100 text-red-800';
    if (item.isNearExpiry) return 'bg-orange-100 text-orange-800';
    if (item.isLowStock) return 'bg-yellow-100 text-yellow-800';
    return 'bg-gray-100 text-gray-800';
  }

  alertBadgeLabel(item: InventoryItemDto): string {
    if (item.isExpired) return 'Đã hết hạn';
    if (item.isNearExpiry) return 'Sắp hết hạn';
    if (item.isLowStock) return 'Tồn kho thấp';
    return '';
  }
}
