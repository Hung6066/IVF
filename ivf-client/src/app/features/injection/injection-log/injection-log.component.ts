import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  MedicationAdminService,
  RecordMedicationAdminRequest,
} from '../../../core/services/medication-admin.service';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
  selector: 'app-injection-log',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, CycleSearchComponent],
  templateUrl: './injection-log.component.html',
  styleUrls: ['./injection-log.component.scss'],
})
export class InjectionLogComponent {
  private service = inject(MedicationAdminService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  form: RecordMedicationAdminRequest = {
    patientId: '',
    cycleId: '',
    medicationName: '',
    dosage: '',
    route: 'SC',
    administeredAt: new Date().toISOString().slice(0, 16),
    administeredByUserId: '',
    isTriggerShot: false,
    notes: '',
  };

  routes = ['SC', 'IM', 'IV', 'PO', 'Vaginal', 'Nasal'];

  save() {
    if (!this.form.patientId || !this.form.medicationName) {
      this.error.set('Vui lòng nhập đầy đủ thông tin');
      return;
    }
    this.saving.set(true);
    this.service.record(this.form).subscribe({
      next: () => {
        this.successMsg.set('Đã ghi nhận tiêm thuốc');
        this.saving.set(false);
        setTimeout(() => this.router.navigate(['/injection']), 1500);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  back() {
    this.router.navigate(['/injection']);
  }
}
