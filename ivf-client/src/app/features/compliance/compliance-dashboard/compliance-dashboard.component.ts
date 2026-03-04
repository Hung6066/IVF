import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ComplianceService } from '../../../core/services/compliance.service';
import {
  ComplianceHealthDashboard,
  SecurityTrend,
  AuditReadiness,
  AiPerformanceDashboard,
} from '../../../core/models/compliance.model';

@Component({
  selector: 'app-compliance-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './compliance-dashboard.component.html',
  styleUrls: ['./compliance-dashboard.component.scss'],
})
export class ComplianceDashboardComponent implements OnInit {
  private complianceService = inject(ComplianceService);

  health = signal<ComplianceHealthDashboard | null>(null);
  securityTrends = signal<SecurityTrend[]>([]);
  auditReadiness = signal<AuditReadiness | null>(null);
  aiPerformance = signal<AiPerformanceDashboard | null>(null);
  loading = signal(true);
  activeTab = signal<'overview' | 'security' | 'gdpr' | 'ai' | 'audit'>('overview');

  ngOnInit() {
    this.loadAll();
  }

  loadAll() {
    this.loading.set(true);
    this.complianceService.getHealthDashboard().subscribe({
      next: (data) => {
        this.health.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.complianceService.getSecurityTrends(6).subscribe({
      next: (data) => this.securityTrends.set(data),
    });

    this.complianceService.getAuditReadiness().subscribe({
      next: (data) => this.auditReadiness.set(data),
    });

    this.complianceService.getAiPerformance().subscribe({
      next: (data) => this.aiPerformance.set(data),
    });
  }

  getHealthColor(): string {
    const score = this.health()?.overallHealthScore ?? 0;
    if (score >= 90) return '#10b981';
    if (score >= 70) return '#f59e0b';
    return '#ef4444';
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'Healthy':
        return '✅';
      case 'Warning':
        return '⚠️';
      case 'Critical':
        return '🚨';
      default:
        return '❓';
    }
  }

  getAlertIcon(severity: string): string {
    switch (severity) {
      case 'Critical':
        return '🔴';
      case 'High':
        return '🟠';
      case 'Medium':
        return '🟡';
      case 'Low':
        return '🔵';
      default:
        return '⚪';
    }
  }
}
