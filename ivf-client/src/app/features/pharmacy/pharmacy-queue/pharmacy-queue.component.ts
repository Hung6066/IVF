import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PrescriptionDto } from '../../../core/models/prescription.models';

@Component({
  selector: 'app-pharmacy-queue',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pharmacy-queue.component.html',
  styleUrls: ['./pharmacy-queue.component.scss'],
})
export class PharmacyQueueComponent {
  @Input() prescriptions: PrescriptionDto[] = [];
  @Output() selectPrescription = new EventEmitter<PrescriptionDto>();
  @Output() processPrescription = new EventEmitter<PrescriptionDto>();

  get pendingList(): PrescriptionDto[] {
    return this.prescriptions.filter(p => p.status === 'Pending');
  }

  get enteredList(): PrescriptionDto[] {
    return this.prescriptions.filter(p => p.status === 'Entered' || p.status === 'Printed');
  }

  get dispensedList(): PrescriptionDto[] {
    return this.prescriptions.filter(p => p.status === 'Dispensed');
  }

  getUrgencyClass(rx: PrescriptionDto): string {
    // Simple urgency heuristic based on number of items
    if (rx.items.length >= 5) return 'urgency-high';
    if (rx.items.length >= 3) return 'urgency-medium';
    return 'urgency-low';
  }

  getUrgencyLabel(rx: PrescriptionDto): string {
    if (rx.items.length >= 5) return 'Nhieu thuoc';
    if (rx.items.length >= 3) return 'Binh thuong';
    return 'Don gian';
  }

  onSelect(rx: PrescriptionDto): void {
    this.selectPrescription.emit(rx);
  }

  onProcess(rx: PrescriptionDto, event: Event): void {
    event.stopPropagation();
    this.processPrescription.emit(rx);
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    return new Date(dateStr).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }
}
