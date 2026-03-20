import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PregnancyService, PregnancyDto } from '../../../core/services/pregnancy.service';

@Component({
  selector: 'app-pregnancy-prenatal',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './pregnancy-prenatal.component.html',
  styleUrls: ['./pregnancy-prenatal.component.scss'],
})
export class PregnancyPrenatalComponent implements OnInit {
  private service = inject(PregnancyService);
  private route = inject(ActivatedRoute);

  cycleId = '';
  pregnancy = signal<PregnancyDto | null>(null);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  visitNumber = signal(1);

  form = {
    examDate: new Date().toISOString().slice(0, 10),
    gestationalSacs: 1,
    fetalHeartbeats: 1,
    dueDate: '',
    ultrasoundFindings: '',
    notes: '',
    issuedMaternityBook: false, // P6.06
    maternityBookIssuedDate: new Date().toISOString().slice(0, 10),
  };

  ngOnInit() {
    this.cycleId = this.route.snapshot.paramMap.get('cycleId') || '';
    const visit = this.route.snapshot.queryParamMap.get('visit');
    if (visit) this.visitNumber.set(+visit);
    if (this.cycleId) {
      this.service
        .getByCycle(this.cycleId)
        .subscribe({ next: (d) => this.pregnancy.set(d), error: () => {} });
    }
  }

  submit() {
    this.saving.set(true);
    this.error.set('');
    this.service
      .recordPrenatalExam(this.cycleId, {
        examDate: this.form.examDate,
        gestationalSacs: this.form.gestationalSacs,
        fetalHeartbeats: this.form.fetalHeartbeats,
        dueDate: this.form.dueDate || undefined,
        ultrasoundFindings: this.form.ultrasoundFindings || undefined,
        notes: this.form.notes || undefined,
      })
      .subscribe({
        next: (data) => {
          this.pregnancy.set(data);
          this.saving.set(false);
          this.successMsg.set('Đã lưu kết quả khám thai 7 tuần');
          setTimeout(() => this.successMsg.set(''), 3000);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi lưu khám thai');
          this.saving.set(false);
        },
      });
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
}
