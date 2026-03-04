import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComplianceService } from '../../../core/services/compliance.service';
import { AssetInventory } from '../../../core/models/compliance.model';

@Component({
  selector: 'app-asset-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './asset-inventory.component.html',
  styleUrls: ['./asset-inventory.component.scss'],
})
export class AssetInventoryComponent implements OnInit {
  private complianceService = inject(ComplianceService);

  assets = signal<AssetInventory[]>([]);
  totalCount = signal(0);
  loading = signal(true);

  filterType = '';
  filterClassification = '';
  filterOwner = '';
  page = 1;

  showModal = false;
  editingId = '';
  form = {
    assetName: '',
    assetType: 'Server',
    classification: 'Confidential',
    owner: '',
    criticalityLevel: 'Medium',
    containsPhi: false,
    containsPii: false,
    department: '',
    location: '',
    environment: '',
  };

  assetTypes = [
    'Server',
    'Database',
    'Application',
    'Network',
    'Endpoint',
    'Storage',
    'Cloud',
    'IoT',
    'Other',
  ];
  classifications = ['Public', 'Internal', 'Confidential', 'Restricted'];

  ngOnInit() {
    this.loadAssets();
  }

  loadAssets() {
    this.loading.set(true);
    this.complianceService
      .getAssets({
        type: this.filterType || undefined,
        classification: this.filterClassification || undefined,
        owner: this.filterOwner || undefined,
        page: this.page,
      })
      .subscribe({
        next: (result) => {
          this.assets.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onFilterChange() {
    this.page = 1;
    this.loadAssets();
  }

  openCreate() {
    this.editingId = '';
    this.form = {
      assetName: '',
      assetType: 'Server',
      classification: 'Confidential',
      owner: '',
      criticalityLevel: 'Medium',
      containsPhi: false,
      containsPii: false,
      department: '',
      location: '',
      environment: '',
    };
    this.showModal = true;
  }

  openEdit(asset: AssetInventory) {
    this.editingId = asset.id;
    this.form = {
      assetName: asset.assetName,
      assetType: asset.assetType,
      classification: asset.classification,
      owner: asset.owner,
      criticalityLevel: asset.criticalityLevel,
      containsPhi: asset.containsPhi,
      containsPii: asset.containsPii,
      department: asset.department || '',
      location: asset.location || '',
      environment: asset.environment || '',
    };
    this.showModal = true;
  }

  submitForm() {
    if (!this.form.assetName || !this.form.owner) return;
    const obs = this.editingId
      ? this.complianceService.updateAsset(this.editingId, this.form)
      : this.complianceService.createAsset(this.form);
    obs.subscribe({
      next: () => {
        this.showModal = false;
        this.loadAssets();
      },
    });
  }

  deleteAsset(asset: AssetInventory) {
    if (!confirm(`Xoá tài sản "${asset.assetName}"?`)) return;
    this.complianceService.deleteAsset(asset.id).subscribe(() => this.loadAssets());
  }

  getClassificationIcon(cls: string): string {
    const icons: Record<string, string> = {
      Public: '🟢',
      Internal: '🔵',
      Confidential: '🟠',
      Restricted: '🔴',
    };
    return icons[cls] || '⚪';
  }
}
