import { Component, Input, Output, EventEmitter, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LabOrderService } from '../../../core/services/lab-order.service';
import { LabOrderDto, LabTestResultInput } from '../../../core/models/lab-order.models';
import { AuthService } from '../../../core/services/auth.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';

interface ResultRow {
  testId: string;
  testCode: string;
  testName: string;
  referenceRange: string;
  resultValue: string;
  resultUnit: string;
  isAbnormal: boolean;
  notes: string;
}

@Component({
  selector: 'app-lab-result-entry',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lab-result-entry.component.html',
  styleUrls: ['./lab-result-entry.component.scss'],
})
export class LabResultEntryComponent implements OnInit {
  private labOrderService = inject(LabOrderService);
  private authService = inject(AuthService);
  private notificationService = inject(GlobalNotificationService);

  @Input({ required: true }) order!: LabOrderDto;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  saving = signal(false);
  resultRows: ResultRow[] = [];

  ngOnInit(): void {
    this.resultRows = this.order.tests.map(test => ({
      testId: test.id,
      testCode: test.testCode,
      testName: test.testName,
      referenceRange: test.referenceRange || '',
      resultValue: test.resultValue || '',
      resultUnit: test.resultUnit || '',
      isAbnormal: test.isAbnormal,
      notes: test.notes || '',
    }));
  }

  onSubmit(): void {
    const userId = this.authService.user()?.id;
    if (!userId) {
      this.notificationService.error('Loi', 'Khong xac dinh duoc nguoi dung');
      return;
    }

    const results: LabTestResultInput[] = this.resultRows
      .filter(r => r.resultValue.trim() !== '')
      .map(r => ({
        testId: r.testId,
        resultValue: r.resultValue,
        resultUnit: r.resultUnit || undefined,
        isAbnormal: r.isAbnormal,
        notes: r.notes || undefined,
      }));

    if (results.length === 0) {
      this.notificationService.error('Loi', 'Vui long nhap it nhat 1 ket qua');
      return;
    }

    this.saving.set(true);
    this.labOrderService.enterResults(this.order.id, { performedByUserId: userId, results }).subscribe({
      next: () => {
        this.notificationService.success('Thanh cong', 'Da luu ket qua xet nghiem');
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

  toggleAbnormal(row: ResultRow): void {
    row.isAbnormal = !row.isAbnormal;
  }
}
