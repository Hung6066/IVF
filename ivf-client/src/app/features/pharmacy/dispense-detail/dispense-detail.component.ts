import { Component, Input, Output, EventEmitter, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PrescriptionDto, PrescriptionItemDto } from '../../../core/models/prescription.models';
import { PrescriptionService } from '../../../core/services/prescription.service';
import { AuthService } from '../../../core/services/auth.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';

interface DispenseRow {
  item: PrescriptionItemDto;
  inStock: boolean;
  substituteNote: string;
  dispensed: boolean;
}

@Component({
  selector: 'app-dispense-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dispense-detail.component.html',
  styleUrls: ['./dispense-detail.component.scss'],
})
export class DispenseDetailComponent {
  private prescriptionService = inject(PrescriptionService);
  private authService = inject(AuthService);
  private notificationService = inject(GlobalNotificationService);

  @Input({ required: true }) prescription!: PrescriptionDto;
  @Output() closed = new EventEmitter<void>();
  @Output() dispensed = new EventEmitter<void>();

  saving = signal(false);
  patientInstructions = '';
  dispenseRows: DispenseRow[] = [];

  ngOnInit(): void {
    this.dispenseRows = this.prescription.items.map(item => ({
      item,
      inStock: true, // In real app, check against inventory
      substituteNote: '',
      dispensed: false,
    }));
  }

  get allChecked(): boolean {
    return this.dispenseRows.length > 0 && this.dispenseRows.every(r => r.dispensed || !r.inStock);
  }

  toggleDispensed(row: DispenseRow): void {
    row.dispensed = !row.dispensed;
  }

  toggleOutOfStock(row: DispenseRow): void {
    row.inStock = !row.inStock;
    if (!row.inStock) {
      row.dispensed = false;
    }
  }

  onDispense(): void {
    const userId = this.authService.user()?.id;
    if (!userId) {
      this.notificationService.error('Loi', 'Khong xac dinh duoc nguoi dung');
      return;
    }

    const dispensedCount = this.dispenseRows.filter(r => r.dispensed).length;
    if (dispensedCount === 0) {
      this.notificationService.error('Loi', 'Vui long danh dau it nhat 1 thuoc da phat');
      return;
    }

    this.saving.set(true);

    // Update notes with patient instructions and substitutions
    const notes = this.buildDispenseNotes();
    const notesObs = notes
      ? this.prescriptionService.updateNotes(this.prescription.id, notes)
      : undefined;

    const dispenseAction = () => {
      this.prescriptionService.dispense(this.prescription.id, userId).subscribe({
        next: () => {
          this.notificationService.success('Thanh cong', 'Da phat thuoc thanh cong');
          this.saving.set(false);
          this.dispensed.emit();
        },
        error: (err) => {
          this.notificationService.error('Loi', err.error?.detail || err.message);
          this.saving.set(false);
        },
      });
    };

    if (notesObs) {
      notesObs.subscribe({
        next: () => dispenseAction(),
        error: () => dispenseAction(), // Still try to dispense even if notes fail
      });
    } else {
      dispenseAction();
    }
  }

  private buildDispenseNotes(): string {
    const parts: string[] = [];

    const substitutions = this.dispenseRows.filter(r => r.substituteNote.trim());
    if (substitutions.length > 0) {
      parts.push('Thay the: ' + substitutions.map(r => `${r.item.drugName}: ${r.substituteNote}`).join('; '));
    }

    const outOfStock = this.dispenseRows.filter(r => !r.inStock);
    if (outOfStock.length > 0) {
      parts.push('Het hang: ' + outOfStock.map(r => r.item.drugName).join(', '));
    }

    if (this.patientInstructions.trim()) {
      parts.push('Huong dan: ' + this.patientInstructions.trim());
    }

    return parts.join(' | ');
  }

  close(): void {
    this.closed.emit();
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Pending: 'Cho xu ly',
      Entered: 'Da tiep nhan',
      Printed: 'Da in',
      Dispensed: 'Da phat',
      Cancelled: 'Da huy',
    };
    return labels[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Pending: 'status-badge pending',
      Entered: 'status-badge info',
      Printed: 'status-badge info',
      Dispensed: 'status-badge success',
      Cancelled: 'status-badge danger',
    };
    return classes[status] || 'status-badge';
  }
}
