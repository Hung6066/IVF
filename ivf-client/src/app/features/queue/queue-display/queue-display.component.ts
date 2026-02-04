import { Component, OnInit, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
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
  currentTicket = signal<QueueTicket | null>(null);
  waitingTickets = signal<QueueTicket[]>([]);

  private refreshInterval?: ReturnType<typeof setInterval>;

  constructor(private route: ActivatedRoute, private api: ApiService) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.departmentCode = params['departmentCode'];
      this.loadQueue();
    });
    // Auto-refresh every 10 seconds
    this.refreshInterval = setInterval(() => this.loadQueue(), 10000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  loadQueue(): void {
    this.api.getQueue(this.departmentCode).subscribe(tickets => {
      this.tickets.set(tickets);

      const current = tickets.find(t => t.status === 'InService' || t.status === 'Called');
      this.currentTicket.set(current || null);

      this.waitingTickets.set(tickets.filter(t => t.status === 'Waiting'));
    });
  }

  callTicket(id: string): void {
    this.api.callTicket(id).subscribe(() => this.loadQueue());
  }

  completeTicket(): void {
    const current = this.currentTicket();
    if (current) {
      this.api.completeTicket(current.id).subscribe(() => this.loadQueue());
    }
  }

  skipTicket(): void {
    const current = this.currentTicket();
    if (current) {
      this.api.skipTicket(current.id).subscribe(() => this.loadQueue());
    }
  }

  getDepartmentName(code: string): string {
    const names: Record<string, string> = {
      'REC': 'Tiếp đón',
      'US': 'Siêu âm',
      'LAB': 'Xét nghiệm',
      'AND': 'Nam khoa',
      'CON': 'Tư vấn',
      'INJ': 'Tiêm'
    };
    return names[code] || code;
  }

  formatTime(date: string): string {
    if (!date) return '';
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }
}
