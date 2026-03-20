import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { EggBankService } from '../../../core/services/egg-bank.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';

@Component({
  selector: 'app-egg-donor-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent],
  templateUrl: './egg-donor-register.component.html',
  styleUrls: ['./egg-donor-register.component.scss'],
})
export class EggDonorRegisterComponent {
  private service = inject(EggBankService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');
  patientId = '';

  submit() {
    if (!this.patientId.trim()) {
      this.error.set('Vui lòng nhập ID bệnh nhân');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.service.createDonor({ patientId: this.patientId }).subscribe({
      next: (d) => this.router.navigate(['/egg-donor', d.id]),
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi đăng ký NCT');
        this.saving.set(false);
      },
    });
  }
}
