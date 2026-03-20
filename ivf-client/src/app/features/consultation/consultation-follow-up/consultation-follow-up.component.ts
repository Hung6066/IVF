import { Component, Input, Output, EventEmitter, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConsultationApiService } from '../../../core/services/consultation-api.service';
import { ConsultationDto, RecordClinicalDataRequest } from '../../../core/models/consultation.models';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';

@Component({
  selector: 'app-consultation-follow-up',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './consultation-follow-up.component.html',
  styleUrls: ['./consultation-follow-up.component.scss'],
})
export class ConsultationFollowUpComponent implements OnInit {
  private consultationApi = inject(ConsultationApiService);
  private notificationService = inject(GlobalNotificationService);

  @Input({ required: true }) consultation!: ConsultationDto;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  saving = signal(false);

  form = {
    sinceLastVisit: '',
    currentSymptoms: '',
    physicalExamination: '',
    chiefComplaint: '',
  };

  ngOnInit(): void {
    this.form.chiefComplaint = this.consultation.chiefComplaint || '';
    this.form.physicalExamination = this.consultation.physicalExamination || '';
  }

  onSubmit(): void {
    if (!this.form.sinceLastVisit?.trim() && !this.form.currentSymptoms?.trim()) {
      this.notificationService.error('Thieu thong tin', 'Vui long nhap dien bien hoac trieu chung');
      return;
    }

    // Map the follow-up fields into clinical data format
    const request: RecordClinicalDataRequest = {
      chiefComplaint: this.form.chiefComplaint || undefined,
      medicalHistory: this.form.sinceLastVisit
        ? `[Tai kham] Dien bien tu lan kham truoc: ${this.form.sinceLastVisit}`
        : undefined,
      physicalExamination: this.buildExamNotes(),
    };

    this.saving.set(true);
    this.consultationApi.recordClinicalData(this.consultation.id, request).subscribe({
      next: () => {
        this.notificationService.success('Thanh cong', 'Da luu phieu tai kham');
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.notificationService.error('Loi', err.error?.detail || err.message);
        this.saving.set(false);
      },
    });
  }

  private buildExamNotes(): string {
    const parts: string[] = [];
    if (this.form.currentSymptoms) {
      parts.push(`Trieu chung hien tai: ${this.form.currentSymptoms}`);
    }
    if (this.form.physicalExamination) {
      parts.push(`Kham lam sang: ${this.form.physicalExamination}`);
    }
    return parts.join('\n') || '';
  }

  close(): void {
    this.closed.emit();
  }
}
