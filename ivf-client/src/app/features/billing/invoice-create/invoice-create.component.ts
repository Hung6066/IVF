import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { FinanceService } from '../../../core/services/finance.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
  selector: 'app-invoice-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent, CycleSearchComponent],
  templateUrl: './invoice-create.component.html',
  styleUrls: ['./invoice-create.component.scss'],
})
export class InvoiceCreateComponent {
  private financeService = inject(FinanceService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');

  form = {
    patientId: '',
    cycleId: '',
  };

  save() {
    if (!this.form.patientId) {
      this.error.set('Vui lòng nhập mã bệnh nhân');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.financeService
      .createInvoice({ patientId: this.form.patientId, cycleId: this.form.cycleId || undefined })
      .subscribe({
        next: (inv) => {
          this.saving.set(false);
          this.router.navigate(['/billing', inv.id]);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi tạo hóa đơn');
          this.saving.set(false);
        },
      });
  }

  back() {
    this.router.navigate(['/billing']);
  }
}
