import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FetService } from '../../../core/services/fet.service';
import { FetProtocolDto } from '../../../core/models/fet.models';

@Component({
  selector: 'app-fet-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './fet-detail.component.html',
  styleUrls: ['./fet-detail.component.scss'],
})
export class FetDetailComponent implements OnInit {
  private service = inject(FetService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  id = '';
  protocol = signal<FetProtocolDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');
  activeTab = 'overview';

  // Endometrium form
  showEndoForm = false;
  endoForm = { thickness: 0, pattern: '', checkDate: new Date().toISOString().slice(0, 10) };

  // Thawing form
  showThawForm = false;
  thawForm = {
    embryosToThaw: 1,
    embryosSurvived: 1,
    thawDate: new Date().toISOString().slice(0, 10),
    embryoGrade: '',
    embryoAge: 5,
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
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Không tìm thấy FET protocol');
        this.loading.set(false);
      },
    });
  }

  saveEndometrium() {
    this.saving.set(true);
    this.service.recordEndometriumCheck(this.id, this.endoForm).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.showEndoForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã lưu SA nội mạc');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  saveThawing() {
    this.saving.set(true);
    this.service.recordThawing(this.id, this.thawForm).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.showThawForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã lưu thông tin rã đông phôi');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  markTransferred() {
    if (!confirm('Xác nhận đã thực hiện chuyển phôi trữ?')) return;
    this.saving.set(true);
    this.service.markTransferred(this.id).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.saving.set(false);
        this.successMsg.set('Đã ghi nhận chuyển phôi trữ');
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  formatDate(d?: string): string {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('vi-VN');
  }
}
