import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { InventoryService } from '../../../core/services/inventory.service';
import { InventoryItemDto } from '../../../core/models/inventory.models';

@Component({
  selector: 'app-inventory-report',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './inventory-report.component.html',
  styleUrls: ['./inventory-report.component.scss'],
})
export class InventoryReportComponent implements OnInit {
  private inventoryService = inject(InventoryService);

  loading = signal(true);
  items = signal<InventoryItemDto[]>([]);

  ngOnInit() {
    this.inventoryService.search(undefined, undefined, undefined, 1, 100).subscribe({
      next: (res) => {
        this.items.set(res.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  get lowStockCount() {
    return this.items().filter((i) => i.isLowStock).length;
  }
  get expiringCount() {
    return this.items().filter((i) => i.isNearExpiry).length;
  }
  get totalValue() {
    return this.items().reduce((s, i) => s + (i.unitPrice ?? 0) * i.currentStock, 0);
  }
}
