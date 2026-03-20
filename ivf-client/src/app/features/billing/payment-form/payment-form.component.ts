import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FinanceService } from '../../../core/services/finance.service';

@Component({
  selector: 'app-payment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './payment-form.component.html',
  styleUrls: ['./payment-form.component.scss'],
})
export class PaymentFormComponent {
  private route = inject(ActivatedRoute);
  private service = inject(FinanceService);
  private router = inject(Router);

  invoiceId = this.route.snapshot.paramMap.get('id') ?? '';
  saving = signal(false);
  error = signal('');
  success = signal(false);

  form = {
    amount: 0,
    paymentMethod: 'Cash',
    transactionReference: '',
  };

  methods = [
    { value: 'Cash', label: 'Tiền mặt' },
    { value: 'BankTransfer', label: 'Chuyển khoản' },
    { value: 'Card', label: 'Thẻ ngân hàng' },
    { value: 'Insurance', label: 'Bảo hiểm' },
  ];

  save() {
    if (!this.form.amount || this.form.amount <= 0) {
      this.error.set('Số tiền thanh toán phải lớn hơn 0');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.service
      .recordPayment(this.invoiceId, {
        amount: this.form.amount,
        paymentMethod: this.form.paymentMethod,
        transactionReference: this.form.transactionReference || undefined,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/billing', this.invoiceId]);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi xử lý thanh toán');
          this.saving.set(false);
        },
      });
  }

  back() {
    this.router.navigate(['/billing', this.invoiceId]);
  }
}
