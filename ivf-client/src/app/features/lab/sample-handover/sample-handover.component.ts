import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface HandoverItem {
  sampleId: string;
  sampleCode: string;
  sampleType: string;
  patientName: string;
  cycleId: string;
  quantity: number;
  unit: string;
  condition: 'Good' | 'Acceptable' | 'Poor';
  notes: string;
}

@Component({
  selector: 'app-sample-handover',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sample-handover.component.html',
  styleUrls: ['./sample-handover.component.scss'],
})
export class SampleHandoverComponent {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private readonly baseUrl = environment.apiUrl;

  saving = signal(false);
  success = signal('');
  error = signal('');
  searchQuery = signal('');
  searchResults = signal<any[]>([]);
  searching = signal(false);

  handoverForm = {
    handoverType: 'NHS_TO_LABO' as 'NHS_TO_LABO' | 'LABO_TO_NHS',
    handoverDate: new Date().toISOString().slice(0, 16),
    fromDepartment: 'NHS',
    toDepartment: 'LABO',
    handedByName: '',
    receivedByName: '',
    notes: '',
  };

  items = signal<HandoverItem[]>([]);

  sampleTypes = ['Tinh trùng đông lạnh', 'Noãn tươi', 'Phôi đông lạnh', 'Mẫu xét nghiệm'];
  conditionOptions: { value: HandoverItem['condition']; label: string }[] = [
    { value: 'Good', label: 'Tốt' },
    { value: 'Acceptable', label: 'Chấp nhận' },
    { value: 'Poor', label: 'Kém' },
  ];

  get handoverTypeLabel(): string {
    return this.handoverForm.handoverType === 'NHS_TO_LABO'
      ? 'NHS → LABO (Bàn giao vào phòng lab)'
      : 'LABO → NHS (Trả mẫu từ phòng lab)';
  }

  onHandoverTypeChange(): void {
    if (this.handoverForm.handoverType === 'NHS_TO_LABO') {
      this.handoverForm.fromDepartment = 'NHS';
      this.handoverForm.toDepartment = 'LABO';
    } else {
      this.handoverForm.fromDepartment = 'LABO';
      this.handoverForm.toDepartment = 'NHS';
    }
  }

  searchSamples(): void {
    const q = this.searchQuery();
    if (!q || q.length < 2) return;
    this.searching.set(true);
    this.http
      .get<any>(`${this.baseUrl}/spermbank/samples/available`, {
        params: { q, pageSize: '20' },
      })
      .subscribe({
        next: (res) => {
          this.searchResults.set(res.items ?? res ?? []);
          this.searching.set(false);
        },
        error: () => {
          this.searching.set(false);
        },
      });
  }

  addSampleItem(): void {
    this.items.update((list) => [
      ...list,
      {
        sampleId: '',
        sampleCode: '',
        sampleType: 'Tinh trùng đông lạnh',
        patientName: '',
        cycleId: '',
        quantity: 1,
        unit: 'Lọ',
        condition: 'Good',
        notes: '',
      },
    ]);
  }

  removeItem(idx: number): void {
    this.items.update((list) => list.filter((_, i) => i !== idx));
  }

  canSubmit(): boolean {
    return (
      this.items().length > 0 &&
      !!this.handoverForm.handedByName &&
      !!this.handoverForm.receivedByName
    );
  }

  submit(): void {
    if (!this.canSubmit()) {
      this.error.set('Vui lòng điền đầy đủ thông tin người bàn giao và nhận');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    const payload = {
      ...this.handoverForm,
      items: this.items(),
    };
    this.http.post(`${this.baseUrl}/lab/sample-handovers`, payload).subscribe({
      next: () => {
        this.success.set('Đã lưu biên bản bàn giao mẫu thành công');
        this.saving.set(false);
        setTimeout(() => this.router.navigate(['/lab']), 1800);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi khi lưu bàn giao');
        this.saving.set(false);
      },
    });
  }

  printHandover(): void {
    window.print();
  }

  back(): void {
    this.router.navigate(['/lab']);
  }
}
