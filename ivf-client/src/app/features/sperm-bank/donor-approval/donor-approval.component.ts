import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { SpermBankService, Donor } from '../sperm-bank.service';

@Component({
  selector: 'app-donor-approval',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './donor-approval.component.html',
  styleUrls: ['./donor-approval.component.scss'],
})
export class DonorApprovalComponent implements OnInit {
  private service = inject(SpermBankService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  success = signal('');
  donor = signal<Donor | null>(null);

  decision = 'Approved';
  rejectionReason = '';
  donorCode = '';
  notes = '';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.service.getDonors().subscribe({
        next: (donors) => {
          this.donor.set(donors.find((d) => d.id === id) || null);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  submit() {
    if (this.decision === 'Approved' && !this.donorCode) {
      this.error.set('Vui lòng nhập mã NHTT');
      return;
    }
    this.saving.set(true);
    setTimeout(() => {
      this.success.set(
        this.decision === 'Approved' ? 'Đã phê duyệt người hiến' : 'Đã từ chối người hiến',
      );
      this.saving.set(false);
      setTimeout(() => this.router.navigate(['/sperm-bank']), 1500);
    }, 800);
  }

  back() {
    this.router.navigate(['/sperm-bank']);
  }
}
