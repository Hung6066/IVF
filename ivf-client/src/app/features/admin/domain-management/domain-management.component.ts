import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomainManagementService } from '../../../core/services/domain-management.service';
import { TenantDomainInfo } from '../../../core/models/domain-management.model';

@Component({
  selector: 'app-domain-management',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './domain-management.component.html',
  styleUrls: ['./domain-management.component.scss'],
})
export class DomainManagementComponent implements OnInit {
  domains = signal<TenantDomainInfo[]>([]);
  loading = signal(false);
  syncing = signal(false);
  syncMessage = signal('');
  syncSuccess = signal<boolean | null>(null);
  activeTab = signal<'domains' | 'preview' | 'current'>('domains');
  caddyfilePreview = signal('');
  currentConfig = signal('');
  previewLoading = signal(false);
  configLoading = signal(false);

  constructor(private domainService: DomainManagementService) {}

  ngOnInit(): void {
    this.loadDomains();
  }

  loadDomains(): void {
    this.loading.set(true);
    this.domainService.getDomains().subscribe({
      next: (domains) => {
        this.domains.set(domains);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  syncConfig(): void {
    this.syncing.set(true);
    this.syncMessage.set('');
    this.syncSuccess.set(null);
    this.domainService.syncConfig().subscribe({
      next: (result) => {
        this.syncMessage.set(result.message);
        this.syncSuccess.set(result.success);
        this.syncing.set(false);
        if (result.success) {
          this.loadDomains();
        }
      },
      error: (err) => {
        this.syncMessage.set(err.error || 'Lỗi đồng bộ Caddy');
        this.syncSuccess.set(false);
        this.syncing.set(false);
      },
    });
  }

  loadPreview(): void {
    this.activeTab.set('preview');
    this.previewLoading.set(true);
    this.domainService.getPreview().subscribe({
      next: (preview) => {
        this.caddyfilePreview.set(preview);
        this.previewLoading.set(false);
      },
      error: () => this.previewLoading.set(false),
    });
  }

  loadCurrentConfig(): void {
    this.activeTab.set('current');
    this.configLoading.set(true);
    this.domainService.getCurrentConfig().subscribe({
      next: (config) => {
        this.currentConfig.set(config);
        this.configLoading.set(false);
      },
      error: () => {
        this.currentConfig.set('Không thể kết nối Caddy Admin API');
        this.configLoading.set(false);
      },
    });
  }

  switchTab(tab: 'domains' | 'preview' | 'current'): void {
    if (tab === 'preview') {
      this.loadPreview();
    } else if (tab === 'current') {
      this.loadCurrentConfig();
    } else {
      this.activeTab.set('domains');
    }
  }

  getActiveDomainCount(): number {
    return this.domains().filter((d) => d.isActive && d.subdomain).length;
  }

  getCustomDomainCount(): number {
    return this.domains().filter((d) => d.customDomain).length;
  }
}
