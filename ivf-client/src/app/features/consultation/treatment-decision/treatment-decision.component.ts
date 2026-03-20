import { Component, Input, Output, EventEmitter, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConsultationApiService } from '../../../core/services/consultation-api.service';
import { ConsultationDto, RecordDiagnosisRequest } from '../../../core/models/consultation.models';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';

@Component({
  selector: 'app-treatment-decision',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './treatment-decision.component.html',
  styleUrls: ['./treatment-decision.component.scss'],
})
export class TreatmentDecisionComponent implements OnInit {
  private consultationApi = inject(ConsultationApiService);
  private notificationService = inject(GlobalNotificationService);

  @Input({ required: true }) consultation!: ConsultationDto;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  saving = signal(false);

  form: RecordDiagnosisRequest = {
    diagnosis: '',
    treatmentPlan: '',
    recommendedMethod: '',
  };

  treatmentMethods = [
    { value: 'QHTN', label: 'QHTN - Quan he tinh nhien' },
    { value: 'IUI', label: 'IUI - Bom tinh trung' },
    { value: 'ICSI', label: 'ICSI - Tiem tinh trung vao bao tuong' },
    { value: 'IVM', label: 'IVM - Truong thanh trung trong ong nghiem' },
  ];

  patientConsent = false;

  ngOnInit(): void {
    this.form = {
      diagnosis: this.consultation.diagnosis || '',
      treatmentPlan: this.consultation.treatmentPlan || '',
      recommendedMethod: this.consultation.recommendedMethod || '',
    };
  }

  onSubmit(): void {
    if (!this.form.diagnosis?.trim()) {
      this.notificationService.error('Thieu thong tin', 'Vui long nhap chan doan');
      return;
    }
    if (!this.form.recommendedMethod) {
      this.notificationService.error('Thieu thong tin', 'Vui long chon phuong phap dieu tri');
      return;
    }

    this.saving.set(true);
    this.consultationApi.recordDiagnosis(this.consultation.id, this.form).subscribe({
      next: () => {
        this.notificationService.success('Thanh cong', 'Da luu quyet dinh dieu tri');
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.notificationService.error('Loi', err.error?.detail || err.message);
        this.saving.set(false);
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
