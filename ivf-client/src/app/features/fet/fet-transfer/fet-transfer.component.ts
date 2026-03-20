import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FetService } from '../../../core/services/fet.service';
import { FetProtocolDto, ScheduleTransferRequest, RecordThawingRequest } from '../../../core/models/fet.models';

@Component({
  selector: 'app-fet-transfer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './fet-transfer.component.html',
  styleUrls: ['./fet-transfer.component.scss']
})
export class FetTransferComponent implements OnInit {
  private service = inject(FetService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  id = '';
  protocol = signal<FetProtocolDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');
  activeStep: 'schedule' | 'thaw' | 'transfer' = 'schedule';

  scheduleForm: ScheduleTransferRequest = {
    transferDate: new Date().toISOString().slice(0, 10)
  };

  thawForm: RecordThawingRequest = {
    embryosToThaw: 1,
    embryosSurvived: 1,
    thawDate: new Date().toISOString().slice(0, 10),
    embryoGrade: '',
    embryoAge: 5
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
        if (data.plannedTransferDate) {
          this.scheduleForm.transferDate = data.plannedTransferDate.slice(0, 10);
          this.activeStep = data.thawDate ? 'transfer' : 'thaw';
        }
        if (data.thawDate) {
          this.thawForm = {
            embryosToThaw: data.embryosToThaw,
            embryosSurvived: data.embryosSurvived,
            thawDate: data.thawDate.slice(0, 10),
            embryoGrade: data.embryoGrade || '',
            embryoAge: data.embryoAge
          };
        }
        this.loading.set(false);
      },
      error: () => { this.error.set('Không tải được dữ liệu'); this.loading.set(false); }
    });
  }

  scheduleTransfer() {
    this.saving.set(true);
    this.service.scheduleTransfer(this.id, this.scheduleForm).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.activeStep = 'thaw';
        this.saving.set(false);
        this.successMsg.set('Đã lên lịch chuyển phôi');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi'); this.saving.set(false); }
    });
  }

  recordThawing() {
    this.saving.set(true);
    this.service.recordThawing(this.id, this.thawForm).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.activeStep = 'transfer';
        this.saving.set(false);
        this.successMsg.set('Đã ghi nhận rã đông phôi');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi'); this.saving.set(false); }
    });
  }

  confirmTransfer() {
    if (!confirm('Xác nhận đã thực hiện chuyển phôi trữ?')) return;
    this.saving.set(true);
    this.service.markTransferred(this.id).subscribe({
      next: (data) => {
        this.protocol.set(data);
        this.saving.set(false);
        this.successMsg.set('Đã ghi nhận chuyển phôi trữ thành công!');
      },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi'); this.saving.set(false); }
    });
  }

  back() { this.router.navigate(['/fet', this.id]); }

  formatDate(d?: string): string {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('vi-VN');
  }
}
