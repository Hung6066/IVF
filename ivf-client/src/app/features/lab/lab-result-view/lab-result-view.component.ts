import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LabOrderDto } from '../../../core/models/lab-order.models';

@Component({
  selector: 'app-lab-result-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './lab-result-view.component.html',
  styleUrls: ['./lab-result-view.component.scss'],
})
export class LabResultViewComponent {
  @Input({ required: true }) order!: LabOrderDto;
  @Output() closed = new EventEmitter<void>();

  close(): void {
    this.closed.emit();
  }

  printResults(): void {
    window.print();
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Ordered: 'Da chi dinh',
      SampleCollected: 'Da lay mau',
      InProgress: 'Dang thuc hien',
      Completed: 'Hoan thanh',
      Delivered: 'Da tra KQ',
    };
    return labels[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Ordered: 'status-badge pending',
      SampleCollected: 'status-badge info',
      InProgress: 'status-badge processing',
      Completed: 'status-badge success',
      Delivered: 'status-badge success',
    };
    return classes[status] || 'status-badge';
  }

  hasAnyAbnormal(): boolean {
    return this.order.tests.some(t => t.isAbnormal);
  }
}
