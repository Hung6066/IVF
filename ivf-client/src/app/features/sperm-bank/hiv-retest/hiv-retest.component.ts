import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface HivRetestRecord {
  id: string;
  donorId: string;
  donorCode: string;
  scheduledDate: string;
  conductedDate?: string;
  result?: string;
  biometricVerified: boolean;
  notes?: string;
  status: 'Pending' | 'Completed' | 'Overdue';
}

@Component({
  selector: 'app-hiv-retest',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './hiv-retest.component.html',
  styleUrls: ['./hiv-retest.component.scss'],
})
export class HivRetestComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

  donorId = '';
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  success = signal('');
  existing = signal<HivRetestRecord | null>(null);

  form = {
    scheduledDate: '',
    conductedDate: new Date().toISOString().slice(0, 10),
    rapidResult: '',
    confirmatoryResult: '',
    biometricVerified: false,
    notes: '',
  };

  labResults = ['', 'Âm tính', 'Dương tính', 'Chờ kết quả'];

  get resultStatus(): string {
    if (!this.form.confirmatoryResult) return '';
    if (this.form.confirmatoryResult === 'Âm tính') return 'text-green-600';
    if (this.form.confirmatoryResult === 'Dương tính') return 'text-red-600';
    return 'text-yellow-600';
  }

  ngOnInit() {
    this.donorId = this.route.snapshot.paramMap.get('donorId') || '';
    // Compute scheduled date = 3 months from today (default)
    const d = new Date();
    d.setMonth(d.getMonth() + 3);
    this.form.scheduledDate = d.toISOString().slice(0, 10);
    this.loadExisting();
  }

  loadExisting() {
    this.loading.set(true);
    this.http
      .get<HivRetestRecord>(`${this.baseUrl}/spermbank/donors/${this.donorId}/hiv-retest`)
      .subscribe({
        next: (rec) => {
          this.existing.set(rec);
          if (rec.scheduledDate) this.form.scheduledDate = rec.scheduledDate.slice(0, 10);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  submit() {
    if (!this.form.biometricVerified) {
      this.error.set('Phải xác minh sinh trắc học trước khi ghi nhận kết quả');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.http
      .post(`${this.baseUrl}/spermbank/donors/${this.donorId}/hiv-retest`, {
        scheduledDate: this.form.scheduledDate,
        conductedDate: this.form.conductedDate,
        rapidResult: this.form.rapidResult,
        confirmatoryResult: this.form.confirmatoryResult,
        biometricVerified: this.form.biometricVerified,
        notes: this.form.notes,
      })
      .subscribe({
        next: () => {
          this.success.set('Đã ghi nhận kết quả XN HIV lần 2');
          this.saving.set(false);
          setTimeout(() => this.router.navigate(['/sperm-bank']), 1500);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi ghi nhận kết quả');
          this.saving.set(false);
        },
      });
  }

  back() {
    this.router.navigate(['/sperm-bank']);
  }
}
