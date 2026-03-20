import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface AvailableSample {
  id: string;
  sampleCode: string;
  donorId: string;
  donorCode: string;
  vialCount: number;
  status: string;
}

@Component({
  selector: 'app-sperm-sample-usage',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sperm-sample-usage.component.html',
  styleUrls: ['./sperm-sample-usage.component.scss'],
})
export class SpermSampleUsageComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private readonly baseUrl = environment.apiUrl;

  cycleId = this.route.snapshot.paramMap.get('cycleId') ?? '';
  samples = signal<AvailableSample[]>([]);
  loading = signal(false);
  saving = signal(false);
  success = signal('');
  error = signal('');
  selectedSample = signal<AvailableSample | null>(null);

  procedureOptions = [
    { value: 'IUI-D', label: 'IUI-D (Thụ tinh trong tử cung — tinh trùng người hiến)' },
    { value: 'ICSI', label: 'ICSI (Tiêm tinh trùng vào noãn)' },
    { value: 'IVF', label: 'IVF (Thụ tinh trong ống nghiệm)' },
    { value: 'IUI', label: 'IUI (Thụ tinh trong tử cung)' },
  ];

  form = {
    spermSampleId: '',
    usageDate: new Date().toISOString().slice(0, 16),
    procedure: 'IUI-D',
    vialsUsed: 1,
    authorizedByName: '',
    performedByName: '',
    postThawMotility: null as number | null,
    postThawConcentration: null as number | null,
    postThawNotes: '',
    notes: '',
  };

  ngOnInit(): void {
    this.loadAvailableSamples();
  }

  loadAvailableSamples(): void {
    this.loading.set(true);
    this.http.get<any[]>(`${this.baseUrl}/spermbank/samples/available`).subscribe({
      next: (items) => {
        this.samples.set(
          items.map((s) => ({
            id: s.id,
            sampleCode: s.sampleCode,
            donorId: s.donorId,
            donorCode: s.donorCode ?? s.donorId,
            vialCount: s.vialCount ?? s.totalVials ?? 0,
            status: s.status,
          })),
        );
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onSampleSelect(): void {
    const found = this.samples().find((s) => s.id === this.form.spermSampleId);
    this.selectedSample.set(found ?? null);
    if (found) this.form.vialsUsed = Math.min(1, found.vialCount);
  }

  canSubmit(): boolean {
    return !!this.form.spermSampleId && !!this.form.procedure && this.form.vialsUsed > 0;
  }

  save(): void {
    if (!this.canSubmit()) {
      this.error.set('Chọn mẫu và điền đủ thông tin');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.http
      .post(`${this.baseUrl}/spermbank/samples/${this.form.spermSampleId}/usage`, {
        cycleId: this.cycleId,
        ...this.form,
      })
      .subscribe({
        next: () => {
          this.success.set('Đã ghi nhận sử dụng mẫu tinh trùng thành công');
          this.saving.set(false);
          setTimeout(() => this.router.navigate(['/sperm-bank']), 1800);
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi khi lưu');
          this.saving.set(false);
        },
      });
  }

  back(): void {
    this.router.navigate(['/sperm-bank']);
  }
}
