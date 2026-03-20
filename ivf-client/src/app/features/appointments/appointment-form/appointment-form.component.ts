import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AppointmentService } from '../../../core/services/appointment.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';
import { AppointmentType, CreateAppointmentRequest } from '../../../core/models/appointment.models';

@Component({
  selector: 'app-appointment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent, CycleSearchComponent],
  templateUrl: './appointment-form.component.html',
  styleUrls: ['./appointment-form.component.scss'],
})
export class AppointmentFormComponent {
  private service = inject(AppointmentService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');

  form: CreateAppointmentRequest = {
    patientId: '',
    scheduledAt: new Date(Date.now() + 3600_000).toISOString().slice(0, 16),
    type: 'Consultation',
    durationMinutes: 30,
    doctorId: '',
    cycleId: '',
    roomNumber: '',
    notes: '',
  };

  types: AppointmentType[] = [
    'Consultation',
    'Ultrasound',
    'Injection',
    'EggRetrieval',
    'EmbryoTransfer',
    'LabTest',
    'SemenCollection',
    'FollowUp',
    'Other',
  ];

  typeLabels: Record<AppointmentType, string> = {
    Consultation: 'Tư vấn',
    Ultrasound: 'Siêu âm',
    Injection: 'Tiêm thuốc',
    EggRetrieval: 'Chọc hút trứng',
    EmbryoTransfer: 'Chuyển phôi',
    LabTest: 'Xét nghiệm',
    SemenCollection: 'Lấy tinh trùng',
    FollowUp: 'Tái khám',
    Other: 'Khác',
  };

  save() {
    if (!this.form.patientId) {
      this.error.set('Vui lòng nhập mã bệnh nhân');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    const payload: CreateAppointmentRequest = {
      ...this.form,
      doctorId: this.form.doctorId || undefined,
      cycleId: this.form.cycleId || undefined,
      roomNumber: this.form.roomNumber || undefined,
    };
    this.service.createAppointment(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/appointments']);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi tạo lịch hẹn');
        this.saving.set(false);
      },
    });
  }

  back() {
    this.router.navigate(['/appointments']);
  }
}
