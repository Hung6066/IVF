import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-semen-analysis-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent, CycleSearchComponent],
  templateUrl: './semen-analysis-form.component.html',
  styleUrls: ['./semen-analysis-form.component.scss'],
})
export class SemenAnalysisFormComponent {
  private http = inject(HttpClient);
  private router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

  saving = signal(false);
  error = signal('');
  success = signal('');

  form = {
    patientId: '',
    cycleId: '',
    analysisDate: new Date().toISOString().slice(0, 10),
    volume: undefined as number | undefined,
    appearance: 'Bình thường',
    liquefaction: 'Hoàn toàn',
    ph: undefined as number | undefined,
    concentration: undefined as number | undefined,
    totalCount: undefined as number | undefined,
    progressiveMotility: undefined as number | undefined,
    nonProgressiveMotility: undefined as number | undefined,
    immotile: undefined as number | undefined,
    normalMorphology: undefined as number | undefined,
    vitality: undefined as number | undefined,
    notes: '',
  };

  appearanceOptions = ['Bình thường', 'Xám nhạt', 'Vàng', 'Đục nhiều'];
  liquefactionOptions = ['Hoàn toàn', 'Không hoàn toàn', 'Không liquefied'];

  save() {
    if (!this.form.patientId) {
      this.error.set('Vui lòng nhập mã bệnh nhân');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.http.post(`${this.baseUrl}/andrology/semen-analysis`, this.form).subscribe({
      next: () => {
        this.success.set('Đã lưu kết quả phân tích tinh dịch');
        this.saving.set(false);
        setTimeout(() => this.router.navigate(['/andrology']), 1500);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi khi lưu');
        this.saving.set(false);
      },
    });
  }

  back() {
    this.router.navigate(['/andrology']);
  }
}
