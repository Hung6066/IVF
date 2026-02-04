import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ConsultationService } from './consultation.service';
import { QueueTicket } from '../../../core/models/api.models';

@Component({
  selector: 'app-consultation-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './consultation-dashboard.component.html',
  styleUrls: ['./consultation-dashboard.component.scss']
})
export class ConsultationDashboardComponent implements OnInit {
  private service = inject(ConsultationService);

  activeTab = 'queue';
  queue = signal<QueueTicket[]>([]);
  queueCount = signal(0);
  completedCount = signal(0);

  showForm = false;
  currentTicketId: string | null = null;
  currentPatientName = '';
  consultNotes = '';

  ngOnInit() {
    this.refreshQueue();
  }

  refreshQueue() {
    this.service.getQueue().subscribe(data => {
      this.queue.set(data);
      this.queueCount.set(data.length);
    });
  }

  formatTime(date: string): string {
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  callPatient(q: QueueTicket) {
    this.service.callPatient(q.id).subscribe({
      next: () => {
        this.refreshQueue();
        alert(`Đang gọi mời ${q.patientName}`);
      },
      error: err => alert('Lỗi: ' + err.message)
    });
  }

  startConsult(q: QueueTicket) {
    this.currentTicketId = q.id;
    this.currentPatientName = q.patientName || '';
    this.showForm = true;
  }

  submitConsult() {
    if (!this.currentTicketId) return;
    this.service.completeTicket(this.currentTicketId).subscribe(() => {
      alert('Đã hoàn thành tư vấn!');
      this.showForm = false;
      this.consultNotes = '';
      this.currentTicketId = null;
      this.completedCount.update(c => c + 1);
      this.refreshQueue();
    });
  }
}
