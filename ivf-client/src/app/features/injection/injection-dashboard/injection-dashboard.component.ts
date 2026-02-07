import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { InjectionService } from './injection.service';
import { QueueTicket } from '../../../core/models/api.models';

@Component({
  selector: 'app-injection-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './injection-dashboard.component.html',
  styleUrls: ['./injection-dashboard.component.scss']
})
export class InjectionDashboardComponent implements OnInit {
  private service = inject(InjectionService);

  activeTab = 'queue';
  queue = signal<QueueTicket[]>([]);
  history = signal<QueueTicket[]>([]);
  queueCount = signal(0);
  completedCount = signal(0);

  showForm = false;
  currentTicketId: string | null = null;
  currentPatientName = '';
  injectionNotes = '';

  ngOnInit() {
    this.refreshQueue();

    // Auto-refresh queue every 10 seconds
    setInterval(() => this.refreshQueue(), 10000);
  }

  refreshQueue() {
    this.service.getQueue().subscribe(data => {
      this.queue.set(data);
      this.queueCount.set(data.length);
    });

    this.service.getHistory().subscribe(data => {
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
        alert(`Đang gọi mời ${q.patientName}`);
      },
      error: err => alert('Lỗi: ' + err.message)
    });
  }

  startInjection(q: QueueTicket) {
    this.service.startService(q.id).subscribe({
      next: () => {
        this.currentTicketId = q.id;
        this.currentPatientName = q.patientName || '';
        this.showForm = true;
        this.refreshQueue();
      },
      error: err => alert('Lỗi bắt đầu: ' + err.message)
    });
  }

  skipPatient(q: QueueTicket) {
    if (confirm(`Bỏ qua bệnh nhân ${q.patientName}?`)) {
      this.service.skipTicket(q.id).subscribe({
        next: () => this.refreshQueue(),
        error: err => alert('Lỗi: ' + err.message)
      });
    }
  }

  submitInjection() {
    if (!this.currentTicketId) return;
    this.service.completeTicket(this.currentTicketId, this.injectionNotes).subscribe(() => {
      alert('Đã hoàn thành tiêm!');
      this.showForm = false;
      this.injectionNotes = '';
      this.currentTicketId = null;
      this.completedCount.update(c => c + 1);
      this.refreshQueue();
    });
  }
}
