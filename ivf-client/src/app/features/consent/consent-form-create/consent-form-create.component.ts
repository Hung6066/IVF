import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ConsentFormService } from '../../../core/services/consent-form.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';
import { CreateConsentFormRequest, ConsentType } from '../../../core/models/consent-form.models';

@Component({
  selector: 'app-consent-form-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent, CycleSearchComponent],
  templateUrl: './consent-form-create.component.html',
  styleUrls: ['./consent-form-create.component.scss'],
})
export class ConsentFormCreateComponent {
  private service = inject(ConsentFormService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');

  form: CreateConsentFormRequest = {
    patientId: '',
    cycleId: '',
    consentType: 'General',
    title: '',
    content: '',
  };

  consentTypes: { value: ConsentType; label: string }[] = [
    { value: 'OPU', label: 'Chọc hút trứng' },
    { value: 'IUI', label: 'Bơm tinh trùng (IUI)' },
    { value: 'Anesthesia', label: 'Tiền mê / Gây mê' },
    { value: 'EggDonation', label: 'Cho trứng' },
    { value: 'SpermDonation', label: 'Cho tinh trùng' },
    { value: 'FET', label: 'Chuyển phôi trữ (FET)' },
    { value: 'General', label: 'Đồng thuận chung' },
  ];

  save() {
    if (!this.form.patientId || !this.form.title) {
      this.error.set('Vui lòng nhập đầy đủ thông tin bắt buộc');
      return;
    }
    this.saving.set(true);
    this.service.create(this.form).subscribe({
      next: (result) => this.router.navigate(['/consent', result.id]),
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi tạo consent');
        this.saving.set(false);
      },
    });
  }

  back() {
    this.router.navigate(['/consent']);
  }
}
