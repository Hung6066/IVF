import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { EggBankService } from '../../../core/services/egg-bank.service';
import { EggDonorDto } from '../../../core/models/egg-donor.models';

@Component({
  selector: 'app-egg-donor-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './egg-donor-list.component.html',
  styleUrls: ['./egg-donor-list.component.scss'],
})
export class EggDonorListComponent implements OnInit {
  private service = inject(EggBankService);

  donors = signal<EggDonorDto[]>([]);
  total = signal(0);
  loading = signal(false);
  searchQuery = '';
  page = 1;

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service.searchDonors(this.searchQuery || undefined, this.page).subscribe({
      next: (res) => {
        this.donors.set(res.items);
        this.total.set(res.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  search() {
    this.page = 1;
    this.load();
  }
  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
}
