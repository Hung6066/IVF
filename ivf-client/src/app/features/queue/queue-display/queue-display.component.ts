import { Component, OnInit, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { QueueService } from '../../../core/services/queue.service';
import { QueueTicket } from '../../../core/models/api.models';

@Component({
  selector: 'app-queue-display',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './queue-display.component.html',
  styleUrls: ['./queue-display.component.scss']
})
export class QueueDisplayComponent implements OnInit, OnDestroy {
  departmentCode = '';
  tickets = signal<QueueTicket[]>([]);
  currentTickets = signal<QueueTicket[]>([]);
  waitingTickets = signal<QueueTicket[]>([]);

  blinkEffect = false;

  private refreshInterval?: ReturnType<typeof setInterval>;

  constructor(private route: ActivatedRoute, private queueService: QueueService) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.departmentCode = params['departmentCode'] || 'ALL'; // Default to ALL if missing (though route usually requires it)
      this.loadQueue();
    });
    // Auto-refresh every 10 seconds
    this.refreshInterval = setInterval(() => this.loadQueue(), 10000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  loadQueue(): void {
    if (!this.departmentCode) return;
    this.queueService.getQueueByDept(this.departmentCode).subscribe(tickets => {
      this.tickets.set(tickets);

      const active = tickets.filter(t => t.status === 'Called' || t.status === 'InService');
      this.currentTickets.set(active);

      this.waitingTickets.set(tickets.filter(t => t.status === 'Waiting'));

      if (active.length > 0 && active.some(t => t.status === 'Called')) {
        this.blinkEffect = true;
        setTimeout(() => this.blinkEffect = false, 3000);
      }
    });
  }

  callTicket(id: string): void {
    this.queueService.callTicket(id).subscribe(() => this.loadQueue());
  }

  completeTicket(id: string): void {
    this.queueService.completeTicket(id).subscribe(() => this.loadQueue());
  }

  skipTicket(id: string): void {
    this.queueService.skipTicket(id).subscribe(() => this.loadQueue());
  }

  getDepartmentName(code: string): string {
    if (code?.toUpperCase() === 'ALL') return 'Tất cả các hàng chờ';
    const names: Record<string, string> = {
      'REC': 'Tiếp đón',
      'US': 'Siêu âm',
      'LAB': 'Xét nghiệm',
      'AND': 'Nam khoa',
      'CON': 'Tư vấn',
      'INJ': 'Tiêm',
      'TV': 'Tư vấn',
      'TM': 'Tiêm',
      'XN': 'Xét nghiệm',
      'NAM': 'Nam khoa'
    };
    return names[code] || code;
  }

  formatTime(date: string): string {
    if (!date) return '';
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }
}
