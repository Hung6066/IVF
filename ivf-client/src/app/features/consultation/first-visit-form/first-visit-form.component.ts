import { Component, Input, Output, EventEmitter, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConsultationApiService } from '../../../core/services/consultation-api.service';
import { ConsultationDto, RecordClinicalDataRequest } from '../../../core/models/consultation.models';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';

@Component({
  selector: 'app-first-visit-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './first-visit-form.component.html',
  styleUrls: ['./first-visit-form.component.scss'],
})
export class FirstVisitFormComponent implements OnInit {
  private consultationApi = inject(ConsultationApiService);
  private notificationService = inject(GlobalNotificationService);

  @Input({ required: true }) consultation!: ConsultationDto;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  saving = signal(false);

  form: RecordClinicalDataRequest = {
    chiefComplaint: '',
    medicalHistory: '',
    pastHistory: '',
    surgicalHistory: '',
    familyHistory: '',
    obstetricHistory: '',
    menstrualHistory: '',
    physicalExamination: '',
  };

  ngOnInit(): void {
    // Pre-fill from existing data if editing
    this.form = {
      chiefComplaint: this.consultation.chiefComplaint || '',
      medicalHistory: this.consultation.medicalHistory || '',
      pastHistory: this.consultation.pastHistory || '',
      surgicalHistory: this.consultation.surgicalHistory || '',
      familyHistory: this.consultation.familyHistory || '',
      obstetricHistory: this.consultation.obstetricHistory || '',
      menstrualHistory: this.consultation.menstrualHistory || '',
      physicalExamination: this.consultation.physicalExamination || '',
    };
  }

  onSubmit(): void {
    if (!this.form.chiefComplaint?.trim()) {
      this.notificationService.error('Thieu thong tin', 'Vui long nhap ly do kham');
      return;
    }

    this.saving.set(true);
    this.consultationApi.recordClinicalData(this.consultation.id, this.form).subscribe({
      next: () => {
        this.notificationService.success('Thanh cong', 'Da luu thong tin kham');
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
