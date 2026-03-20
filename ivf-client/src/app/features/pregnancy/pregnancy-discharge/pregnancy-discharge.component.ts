import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PregnancyService, PregnancyDto } from '../../../core/services/pregnancy.service';

@Component({
  selector: 'app-pregnancy-discharge',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './pregnancy-discharge.component.html',
  styleUrls: ['./pregnancy-discharge.component.scss'],
})
export class PregnancyDischargeComponent implements OnInit {
  private service = inject(PregnancyService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  cycleId = '';
  pregnancy = signal<PregnancyDto | null>(null);
  saving = signal(false);
  error = signal('');

  form = {
    outcome: 'Ongoing',
    dischargeDate: new Date().toISOString().slice(0, 10),
    notes: '',
  };

  outcomes = [
    'Ongoing',
    'LiveBirth',
    'Miscarriage',
    'Ectopic',
    'BiochemicalPregnancy',
    'TransferredOut',
  ];

  ngOnInit() {
    this.cycleId = this.route.snapshot.paramMap.get('cycleId') || '';
    if (this.cycleId) {
      this.service
        .getByCycle(this.cycleId)
        .subscribe({ next: (d) => this.pregnancy.set(d), error: () => {} });
    }
  }

  submit() {
    if (!confirm('X\u00e1c nh\u1eadn xu\u1ea5t vi\u1ec7n / k\u1ebft th\u00fac chu k\u1ef3 IVF?'))
      return;
    this.saving.set(true);
    this.error.set('');
    const outcomeNote = this.form.notes
      ? `${this.form.outcome}: ${this.form.notes}`
      : this.form.outcome;
    this.service.discharge(this.cycleId, outcomeNote, this.form.dischargeDate).subscribe({
      next: () => this.router.navigate(['/pregnancy', this.cycleId, 'result']),
      error: (err) => {
        this.error.set(err.error?.message || 'L\u1ed7i xu\u1ea5t vi\u1ec7n');
        this.saving.set(false);
      },
    });
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '\u2014';
  }
}
