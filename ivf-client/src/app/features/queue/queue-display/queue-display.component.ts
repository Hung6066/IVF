import { Component, OnInit, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { QueueTicket } from '../../../core/models/api.models';

@Component({
    selector: 'app-queue-display',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="queue-display">
      <header class="page-header">
        <h1>{{ getDepartmentName(departmentCode) }}</h1>
        <div class="header-actions">
          <span class="ticket-count">🎫 {{ tickets().length }} số chờ</span>
          <button class="btn-refresh" (click)="loadQueue()">🔄 Làm mới</button>
        </div>
      </header>

      <div class="queue-grid">
        <section class="current-section">
          <h2>Đang phục vụ</h2>
          <div class="current-ticket" [class.active]="currentTicket()">
            @if (currentTicket()) {
              <span class="ticket-number">{{ currentTicket()!.ticketNumber }}</span>
              <span class="patient-name">{{ currentTicket()!.patientName || 'Bệnh nhân' }}</span>
            } @else {
              <span class="no-ticket">Chưa có</span>
            }
          </div>
          @if (currentTicket()) {
            <div class="current-actions">
              <button class="btn-complete" (click)="completeTicket()">✅ Hoàn thành</button>
              <button class="btn-skip" (click)="skipTicket()">⏭️ Bỏ qua</button>
            </div>
          }
        </section>

        <section class="waiting-section">
          <h2>Hàng chờ</h2>
          <div class="waiting-list">
            @for (ticket of waitingTickets(); track ticket.id; let i = $index) {
              <div class="waiting-item" [class.next]="i === 0">
                <span class="position">{{ i + 1 }}</span>
                <span class="ticket-num">{{ ticket.ticketNumber }}</span>
                <span class="time">{{ formatTime(ticket.issuedAt) }}</span>
                @if (i === 0) {
                  <button class="btn-call" (click)="callTicket(ticket.id)">📢 Gọi</button>
                }
              </div>
            } @empty {
              <div class="empty-queue">Không có bệnh nhân chờ</div>
            }
          </div>
        </section>
      </div>
    </div>
  `,
    styles: [`
    .queue-display { max-width: 1200px; }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
    }

    .page-header h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .ticket-count {
      background: #f1f5f9;
      padding: 0.5rem 1rem;
      border-radius: 999px;
      font-size: 0.875rem;
    }

    .btn-refresh {
      padding: 0.5rem 1rem;
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      cursor: pointer;
    }

    .queue-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 2rem;
    }

    .current-section, .waiting-section {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .current-section h2, .waiting-section h2 {
      font-size: 1rem;
      color: #6b7280;
      margin: 0 0 1.5rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .current-ticket {
      background: #f8fafc;
      border-radius: 16px;
      padding: 3rem;
      text-align: center;
      margin-bottom: 1.5rem;
    }

    .current-ticket.active {
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
    }

    .ticket-number {
      display: block;
      font-size: 4rem;
      font-weight: 700;
      line-height: 1;
      margin-bottom: 0.5rem;
    }

    .patient-name {
      font-size: 1.25rem;
      opacity: 0.9;
    }

    .no-ticket {
      font-size: 1.5rem;
      color: #9ca3af;
    }

    .current-actions {
      display: flex;
      gap: 1rem;
    }

    .btn-complete, .btn-skip {
      flex: 1;
      padding: 0.875rem;
      border: none;
      border-radius: 8px;
      font-size: 0.9375rem;
      font-weight: 500;
      cursor: pointer;
    }

    .btn-complete {
      background: #10b981;
      color: white;
    }

    .btn-skip {
      background: #f1f5f9;
      color: #64748b;
    }

    .waiting-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      max-height: 400px;
      overflow-y: auto;
    }

    .waiting-item {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem;
      background: #f8fafc;
      border-radius: 8px;
    }

    .waiting-item.next {
      background: linear-gradient(135deg, rgba(102,126,234,0.1), rgba(118,75,162,0.1));
      border: 1px solid rgba(102,126,234,0.3);
    }

    .position {
      width: 28px;
      height: 28px;
      background: #e2e8f0;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.75rem;
      font-weight: 600;
    }

    .waiting-item.next .position {
      background: #667eea;
      color: white;
    }

    .ticket-num {
      font-weight: 600;
      font-size: 1.125rem;
    }

    .time {
      color: #6b7280;
      font-size: 0.875rem;
      margin-left: auto;
    }

    .btn-call {
      padding: 0.5rem 1rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 6px;
      font-size: 0.8125rem;
      cursor: pointer;
    }

    .empty-queue {
      text-align: center;
      color: #9ca3af;
      padding: 2rem;
    }
  `]
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
            'AND': 'Nam khoa'
        };
        return names[code] || code;
    }

    formatTime(date: string): string {
        return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }
}
