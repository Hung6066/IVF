import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { EggBankService } from '../../../core/services/egg-bank.service';
import {
  EggDonorRecipientService,
  MatchEggDonorRequest,
} from '../../../core/services/egg-donor-recipient.service';
import { EggDonorDto } from '../../../core/models/egg-donor.models';

@Component({
  selector: 'app-egg-donor-matching',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './egg-donor-matching.component.html',
  styleUrls: ['./egg-donor-matching.component.scss'],
})
export class EggDonorMatchingComponent implements OnInit {
  private eggBankService = inject(EggBankService);
  private matchService = inject(EggDonorRecipientService);

  donors = signal<EggDonorDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  showMatchForm = false;
  matchForm = { eggDonorId: '', recipientCoupleId: '', matchedByUserId: '', notes: '' };

  ngOnInit() {
    this.loading.set(true);
    this.eggBankService.searchDonors(undefined, 1, 100).subscribe({
      next: (res) => {
        this.donors.set(res.items.filter((d) => d.status === 'Active'));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openMatch(donorId: string) {
    this.matchForm = { eggDonorId: donorId, recipientCoupleId: '', matchedByUserId: '', notes: '' };
    this.showMatchForm = true;
  }

  submitMatch() {
    if (!this.matchForm.recipientCoupleId || !this.matchForm.matchedByUserId) {
      this.error.set('Vui lòng nhập đầy đủ thông tin');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.matchService.match(this.matchForm).subscribe({
      next: () => {
        this.showMatchForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã ghép cặp NCT - Người nhận thành công');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi ghép cặp');
        this.saving.set(false);
      },
    });
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
}
