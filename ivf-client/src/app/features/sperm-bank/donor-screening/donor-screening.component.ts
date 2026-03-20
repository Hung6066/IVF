import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { SpermBankService, Donor } from '../sperm-bank.service';

@Component({
  selector: 'app-donor-screening',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './donor-screening.component.html',
  styleUrls: ['./donor-screening.component.scss'],
})
export class DonorScreeningComponent implements OnInit {
  private service = inject(SpermBankService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  success = signal('');
  donor = signal<Donor | null>(null);

  screening = {
    hivRapidResult: '',
    hivConfirmatoryResult: '', // P8.04: Rapid 15min / Confirmatory 2h
    hivRapidReportedAt: new Date().toISOString().slice(0, 16), // datetime of rapid report
    hivRetestDue: '', // P8.08: 3-month retest date
    hbsAgResult: '',
    hcvResult: '',
    vdrlResult: '',
    cmvResult: '',
    bloodType: '',
    rh: '+',
    height: undefined as number | undefined,
    weight: undefined as number | undefined,
    physicalExam: 'Bình thường',
    notes: '',
    screeningDate: new Date().toISOString().slice(0, 10),
  };
  labResults = ['', 'Âm tính', 'Dương tính', 'Chờ kết quả'];

  get computedRetestDue(): string {
    if (!this.screening.screeningDate) return '';
    const d = new Date(this.screening.screeningDate);
    d.setMonth(d.getMonth() + 3);
    return d.toISOString().slice(0, 10);
  }

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

  save() {
    this.saving.set(true);
    // POST to backend — for now just simulate
    setTimeout(() => {
      this.success.set('Đã lưu kết quả sàng lọc');
      this.saving.set(false);
    }, 800);
  }

  back() {
    this.router.navigate(['/sperm-bank']);
  }
}
