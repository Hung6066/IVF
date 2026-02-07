import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ConsultationService } from './consultation.service';
import { QueueTicket } from '../../../core/models/api.models';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';

@Component({
  selector: 'app-consultation-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './consultation-dashboard.component.html',
  styleUrls: ['./consultation-dashboard.component.scss']
})
export class ConsultationDashboardComponent implements OnInit {
  private service = inject(ConsultationService);
  private notificationService = inject(GlobalNotificationService);

  activeTab = 'queue';
  queue = signal<QueueTicket[]>([]);
  history = signal<any[]>([]);
  queueCount = signal(0);
  completedCount = signal(0);

  showForm = false;
  currentTicketId: string | null = null;
  currentPatientName = '';
  consultNotes = '';

  ngOnInit() {
    this.refreshQueue();
    this.refreshHistory();

    // Auto-refresh queue every 10 seconds
    setInterval(() => this.refreshQueue(), 10000);
  }

  refreshQueue() {
    this.service.getQueue().subscribe(data => {
      this.queue.set(data);
      this.queueCount.set(data.length);
    });
  }

  refreshHistory() {
    this.service.getHistory().subscribe((data: any[]) => {
      this.history.set(data);
      this.completedCount.set(data.length);
    });
  }

  formatTime(date: string): string {
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  callPatient(q: QueueTicket) {
    this.service.callPatient(q.id).subscribe({
      next: () => {
        this.refreshQueue();
        this.notificationService.info('Đang gọi', `Đang gọi mời ${q.patientName}`);
      },
      error: err => this.notificationService.error('Lỗi', 'Lỗi: ' + err.message)
    });
  }

  startConsult(q: QueueTicket) {
    this.service.startService(q.id).subscribe({
      next: () => {
        this.currentTicketId = q.id;
        this.currentPatientName = q.patientName || '';
        this.showForm = true;
        this.refreshQueue();
      },
      error: err => this.notificationService.error('Lỗi', 'Lỗi khi bắt đầu: ' + err.message)
    });
  }

  skipPatient(q: QueueTicket) {
    if (confirm(`Bỏ qua bệnh nhân ${q.patientName}?`)) {
      this.service.skipTicket(q.id).subscribe({
        next: () => {
          this.refreshQueue();
          this.notificationService.info('Đã bỏ qua', `Đã bỏ qua ${q.patientName}`);
        },
        error: err => this.notificationService.error('Lỗi', 'Lỗi: ' + err.message)
      });
    }
  }

  submitConsult() {
    if (!this.currentTicketId) return;
    this.service.completeTicket(this.currentTicketId, this.consultNotes).subscribe(() => {
      this.notificationService.success('Thành công', 'Đã hoàn thành tư vấn!');
      this.showForm = false;
      this.consultNotes = '';
      this.currentTicketId = null;
      this.completedCount.update(c => c + 1);
      this.refreshQueue();
      this.refreshHistory(); // Refresh history after completing
    });
  }
}
