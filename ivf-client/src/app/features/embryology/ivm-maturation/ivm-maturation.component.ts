import { Component, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface IvmEgg {
  eggId: string;
  eggCode: string;
  maturityAtRetrieval: 'GV' | 'MI'; // only immature eggs go to IVM
  maturityAfterIvm: 'MII' | 'MI' | 'Degenerate' | 'Atretic' | '';
  ivmHours: number | null;
  notes: string;
}

@Component({
  selector: 'app-ivm-maturation',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './ivm-maturation.component.html',
  styleUrls: ['./ivm-maturation.component.scss'],
})
export class IvmMaturationComponent {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private readonly baseUrl = environment.apiUrl;

  cycleId = this.route.snapshot.paramMap.get('cycleId') ?? '';
  loading = signal(false);
  saving = signal(false);
  success = signal('');
  error = signal('');

  form = {
    maturationDate: new Date().toISOString().slice(0, 10),
    cultureStartTime: new Date().toISOString().slice(0, 16),
    cultureEndTime: '',
    cultureMedium: 'IVM Medium',
    incubatorId: '',
    performedByName: '',
    totalImmatureAtRetrieval: 0,
    notes: '',
  };

  eggs = signal<IvmEgg[]>([]);

  maturityOptions: { value: IvmEgg['maturityAfterIvm']; label: string }[] = [
    { value: 'MII', label: 'MII (Trưởng thành — dùng được)' },
    { value: 'MI', label: 'MI (Chưa trưởng thành)' },
    { value: 'Degenerate', label: 'Thoái hóa' },
    { value: 'Atretic', label: 'Không phát triển' },
  ];

  miiCount = computed(() => this.eggs().filter((e) => e.maturityAfterIvm === 'MII').length);
  miCount = computed(() => this.eggs().filter((e) => e.maturityAfterIvm === 'MI').length);
  degCount = computed(
    () =>
      this.eggs().filter(
        (e) => e.maturityAfterIvm === 'Degenerate' || e.maturityAfterIvm === 'Atretic',
      ).length,
  );
  ivmSuccessRate = computed(() => {
    const total = this.eggs().length;
    return total > 0 ? Math.round((this.miiCount() / total) * 100) : 0;
  });

  addEgg(): void {
    const idx = this.eggs().length + 1;
    this.eggs.update((list) => [
      ...list,
      {
        eggId: '',
        eggCode: `IVM-${String(idx).padStart(2, '0')}`,
        maturityAtRetrieval: 'GV',
        maturityAfterIvm: '',
        ivmHours: 24,
        notes: '',
      },
    ]);
  }

  removeEgg(idx: number): void {
    this.eggs.update((list) => list.filter((_, i) => i !== idx));
  }

  calculateHours(egg: IvmEgg): number | null {
    if (!this.form.cultureStartTime || !this.form.cultureEndTime) return egg.ivmHours;
    const start = new Date(this.form.cultureStartTime);
    const end = new Date(this.form.cultureEndTime);
    return Math.round((end.getTime() - start.getTime()) / (1000 * 3600));
  }

  save(): void {
    if (!this.cycleId) {
      this.error.set('Thiếu thông tin chu kỳ');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    const payload = {
      cycleId: this.cycleId,
      ...this.form,
      eggs: this.eggs().map((e) => ({
        ...e,
        ivmHours: this.calculateHours(e) ?? e.ivmHours,
      })),
      summary: {
        totalIn: this.eggs().length,
        miiCount: this.miiCount(),
        miCount: this.miCount(),
        degenerateCount: this.degCount(),
        ivmSuccessRate: this.ivmSuccessRate(),
      },
    };
    this.http.post(`${this.baseUrl}/cycles/${this.cycleId}/ivm-maturation`, payload).subscribe({
      next: () => {
        this.success.set('Đã lưu kết quả nuôi trưởng thành IVM');
        this.saving.set(false);
        setTimeout(() => this.router.navigate(['/cycles', this.cycleId]), 2000);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi lưu kết quả IVM');
        this.saving.set(false);
      },
    });
  }

  back(): void {
    this.router.navigate(['/cycles', this.cycleId]);
  }
}
