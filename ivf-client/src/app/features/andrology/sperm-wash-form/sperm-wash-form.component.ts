import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
  selector: 'app-sperm-wash-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, CycleSearchComponent],
  templateUrl: './sperm-wash-form.component.html',
  styleUrls: ['./sperm-wash-form.component.scss'],
})
export class SpermWashFormComponent {
  private http = inject(HttpClient);
  private router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

  saving = signal(false);
  error = signal('');
  success = signal('');

  form = {
    cycleId: '',
    method: 'Swim-up',
    washDate: new Date().toISOString().slice(0, 16),
    preWashConcentration: undefined as number | undefined,
    preWashMotility: undefined as number | undefined,
    postWashConcentration: undefined as number | undefined,
    postWashMotility: undefined as number | undefined,
    postWashVolume: undefined as number | undefined,
    totalMotileCount: undefined as number | undefined,
    indication: 'IUI',
    notes: '',
  };

  methods = ['Swim-up', 'Density gradient', 'Simple wash', 'Pellet'];
  indications = ['IUI', 'ICSI', 'IVF', 'Cryo'];

  save() {
    if (!this.form.cycleId) {
      this.error.set('Vui lòng nhập mã chu kỳ');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.http.post(`${this.baseUrl}/andrology/sperm-wash`, this.form).subscribe({
      next: () => {
        this.success.set('Đã lưu kết quả lọc rửa tinh trùng');
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
