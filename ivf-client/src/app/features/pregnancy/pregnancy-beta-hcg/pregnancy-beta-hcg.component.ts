import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  PregnancyService,
  PregnancyDto,
  BetaHcgResultDto,
} from '../../../core/services/pregnancy.service';

@Component({
  selector: 'app-pregnancy-beta-hcg',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './pregnancy-beta-hcg.component.html',
  styleUrls: ['./pregnancy-beta-hcg.component.scss'],
})
export class PregnancyBetaHcgComponent implements OnInit {
  private service = inject(PregnancyService);
  private route = inject(ActivatedRoute);

  cycleId = '';
  pregnancy = signal<PregnancyDto | null>(null);
  results = signal<BetaHcgResultDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');
  showForm = false;

  form = {
    betaHcg: 0,
    testDate: new Date().toISOString().slice(0, 10),
    notes: '',
  };

  ngOnInit() {
    this.cycleId = this.route.snapshot.paramMap.get('cycleId') || '';
    if (this.cycleId) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getByCycle(this.cycleId).subscribe({
      next: (data) => {
        this.pregnancy.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.service.getBetaHcgResults(this.cycleId).subscribe({
      next: (data) => this.results.set(data),
      error: () => {},
    });
  }

  submit() {
    if (this.form.betaHcg < 0) {
      this.error.set('Vui lòng nhập giá trị Beta HCG');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.service
      .recordBetaHcg(
        this.cycleId,
        this.form.betaHcg,
        this.form.testDate,
        this.form.notes || undefined,
      )
      .subscribe({
        next: (data) => {
          this.pregnancy.set(data);
          this.showForm = false;
          this.saving.set(false);
          this.successMsg.set('Đã lưu kết quả Beta HCG');
          this.load();
          setTimeout(() => this.successMsg.set(''), 3000);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi ghi nhận');
          this.saving.set(false);
        },
      });
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
}
