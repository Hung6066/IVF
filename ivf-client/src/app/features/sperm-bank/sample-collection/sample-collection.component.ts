import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-sample-collection',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sample-collection.component.html',
  styleUrls: ['./sample-collection.component.scss'],
})
export class SampleCollectionComponent {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

  donorId = this.route.snapshot.paramMap.get('donorId') ?? '';
  saving = signal(false);
  error = signal('');
  success = signal('');

  form = {
    collectionDate: new Date().toISOString().slice(0, 16),
    abstinenceDays: undefined as number | undefined,
    volume: undefined as number | undefined,
    concentration: undefined as number | undefined,
    progressiveMotility: undefined as number | undefined,
    normalMorphology: undefined as number | undefined,
    vials: 1,
    processingMethod: 'Swim-up',
    notes: '',
    collectionType: 'Collection1',
  };

  processingMethods = ['Swim-up', 'Density gradient', 'Direct'];
  collectionTypes = [
    { value: 'Collection1', label: 'Lấy mẫu lần 1' },
    { value: 'Collection2', label: 'Lấy mẫu lần 2' },
  ];

  save() {
    if (!this.donorId) {
      this.error.set('Thiếu mã người hiến');
      return;
    }
    this.saving.set(true);
    this.http.post(`${this.baseUrl}/sperm-donors/${this.donorId}/samples`, this.form).subscribe({
      next: () => {
        this.success.set('Đã lưu mẫu thành công');
        this.saving.set(false);
        setTimeout(() => this.router.navigate(['/sperm-bank']), 1500);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi lưu mẫu');
        this.saving.set(false);
      },
    });
  }

  back() {
    this.router.navigate(['/sperm-bank']);
  }
}
