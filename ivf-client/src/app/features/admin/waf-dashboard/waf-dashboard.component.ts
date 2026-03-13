import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WafService } from '../../../core/services/waf.service';
import { WafStatus, WafRule, WafEvent } from '../../../core/models/waf.model';

@Component({
  selector: 'app-waf-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './waf-dashboard.component.html',
  styleUrls: ['./waf-dashboard.component.scss'],
})
export class WafDashboardComponent implements OnInit {
  status = signal<WafStatus | null>(null);
  events = signal<WafEvent[]>([]);
  loading = signal(false);
  eventsLoading = signal(false);
  error = signal<string | null>(null);
  activeTab = signal<'overview' | 'rules' | 'events'>('overview');

  constructor(private wafService: WafService) {}

  ngOnInit(): void {
    this.loadStatus();
    this.loadEvents();
  }

  loadStatus(): void {
    this.loading.set(true);
    this.error.set(null);
    this.wafService.getStatus().subscribe({
      next: (s) => {
        this.status.set(s);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.status === 503 ? 'WAF chưa được cấu hình' : 'Không thể tải trạng thái WAF');
        this.loading.set(false);
      },
    });
  }

  loadEvents(): void {
    this.eventsLoading.set(true);
    this.wafService.getEvents(100).subscribe({
      next: (events) => {
        this.events.set(events);
        this.eventsLoading.set(false);
      },
      error: () => this.eventsLoading.set(false),
    });
  }

  getActionBadgeClass(action: string): string {
    switch (action) {
      case 'block': return 'badge-danger';
      case 'managed_challenge': return 'badge-warning';
      case 'challenge': return 'badge-warning';
      case 'execute': return 'badge-info';
      case 'log': return 'badge-secondary';
      default: return 'badge-secondary';
    }
  }

  getActionLabel(action: string): string {
    switch (action) {
      case 'block': return 'Chặn';
      case 'managed_challenge': return 'Thách thức';
      case 'challenge': return 'CAPTCHA';
      case 'execute': return 'Thực thi';
      case 'log': return 'Ghi log';
      default: return action;
    }
  }

  formatTime(ts: string): string {
    return new Date(ts).toLocaleString('vi-VN');
  }
}
