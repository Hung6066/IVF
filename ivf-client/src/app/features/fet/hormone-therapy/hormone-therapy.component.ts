import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FetService } from '../../../core/services/fet.service';
import { FetProtocolDto, UpdateHormoneTherapyRequest } from '../../../core/models/fet.models';

@Component({
  selector: 'app-hormone-therapy',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './hormone-therapy.component.html',
  styleUrls: ['./hormone-therapy.component.scss']
})
export class HormoneTherapyComponent implements OnInit {
  private service = inject(FetService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  id = '';
  protocol = signal<FetProtocolDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  form: UpdateHormoneTherapyRequest = {
    estrogenDrug: '',
    estrogenDose: '',
    estrogenStartDate: new Date().toISOString().slice(0, 10),
    progesteroneDrug: '',
    progesteroneDose: '',
    progesteroneStartDate: ''
  };

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id') || '';
    if (this.id) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getById(this.id).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.form = {
          estrogenDrug: data.estrogenDrug || '',
          estrogenDose: data.estrogenDose || '',
          estrogenStartDate: data.estrogenStartDate?.slice(0, 10) || '',
          progesteroneDrug: data.progesteroneDrug || '',
          progesteroneDose: data.progesteroneDose || '',
          progesteroneStartDate: data.progesteroneStartDate?.slice(0, 10) || ''
        };
        this.loading.set(false);
      },
      error: () => { this.error.set('Không tải được dữ liệu'); this.loading.set(false); }
    });
  }

  save() {
    this.saving.set(true);
    this.error.set('');
    this.service.updateHormoneTherapy(this.id, this.form).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.saving.set(false);
        this.successMsg.set('Đã cập nhật nội tiết');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi lưu'); this.saving.set(false); }
    });
  }

  back() { this.router.navigate(['/fet', this.id]); }

  formatDate(d?: string): string {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('vi-VN');
  }
}
