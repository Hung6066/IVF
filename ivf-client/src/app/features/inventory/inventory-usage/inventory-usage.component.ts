import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { InventoryService } from '../../../core/services/inventory.service';
import { RecordUsageRequest } from '../../../core/models/inventory.models';

@Component({
  selector: 'app-inventory-usage',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './inventory-usage.component.html',
  styleUrls: ['./inventory-usage.component.scss']
})
export class InventoryUsageComponent {
  private service = inject(InventoryService);
  private router = inject(Router);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  form: RecordUsageRequest = { itemId: '', quantity: 1, reference: '', reason: '', performedByName: '' };

  save() {
    this.saving.set(true);
    this.service.recordUsage(this.form).subscribe({
      next: () => { this.successMsg.set('Đã ghi nhận xuất kho'); this.saving.set(false); setTimeout(() => this.router.navigate(['/inventory/stock']), 1500); },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi'); this.saving.set(false); }
    });
  }
  back() { this.router.navigate(['/inventory/stock']); }
}
