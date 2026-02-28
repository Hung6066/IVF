import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ZeroTrustService } from '../../../core/services/zerotrust.service';
import {
  ZTPolicyResponse,
  ZTAccessDecision,
  UpdateZTPolicyRequest,
} from '../../../core/models/zerotrust.model';

@Component({
  selector: 'app-zero-trust-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './zero-trust-dashboard.component.html',
  styleUrls: ['./zero-trust-dashboard.component.scss'],
})
export class ZeroTrustDashboardComponent implements OnInit {
  policies = signal<ZTPolicyResponse[]>([]);
  accessDecision = signal<ZTAccessDecision | null>(null);
  loading = signal(false);
  editingPolicy = signal(false);
  statusMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  accessCheckAction = '';
  editForm: UpdateZTPolicyRequest = {
    action: '',
    requiredAuthLevel: 'Standard',
    maxAllowedRisk: 'Medium',
    requireTrustedDevice: false,
    requireFreshSession: false,
    blockAnomaly: false,
    requireGeoFence: false,
    allowedCountries: null,
    blockVpnTor: false,
    allowBreakGlassOverride: false,
    updatedBy: '',
  };

  constructor(private ztService: ZeroTrustService) {}

  ngOnInit() {
    this.loadPolicies();
  }

  loadPolicies() {
    this.ztService.getAllPolicies().subscribe({
      next: (policies) => this.policies.set(policies),
      error: () => this.showStatus('Không thể tải chính sách', 'error'),
    });
  }

  activePolicies(): number {
    return this.policies().filter((p) => p.isActive).length;
  }

  vpnBlockCount(): number {
    return this.policies().filter((p) => p.blockVpnTor).length;
  }

  trustedDeviceCount(): number {
    return this.policies().filter((p) => p.requireTrustedDevice).length;
  }

  editPolicy(policy: ZTPolicyResponse) {
    this.editForm = {
      action: policy.action,
      requiredAuthLevel: policy.requiredAuthLevel,
      maxAllowedRisk: policy.maxAllowedRisk,
      requireTrustedDevice: policy.requireTrustedDevice,
      requireFreshSession: policy.requireFreshSession,
      blockAnomaly: policy.blockAnomaly,
      requireGeoFence: policy.requireGeoFence,
      allowedCountries: policy.allowedCountries,
      blockVpnTor: policy.blockVpnTor,
      allowBreakGlassOverride: policy.allowBreakGlassOverride,
      updatedBy: '',
    };
    this.editingPolicy.set(true);
  }

  savePolicy() {
    this.loading.set(true);
    this.ztService.updatePolicy(this.editForm).subscribe({
      next: () => {
        this.showStatus('Chính sách đã được cập nhật', 'success');
        this.editingPolicy.set(false);
        this.loadPolicies();
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi cập nhật chính sách', 'error');
        this.loading.set(false);
      },
    });
  }

  testAccess() {
    if (!this.accessCheckAction) return;
    this.loading.set(true);
    this.ztService.checkAccess({ action: this.accessCheckAction }).subscribe({
      next: (decision) => {
        this.accessDecision.set(decision);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi kiểm tra truy cập', 'error');
        this.loading.set(false);
      },
    });
  }

  getAuthLevelClass(level: string): string {
    switch (level) {
      case 'None':
        return 'badge-none';
      case 'Session':
        return 'badge-session';
      case 'Password':
        return 'badge-password';
      case 'MFA':
        return 'badge-mfa';
      case 'FreshSession':
        return 'badge-freshsession';
      case 'Biometric':
        return 'badge-biometric';
      default:
        return 'badge-none';
    }
  }

  getRiskClass(risk: string): string {
    switch (risk) {
      case 'Low':
        return 'badge-low';
      case 'Medium':
        return 'badge-medium';
      case 'High':
        return 'badge-high';
      case 'Critical':
        return 'badge-critical';
      default:
        return 'badge-secondary';
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
