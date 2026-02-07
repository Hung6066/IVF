import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QueueItem } from '../../lab-dashboard.models';

@Component({
  selector: 'app-lab-queue',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './lab-queue.component.html',
  styleUrls: ['./lab-queue.component.scss']
})
export class LabQueueComponent {
  @Input() queue: QueueItem[] = [];
  @Output() callPatient = new EventEmitter<QueueItem>();
  @Output() startProcedure = new EventEmitter<QueueItem>();

  formatTime(date: string): string {
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  onCall(q: QueueItem) {
    this.callPatient.emit(q);
  }

  onStart(q: QueueItem) {
    this.startProcedure.emit(q);
  }
}
