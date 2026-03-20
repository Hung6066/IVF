import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MedicationAdminService } from '../../../core/services/medication-admin.service';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
  selector: 'app-trigger-shot-record',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, CycleSearchComponent],
  templateUrl: './trigger-shot-record.component.html',
  styleUrls: ['./trigger-shot-record.component.scss'],
})
export class TriggerShotRecordComponent {
  private service = inject(MedicationAdminService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  form = {
    patientId: '',
    cycleId: '',
    medicationName: '',
    dosage: '',
    route: 'SC',
    administeredAt: '',
    administeredByUserId: '',
    isTriggerShot: true,
    notes: '',
  };

  triggerDate = new Date().toISOString().slice(0, 10);
  triggerTime = '22:00';
  estimatedOpuDate = '';
  estimatedOpuTime = '';

  calcOpuTime() {
    if (this.triggerDate && this.triggerTime) {
      const trigger = new Date(`${this.triggerDate}T${this.triggerTime}`);
      const opu = new Date(trigger.getTime() + 36 * 60 * 60 * 1000);
      this.estimatedOpuDate = opu.toLocaleDateString('vi-VN');
      this.estimatedOpuTime = opu.toLocaleTimeString('vi-VN', {
        hour: '2-digit',
        minute: '2-digit',
      });
      this.form.administeredAt = `${this.triggerDate}T${this.triggerTime}`;
    }
  }

  save() {
    this.form.administeredAt = `${this.triggerDate}T${this.triggerTime}`;
    this.saving.set(true);
    this.service.record(this.form).subscribe({
      next: () => {
        this.successMsg.set('Đã ghi nhận trigger shot');
        this.saving.set(false);
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
