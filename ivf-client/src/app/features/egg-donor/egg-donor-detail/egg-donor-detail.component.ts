import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { EggBankService } from '../../../core/services/egg-bank.service';
import { EggDonorDto, OocyteSampleDto } from '../../../core/models/egg-donor.models';

@Component({
  selector: 'app-egg-donor-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './egg-donor-detail.component.html',
  styleUrls: ['./egg-donor-detail.component.scss'],
})
export class EggDonorDetailComponent implements OnInit {
  private service = inject(EggBankService);
  private route = inject(ActivatedRoute);

  id = '';
  donor = signal<EggDonorDto | null>(null);
  samples = signal<OocyteSampleDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');
  activeTab = 'profile';

  showProfileForm = false;
  profileForm = {
    bloodType: '',
    height: 0,
    weight: 0,
    ethnicity: '',
    amhLevel: 0,
    antralFollicleCount: 0,
  };

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id') || '';
    if (this.id) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getDonorById(this.id).subscribe({
      next: (d) => {
        this.donor.set(d);
        this.profileForm = {
          bloodType: d.bloodType || '',
          height: d.height || 0,
          weight: d.weight || 0,
          ethnicity: d.ethnicity || '',
          amhLevel: d.amhLevel || 0,
          antralFollicleCount: d.antralFollicleCount || 0,
        };
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Không tìm thấy NCT');
        this.loading.set(false);
      },
    });
    this.service.getSamplesByDonor(this.id).subscribe({
      next: (s) => this.samples.set(s),
      error: () => {},
    });
  }

  updateProfile() {
    this.saving.set(true);
    this.service.updateDonorProfile(this.id, this.profileForm).subscribe({
      next: (d) => {
        this.donor.set(d);
        this.showProfileForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã cập nhật hồ sơ');
        setTimeout(() => this.successMsg.set(''), 3000);
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
