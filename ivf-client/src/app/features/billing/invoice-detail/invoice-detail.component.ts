import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FinanceService } from '../../../core/services/finance.service';
import { Invoice } from '../../../core/models/finance.models';

@Component({
  selector: 'app-invoice-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './invoice-detail.component.html',
  styleUrls: ['./invoice-detail.component.scss'],
})
export class InvoiceDetailComponent implements OnInit {
  private financeService = inject(FinanceService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  loading = signal(true);
  invoice = signal<Invoice | null>(null);
  showPayment = signal(false);
  saving = signal(false);
  error = signal('');

  paymentForm = { amount: 0, paymentMethod: 'Tiền mặt', transactionReference: '' };
  paymentMethods = ['Tiền mặt', 'Chuyển khoản', 'Thẻ', 'Ví điện tử'];

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.financeService.getInvoice(id).subscribe({
        next: (inv) => {
          this.invoice.set(inv);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  openPayment() {
    const inv = this.invoice();
    if (inv) this.paymentForm.amount = inv.totalAmount - inv.paidAmount;
    this.showPayment.set(true);
  }

  submitPayment() {
    const inv = this.invoice();
    if (!inv) return;
    this.saving.set(true);
    this.financeService.recordPayment(inv.id, this.paymentForm).subscribe({
      next: () => {
        this.saving.set(false);
        this.showPayment.set(false);
        this.ngOnInit();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi thu tiền');
        this.saving.set(false);
      },
    });
  }

  issueInvoice() {
    const inv = this.invoice();
    if (!inv) return;
    this.financeService.issueInvoice(inv.id).subscribe({
      next: (updated) => this.invoice.set(updated),
    });
  }

  getIvfmdTotal(inv: Invoice): number {
    return (inv.items ?? [])
      .filter((i) => i.feeType !== 'Hospital')
      .reduce((s, i) => s + i.amount, 0);
  }

  getHospitalTotal(inv: Invoice): number {
    return (inv.items ?? [])
      .filter((i) => i.feeType === 'Hospital')
      .reduce((s, i) => s + i.amount, 0);
  }

  getStatusLabel(status: string): { text: string; css: string } {
    switch (status) {
      case 'Draft':
        return { text: 'Nháp', css: 'bg-gray-100 text-gray-700' };
      case 'Issued':
        return { text: 'Đã phát hành', css: 'bg-blue-100 text-blue-700' };
      case 'PartiallyPaid':
        return { text: 'Thanh toán một phần', css: 'bg-yellow-100 text-yellow-700' };
      case 'Paid':
        return { text: 'Đã thanh toán', css: 'bg-green-100 text-green-700' };
      case 'Refunded':
        return { text: 'Hoàn tiền', css: 'bg-purple-100 text-purple-700' };
      case 'Cancelled':
        return { text: 'Hủy', css: 'bg-red-100 text-red-700' };
      default:
        return { text: status, css: 'bg-gray-100 text-gray-700' };
    }
  }

  back() {
    this.router.navigate(['/billing']);
  }
}
