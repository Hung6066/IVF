import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { FinanceService } from '../../../core/services/finance.service';
import { Invoice } from '../../../core/models/finance.models';

@Component({
  selector: 'app-billing-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './billing-history.component.html',
  styleUrls: ['./billing-history.component.scss'],
})
export class BillingHistoryComponent implements OnInit {
  private financeService = inject(FinanceService);

  loading = signal(true);
  invoices = signal<Invoice[]>([]);
  total = signal(0);
  page = 1;
  pageSize = 20;
  query = '';

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.financeService
      .searchInvoices(this.query || undefined, this.page, this.pageSize)
      .subscribe({
        next: (res) => {
          this.invoices.set(res.items);
          this.total.set(res.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onSearch() {
    this.page = 1;
    this.load();
  }

  nextPage() {
    this.page++;
    this.load();
  }
  prevPage() {
    if (this.page > 1) {
      this.page--;
      this.load();
    }
  }

  get totalPages() {
    return Math.ceil(this.total() / this.pageSize);
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Draft: 'Nháp',
      Issued: 'Phát hành',
      PartiallyPaid: 'Một phần',
      Paid: 'Đã TT',
      Refunded: 'Hoàn',
      Cancelled: 'Hủy',
    };
    return map[status] ?? status;
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Paid':
        return 'bg-green-100 text-green-700';
      case 'PartiallyPaid':
        return 'bg-yellow-100 text-yellow-700';
      case 'Issued':
        return 'bg-blue-100 text-blue-700';
      case 'Cancelled':
        return 'bg-red-100 text-red-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  }
}
