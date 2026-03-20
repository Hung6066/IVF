import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { InventoryService } from '../../../core/services/inventory.service';
import { InventoryItemDto } from '../../../core/models/inventory.models';

@Component({
  selector: 'app-inventory-stock',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './inventory-stock.component.html',
  styleUrls: ['./inventory-stock.component.scss']
})
export class InventoryStockComponent implements OnInit {
  private service = inject(InventoryService);
  private router = inject(Router);

  items = signal<InventoryItemDto[]>([]);
  total = signal(0);
  loading = signal(false);
  searchQuery = '';
  filterCategory = '';
  lowStockOnly = false;
  page = 1;
  pageSize = 20;

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.service.search(this.searchQuery || undefined, this.filterCategory || undefined, this.lowStockOnly, this.page, this.pageSize).subscribe({
      next: (res) => { this.items.set(res.items); this.total.set(res.total); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  goImport() { this.router.navigate(['/inventory/import']); }
  goUsage() { this.router.navigate(['/inventory/usage']); }
  goAlerts() { this.router.navigate(['/inventory/alerts']); }
  goRequests() { this.router.navigate(['/inventory/requests']); }

  stockClass(item: InventoryItemDto): string {
    if (item.isExpired) return 'text-red-600 font-medium';
    if (item.isLowStock) return 'text-orange-600 font-medium';
    return 'text-green-600';
  }

  formatDate(d?: string): string { return d ? new Date(d).toLocaleDateString('vi-VN') : '—'; }
}
