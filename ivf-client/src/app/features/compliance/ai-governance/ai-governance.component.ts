import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComplianceService } from '../../../core/services/compliance.service';
import { AiModelVersion, AiBiasTestResult } from '../../../core/models/compliance.model';

@Component({
  selector: 'app-ai-governance',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-governance.component.html',
  styleUrls: ['./ai-governance.component.scss'],
})
export class AiGovernanceComponent implements OnInit {
  private complianceService = inject(ComplianceService);

  models = signal<AiModelVersion[]>([]);
  biasTests = signal<AiBiasTestResult[]>([]);
  totalModels = signal(0);
  totalTests = signal(0);
  loading = signal(true);

  activeTab = signal<'models' | 'bias'>('models');
  filterSystem = '';
  filterStatus = '';
  page = 1;

  modelStatuses = ['Draft', 'Testing', 'Approved', 'Deployed', 'Retired', 'Rolled Back'];

  ngOnInit() {
    this.loadModels();
    this.loadBiasTests();
  }

  loadModels() {
    this.loading.set(true);
    this.complianceService
      .getAiModels({
        system: this.filterSystem || undefined,
        status: this.filterStatus || undefined,
        page: this.page,
      })
      .subscribe({
        next: (result) => {
          this.models.set(result.items);
          this.totalModels.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  loadBiasTests() {
    this.complianceService
      .getBiasTests({
        system: this.filterSystem || undefined,
        page: 1,
        pageSize: 50,
      })
      .subscribe({
        next: (result) => {
          this.biasTests.set(result.items);
          this.totalTests.set(result.totalCount);
        },
      });
  }

  onFilterChange() {
    this.page = 1;
    this.loadModels();
    this.loadBiasTests();
  }

  formatPercent(val?: number): string {
    if (val === undefined || val === null) return '—';
    return (val * 100).toFixed(1) + '%';
  }

  getStatusIcon(status: string): string {
    const icons: Record<string, string> = {
      Draft: '📝',
      Testing: '🧪',
      Approved: '✅',
      Deployed: '🚀',
      Retired: '📦',
      'Rolled Back': '⏪',
    };
    return icons[status] || '❓';
  }
}
