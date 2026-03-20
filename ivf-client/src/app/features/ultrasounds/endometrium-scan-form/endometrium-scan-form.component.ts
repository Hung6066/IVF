import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { UltrasoundService } from '../../../core/services/ultrasound.service';

@Component({
  selector: 'app-endometrium-scan-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './endometrium-scan-form.component.html',
  styleUrls: ['./endometrium-scan-form.component.scss'],
})
export class EndometriumScanFormComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(UltrasoundService);
  private router = inject(Router);

  cycleId = this.route.snapshot.paramMap.get('cycleId') ?? '';
  saving = signal(false);
  error = signal('');

  form = {
    cycleId: '',
    examDate: new Date().toISOString().split('T')[0],
    endometriumThickness: null as number | null,
    leftOvaryCount: null as number | null,
    rightOvaryCount: null as number | null,
    leftFollicles: '',
    rightFollicles: '',
    findings: '',
  };

  ngOnInit() {
    this.form.cycleId = this.cycleId;
  }

  save() {
    if (!this.form.examDate) {
      this.error.set('Vui lòng chọn ngày siêu âm');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    const payload = {
      ...this.form,
      endometriumThickness: this.form.endometriumThickness ?? undefined,
      leftOvaryCount: this.form.leftOvaryCount ?? undefined,
      rightOvaryCount: this.form.rightOvaryCount ?? undefined,
      leftFollicles: this.form.leftFollicles || undefined,
      rightFollicles: this.form.rightFollicles || undefined,
      findings: this.form.findings || undefined,
    };
    this.service.createUltrasound(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/ultrasounds']);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi lưu kết quả siêu âm');
        this.saving.set(false);
      },
    });
  }

  back() {
    this.router.navigate(['/ultrasounds']);
  }
}
