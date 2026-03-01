import { Component, OnInit, signal, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';

interface SecurityEventItem {
  id: string;
  eventType: string;
  severity: string;
  userId?: string;
  username?: string;
  ipAddress?: string;
  userAgent?: string;
  country?: string;
  details?: string;
  isBlocked: boolean;
  timestamp: string;
}

interface SecurityDashboard24h {
  last24Hours: {
    totalHighSeverity: number;
    blockedRequests: number;
    threatsByType: { type: string; count: number }[];
    uniqueIps: number;
    uniqueUsers: number;
  };
  recentEvents: SecurityEventItem[];
}

@Component({
  selector: 'app-siem-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="tab-content">
      <div class="section-header">
        <h3>🔍 SIEM Security Events</h3>
        <button class="btn btn-primary" (click)="loadData()" [disabled]="loading()">
          🔄 Refresh
        </button>
      </div>

      @if (dashboard()) {
        <!-- Summary Cards -->
        <div class="card-grid four-col">
          <div class="metric-card" [class.danger]="dashboard()!.last24Hours.totalHighSeverity > 0">
            <span class="metric-icon">🚨</span>
            <span class="metric-value">{{ dashboard()!.last24Hours.totalHighSeverity }}</span>
            <span class="metric-label">High Severity (24h)</span>
          </div>
          <div class="metric-card" [class.danger]="dashboard()!.last24Hours.blockedRequests > 0">
            <span class="metric-icon">🛑</span>
            <span class="metric-value">{{ dashboard()!.last24Hours.blockedRequests }}</span>
            <span class="metric-label">Blocked Requests</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">🌐</span>
            <span class="metric-value">{{ dashboard()!.last24Hours.uniqueIps }}</span>
            <span class="metric-label">Unique IPs</span>
          </div>
          <div class="metric-card">
            <span class="metric-icon">👤</span>
            <span class="metric-value">{{ dashboard()!.last24Hours.uniqueUsers }}</span>
            <span class="metric-label">Unique Users</span>
          </div>
        </div>

        <!-- Threats by Type -->
        @if (dashboard()!.last24Hours.threatsByType.length > 0) {
          <div style="margin-top: 1.5rem;">
            <h4>Threats by Type</h4>
            <div class="threat-bars">
              @for (t of dashboard()!.last24Hours.threatsByType; track t.type) {
                <div class="threat-bar-item">
                  <span class="threat-type">{{ t.type }}</span>
                  <div class="threat-bar">
                    <div class="threat-fill" [style.width.%]="getMaxPct(t.count)"></div>
                    <span class="threat-count">{{ t.count }}</span>
                  </div>
                </div>
              }
            </div>
          </div>
        }
      }

      <!-- Recent Events Table -->
      <div class="section-header" style="margin-top: 2rem;">
        <h3>📋 Recent Events</h3>
        <div class="form-row compact">
          <select [(ngModel)]="severityFilter" (ngModelChange)="filterEvents()">
            <option value="">Tất cả</option>
            <option value="high">High</option>
            <option value="critical">Critical</option>
          </select>
        </div>
      </div>

      <table class="data-table">
        <thead>
          <tr>
            <th>Thời gian</th>
            <th>Event Type</th>
            <th>Severity</th>
            <th>IP</th>
            <th>User</th>
            <th>Blocked</th>
            <th>Details</th>
          </tr>
        </thead>
        <tbody>
          @for (e of filteredEvents(); track e.id) {
            <tr [class.blocked-row]="e.isBlocked">
              <td>{{ e.timestamp | date: 'dd/MM HH:mm:ss' }}</td>
              <td class="mono">{{ e.eventType }}</td>
              <td>
                <span [class]="'badge badge-' + getSeverityClass(e.severity)">
                  {{ e.severity }}
                </span>
              </td>
              <td class="mono">{{ e.ipAddress || '—' }}</td>
              <td>{{ e.username || '—' }}</td>
              <td>
                <span [class]="e.isBlocked ? 'badge badge-danger' : 'badge badge-muted'">
                  {{ e.isBlocked ? '🛑' : '—' }}
                </span>
              </td>
              <td class="truncate">{{ e.details || '—' }}</td>
            </tr>
          }
          @if (filteredEvents().length === 0) {
            <tr>
              <td colspan="7" class="empty">Không có events</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class SiemTabComponent implements OnInit {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/security`;

  dashboard = signal<SecurityDashboard24h | null>(null);
  events = signal<SecurityEventItem[]>([]);
  filteredEvents = signal<SecurityEventItem[]>([]);
  loading = signal(false);
  severityFilter = '';

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.http.get<SecurityDashboard24h>(`${this.baseUrl}/dashboard`).subscribe({
      next: (d) => {
        this.dashboard.set(d);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.http.get<SecurityEventItem[]>(`${this.baseUrl}/events/recent?count=100`).subscribe({
      next: (e) => {
        this.events.set(e);
        this.filteredEvents.set(e);
      },
    });
  }

  filterEvents() {
    const all = this.events();
    if (!this.severityFilter) {
      this.filteredEvents.set(all);
    } else {
      this.filteredEvents.set(
        all.filter((e) => e.severity.toLowerCase() === this.severityFilter.toLowerCase()),
      );
    }
  }

  getMaxPct(count: number): number {
    const d = this.dashboard();
    if (!d || d.last24Hours.totalHighSeverity === 0) return 0;
    return Math.min(100, (count / d.last24Hours.totalHighSeverity) * 100);
  }

  getSeverityClass(severity: string): string {
    const map: Record<string, string> = {
      critical: 'danger',
      high: 'danger',
      medium: 'warning',
      low: 'info',
      info: 'muted',
    };
    return map[severity.toLowerCase()] || 'muted';
  }
}
