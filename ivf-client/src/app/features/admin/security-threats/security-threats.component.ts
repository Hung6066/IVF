import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SecurityService } from '../../../core/services/security.service';
import { ThreatAssessment, IpIntelligence, DeviceTrust } from '../../../core/models/security.model';

@Component({
  selector: 'app-security-threats',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './security-threats.component.html',
  styleUrls: ['./security-threats.component.scss'],
})
export class SecurityThreatsComponent {
  loading = signal(false);
  activeTab: 'assess' | 'ip' | 'device' = 'assess';
  statusMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  // Threat Assessment
  assessForm = { ipAddress: '', username: '', userAgent: '', country: '', requestPath: '' };
  assessment = signal<ThreatAssessment | null>(null);

  // IP Intelligence
  ipLookup = '';
  ipResult = signal<IpIntelligence | null>(null);

  // Device Trust
  deviceUserId = '';
  deviceFingerprint = '';
  deviceResult = signal<DeviceTrust | null>(null);

  constructor(private securityService: SecurityService) {}

  runAssessment() {
    if (!this.assessForm.ipAddress) return;
    this.loading.set(true);
    this.securityService.assessThreat(this.assessForm).subscribe({
      next: (result) => {
        this.assessment.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi đánh giá mối đe dọa', 'error');
        this.loading.set(false);
      },
    });
  }

  checkIp() {
    if (!this.ipLookup.trim()) return;
    this.loading.set(true);
    this.securityService.checkIpIntelligence(this.ipLookup.trim()).subscribe({
      next: (result) => {
        this.ipResult.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi kiểm tra IP', 'error');
        this.loading.set(false);
      },
    });
  }

  checkDevice() {
    if (!this.deviceUserId.trim() || !this.deviceFingerprint.trim()) return;
    this.loading.set(true);
    this.securityService
      .checkDeviceTrust(this.deviceUserId.trim(), this.deviceFingerprint.trim())
      .subscribe({
        next: (result) => {
          this.deviceResult.set(result);
          this.loading.set(false);
        },
        error: () => {
          this.showStatus('Lỗi kiểm tra thiết bị', 'error');
          this.loading.set(false);
        },
      });
  }

  getRiskClass(level: string): string {
    switch (level) {
      case 'Critical':
        return 'risk-critical';
      case 'High':
        return 'risk-high';
      case 'Medium':
        return 'risk-medium';
      case 'Low':
        return 'risk-low';
      default:
        return 'risk-none';
    }
  }

  getRiskBarWidth(score: number): number {
    return Math.min(score, 100);
  }

  getTrustClass(level: string): string {
    switch (level) {
      case 'FullyManaged':
        return 'trust-full';
      case 'Registered':
        return 'trust-registered';
      case 'Known':
        return 'trust-known';
      default:
        return 'trust-unknown';
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMessage.set({ text, type });
    setTimeout(() => this.statusMessage.set(null), 4000);
  }
}
