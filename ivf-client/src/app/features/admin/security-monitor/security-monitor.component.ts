import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SecurityService } from '../../../core/services/security.service';
import {
  SecurityDashboard,
  SecurityEvent,
  ThreatByType,
} from '../../../core/models/security.model';

@Component({
  selector: 'app-security-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './security-monitor.component.html',
  styleUrls: ['./security-monitor.component.scss'],
})
export class SecurityMonitorComponent implements OnInit, OnDestroy {
  dashboard = signal<SecurityDashboard | null>(null);
  recentEvents = signal<SecurityEvent[]>([]);
  loading = signal(false);
  autoRefresh = signal(true);
  statusMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  private refreshInterval: ReturnType<typeof setInterval> | null = null;

  // Computed stats
  totalHighSeverity = computed(() => this.dashboard()?.last24Hours.totalHighSeverity ?? 0);
  blockedRequests = computed(() => this.dashboard()?.last24Hours.blockedRequests ?? 0);
  uniqueIps = computed(() => this.dashboard()?.last24Hours.uniqueIps ?? 0);
  uniqueUsers = computed(() => this.dashboard()?.last24Hours.uniqueUsers ?? 0);
  threatsByType = computed(() => this.dashboard()?.last24Hours.threatsByType ?? []);

  constructor(
    private securityService: SecurityService,
    private router: Router,
  ) {}

  ngOnInit() {
    this.loadDashboard();
    this.startAutoRefresh();
  }

  ngOnDestroy() {
    this.stopAutoRefresh();
  }

  loadDashboard() {
    this.loading.set(true);
    this.securityService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.recentEvents.set(data.recentEvents);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải dữ liệu bảo mật', 'error');
        this.loading.set(false);
      },
    });
  }

  toggleAutoRefresh() {
    this.autoRefresh.update((v) => !v);
    if (this.autoRefresh()) {
      this.startAutoRefresh();
    } else {
      this.stopAutoRefresh();
    }
  }

  private startAutoRefresh() {
    this.stopAutoRefresh();
    if (this.autoRefresh()) {
      this.refreshInterval = setInterval(() => this.loadDashboard(), 30000);
    }
  }

  private stopAutoRefresh() {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
      this.refreshInterval = null;
    }
  }

  navigateTo(route: string) {
    this.router.navigate([route]);
  }

  getSeverityClass(severity: string): string {
    switch (severity) {
      case 'Critical':
        return 'severity-critical';
      case 'High':
        return 'severity-high';
      case 'Medium':
        return 'severity-medium';
      case 'Low':
        return 'severity-low';
      default:
        return 'severity-info';
    }
  }

  getEventIcon(eventType: string): string {
    if (eventType.startsWith('AUTH_LOGIN_SUCCESS')) return '✅';
    if (eventType.startsWith('AUTH_LOGIN_FAILED')) return '❌';
    if (eventType.startsWith('AUTH_BRUTE')) return '🔨';
    if (eventType.startsWith('AUTH_')) return '🔑';
    if (eventType.startsWith('AUTHZ_')) return '🚫';
    if (eventType.startsWith('ZT_')) return '🛡️';
    if (eventType.startsWith('THREAT_')) return '⚠️';
    if (eventType.startsWith('SESSION_')) return '📋';
    if (eventType.startsWith('DEVICE_')) return '📱';
    if (eventType.startsWith('DATA_')) return '💾';
    return '🔵';
  }

  formatEventType(eventType: string): string {
    return eventType.replace(/_/g, ' ');
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  formatTimeAgo(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'Vừa xong';
    if (mins < 60) return `${mins} phút trước`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours} giờ trước`;
    return `${Math.floor(hours / 24)} ngày trước`;
  }

  getThreatBarWidth(threat: ThreatByType): number {
    const max = Math.max(...this.threatsByType().map((t) => t.count), 1);
    return (threat.count / max) * 100;
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMessage.set({ text, type });
    setTimeout(() => this.statusMessage.set(null), 4000);
  }
}
