import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { EggBankService } from '../../../core/services/egg-bank.service';
import { OocyteSampleDto } from '../../../core/models/egg-donor.models';

@Component({
  selector: 'app-egg-donor-samples',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './egg-donor-samples.component.html',
  styleUrls: ['./egg-donor-samples.component.scss'],
})
export class EggDonorSamplesComponent implements OnInit {
  private service = inject(EggBankService);
  private route = inject(ActivatedRoute);

  donorId = '';
  samples = signal<OocyteSampleDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  showCreateForm = false;
  createForm = { collectionDate: new Date().toISOString().slice(0, 10) };

  showQualityForm = false;
  qualitySampleId = '';
  qualityForm = {
    totalOocytes: 0,
    matureOocytes: 0,
    immatureOocytes: 0,
    degeneratedOocytes: 0,
    notes: '',
  };

  showVitrifyForm = false;
  vitrifySampleId = '';
  vitrifyForm = { count: 0, freezeDate: new Date().toISOString().slice(0, 10) };

  ngOnInit() {
    this.donorId = this.route.snapshot.paramMap.get('id') || '';
    if (this.donorId) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getSamplesByDonor(this.donorId).subscribe({
      next: (s) => {
        this.samples.set(s);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  createSample() {
    this.saving.set(true);
    this.service
      .createSample({ donorId: this.donorId, collectionDate: this.createForm.collectionDate })
      .subscribe({
        next: () => {
          this.showCreateForm = false;
          this.saving.set(false);
          this.successMsg.set('Đã tạo mẫu');
          this.load();
          setTimeout(() => this.successMsg.set(''), 3000);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi');
          this.saving.set(false);
        },
      });
  }

  openQuality(sampleId: string) {
    this.qualitySampleId = sampleId;
    this.qualityForm = {
      totalOocytes: 0,
      matureOocytes: 0,
      immatureOocytes: 0,
      degeneratedOocytes: 0,
      notes: '',
    };
    this.showQualityForm = true;
  }

  saveQuality() {
    this.saving.set(true);
    this.service.recordQuality(this.qualitySampleId, this.qualityForm).subscribe({
      next: () => {
        this.showQualityForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã ghi nhận chất lượng');
        this.load();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  openVitrify(sampleId: string) {
    this.vitrifySampleId = sampleId;
    this.vitrifyForm = { count: 0, freezeDate: new Date().toISOString().slice(0, 10) };
    this.showVitrifyForm = true;
  }

  saveVitrify() {
    this.saving.set(true);
    this.service.vitrifySample(this.vitrifySampleId, this.vitrifyForm).subscribe({
      next: () => {
        this.showVitrifyForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã trữ lạnh noãn');
        this.load();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
}
