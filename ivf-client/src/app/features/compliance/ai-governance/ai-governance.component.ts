import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComplianceService } from '../../../core/services/compliance.service';
import {
  AiModelVersion,
  AiBiasTestResult,
  CreateModelVersionRequest,
  CreateBiasTestRequest,
} from '../../../core/models/compliance.model';

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

  // Create Model modal
  showModelModal = false;
  modelForm: CreateModelVersionRequest = {
    aiSystemName: '',
    modelVersion: '',
    changeDescription: '',
    configurationJson: '{}',
    thresholdsJson: '{}',
  };

  // Create Bias Test modal
  showBiasModal = false;
  biasForm: CreateBiasTestRequest = {
    aiSystemName: '',
    testType: 'FairnessParity',
    protectedAttribute: '',
    protectedGroupValue: '',
    sampleSize: 1000,
    truePositives: 0,
    falsePositives: 0,
    trueNegatives: 0,
    falseNegatives: 0,
    baselineFpr: 0,
    baselineFnr: 0,
    testPeriodStart: new Date().toISOString().slice(0, 10),
    testPeriodEnd: new Date().toISOString().slice(0, 10),
    fairnessThreshold: 0.25,
  };

  biasTestTypes = [
    'FairnessParity',
    'EqualOpportunity',
    'PredictiveParity',
    'CalibrationTest',
    'DisparateImpact',
  ];

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
      })
      .subscribe({
        next: (result) => {
          this.biasTests.set(result);
          this.totalTests.set(result.length);
        },
      });
  }

  onFilterChange() {
    this.page = 1;
    this.loadModels();
    this.loadBiasTests();
  }

  // ─── Model CRUD ───

  openModelModal() {
    this.modelForm = {
      aiSystemName: '',
      modelVersion: '',
      changeDescription: '',
      configurationJson: '{}',
      thresholdsJson: '{}',
    };
    this.showModelModal = true;
  }

  submitModel() {
    if (!this.modelForm.aiSystemName || !this.modelForm.modelVersion) return;
    this.complianceService.createAiModel(this.modelForm).subscribe({
      next: () => {
        this.showModelModal = false;
        this.loadModels();
      },
    });
  }

  deleteModel(id: string) {
    if (!confirm('Xóa model version này?')) return;
    this.complianceService.deleteAiModel(id).subscribe({
      next: () => this.loadModels(),
    });
  }

  // ─── Bias Test CRUD ───

  openBiasModal() {
    this.biasForm = {
      aiSystemName: '',
      testType: 'FairnessParity',
      protectedAttribute: '',
      protectedGroupValue: '',
      sampleSize: 1000,
      truePositives: 0,
      falsePositives: 0,
      trueNegatives: 0,
      falseNegatives: 0,
      baselineFpr: 0,
      baselineFnr: 0,
      testPeriodStart: new Date().toISOString().slice(0, 10),
      testPeriodEnd: new Date().toISOString().slice(0, 10),
      fairnessThreshold: 0.25,
    };
    this.showBiasModal = true;
  }

  submitBiasTest() {
    if (!this.biasForm.aiSystemName || !this.biasForm.protectedAttribute) return;
    this.complianceService.createBiasTest(this.biasForm).subscribe({
      next: () => {
        this.showBiasModal = false;
        this.loadBiasTests();
      },
    });
  }

  deleteBiasTest(id: string) {
    if (!confirm('Xóa kết quả bias test này?')) return;
    this.complianceService.deleteBiasTest(id).subscribe({
      next: () => this.loadBiasTests(),
    });
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
