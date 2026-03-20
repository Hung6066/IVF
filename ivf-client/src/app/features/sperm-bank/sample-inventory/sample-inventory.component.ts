import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { SpermBankService, Sample, Donor } from '../sperm-bank.service';

@Component({
  selector: 'app-sample-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sample-inventory.component.html',
  styleUrls: ['./sample-inventory.component.scss'],
})
export class SampleInventoryComponent implements OnInit {
  private service = inject(SpermBankService);

  loading = signal(true);
  samples = signal<Sample[]>([]);
  donors = signal<Donor[]>([]);
  filterStatus = 'all';

  ngOnInit() {
    this.service.getSamples().subscribe({
      next: (s) => {
        this.samples.set(s);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.service.getDonors().subscribe({ next: (d) => this.donors.set(d) });
  }

  get filteredSamples(): Sample[] {
    if (this.filterStatus === 'all') return this.samples();
    return this.samples().filter((s) => s.status === this.filterStatus);
  }

  countByStatus(status: string): number {
    return this.samples().filter((s) => s.status === status).length;
  }

  getDonorCode(donorId: string): string {
    return this.donors().find((d) => d.id === donorId)?.code ?? donorId;
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Available':
        return 'bg-green-100 text-green-700';
      case 'Quarantine':
        return 'bg-yellow-100 text-yellow-700';
      case 'Used':
        return 'bg-gray-100 text-gray-600';
      case 'Discarded':
        return 'bg-red-100 text-red-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  }
}
