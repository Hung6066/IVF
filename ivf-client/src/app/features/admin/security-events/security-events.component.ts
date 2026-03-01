import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SecurityService } from '../../../core/services/security.service';
import { SecurityEvent } from '../../../core/models/security.model';

@Component({
  selector: 'app-security-events',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './security-events.component.html',
  styleUrls: ['./security-events.component.scss'],
})
export class SecurityEventsComponent implements OnInit {
  events = signal<SecurityEvent[]>([]);
  filteredEvents = signal<SecurityEvent[]>([]);
  loading = signal(false);
  selectedEvent = signal<SecurityEvent | null>(null);

  // Filters
  filterSeverity = '';
  filterType = '';
  filterIp = '';
  filterUser = '';
  filterBlocked = '';
  eventCount = 100;

  // View mode
  viewMode: 'all' | 'high-severity' = 'all';

  constructor(private securityService: SecurityService) {}

  ngOnInit() {
    this.loadEvents();
  }

  loadEvents() {
    this.loading.set(true);
    if (this.viewMode === 'high-severity') {
      this.securityService.getHighSeverityEvents(48).subscribe({
        next: (events) => {
          this.events.set(events);
          this.applyFilters();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else {
      this.securityService.getRecentEvents(this.eventCount).subscribe({
        next: (events) => {
          this.events.set(events);
          this.applyFilters();
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  switchView(mode: 'all' | 'high-severity') {
    this.viewMode = mode;
    this.loadEvents();
  }

  applyFilters() {
    let result = this.events();

    if (this.filterSeverity) {
      result = result.filter((e) => e.severity === this.filterSeverity);
    }
    if (this.filterType) {
      result = result.filter((e) => e.eventType.includes(this.filterType));
    }
    if (this.filterIp) {
      result = result.filter((e) => e.ipAddress?.includes(this.filterIp));
    }
    if (this.filterUser) {
      result = result.filter((e) =>
        e.username?.toLowerCase().includes(this.filterUser.toLowerCase()),
      );
    }
    if (this.filterBlocked === 'blocked') {
      result = result.filter((e) => e.isBlocked);
    } else if (this.filterBlocked === 'allowed') {
      result = result.filter((e) => !e.isBlocked);
    }

    this.filteredEvents.set(result);
  }

  clearFilters() {
    this.filterSeverity = '';
    this.filterType = '';
    this.filterIp = '';
    this.filterUser = '';
    this.filterBlocked = '';
    this.applyFilters();
  }

  showDetail(event: SecurityEvent) {
    this.selectedEvent.set(event);
  }

  closeDetail() {
    this.selectedEvent.set(null);
  }

  // Unique event type prefixes for filter dropdown
  getEventCategories(): string[] {
    return ['AUTH', 'AUTHZ', 'ZT', 'THREAT', 'SESSION', 'DEVICE', 'DATA', 'API'];
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

  parseJson(json: string | null): Record<string, unknown> | null {
    if (!json) return null;
    try {
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  getObjectKeys(obj: Record<string, unknown>): string[] {
    return Object.keys(obj);
  }
}
