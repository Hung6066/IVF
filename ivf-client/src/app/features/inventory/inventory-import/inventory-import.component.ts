import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { InventoryService } from '../../../core/services/inventory.service';
import { ImportStockRequest } from '../../../core/models/inventory.models';

@Component({
  selector: 'app-inventory-import',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './inventory-import.component.html',
  styleUrls: ['./inventory-import.component.scss']
})
export class InventoryImportComponent {
  private service = inject(InventoryService);
  private router = inject(Router);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  form: ImportStockRequest = { itemId: '', quantity: 1, supplierName: '', unitCost: 0, batchNumber: '', reference: '', performedByName: '' };

  save() {
    this.saving.set(true);
    this.service.importStock(this.form).subscribe({
      next: () => { this.successMsg.set('Đã nhập kho thành công'); this.saving.set(false); setTimeout(() => this.router.navigate(['/inventory/stock']), 1500); },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi nhập kho'); this.saving.set(false); }
    });
  }
  back() { this.router.navigate(['/inventory/stock']); }
}
