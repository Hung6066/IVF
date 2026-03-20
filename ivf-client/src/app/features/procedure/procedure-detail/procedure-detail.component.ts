import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ProcedureService } from '../../../core/services/procedure.service';
import { ProcedureDto } from '../../../core/models/procedure.models';

@Component({
  selector: 'app-procedure-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './procedure-detail.component.html',
  styleUrls: ['./procedure-detail.component.scss'],
})
export class ProcedureDetailComponent implements OnInit {
  private service = inject(ProcedureService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  procedure = signal<ProcedureDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  showCompleteForm = false;
  completeForm = { intraOpFindings: '', postOpNotes: '', complications: '', durationMinutes: 30 };

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id') || '';
    if (id) {
      this.loading.set(true);
      this.service.getById(id).subscribe({
        next: (d) => {
          this.procedure.set(d);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Không tìm thấy thủ thuật');
          this.loading.set(false);
        },
      });
    }
  }

  startProcedure() {
    if (!confirm('Bắt đầu thực hiện thủ thuật?')) return;
    this.saving.set(true);
    this.service.start(this.procedure()!.id).subscribe({
      next: (d) => {
        this.procedure.set(d);
        this.saving.set(false);
        this.successMsg.set('Đã bắt đầu thủ thuật');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  completeProcedure() {
    this.saving.set(true);
    this.service.complete(this.procedure()!.id, this.completeForm).subscribe({
      next: (d) => {
        this.procedure.set(d);
        this.saving.set(false);
        this.showCompleteForm = false;
        this.successMsg.set('Đã hoàn thành thủ thuật');
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  cancelProcedure() {
    const reason = prompt('Lý do huỷ thủ thuật:');
    if (reason === null) return;
    this.saving.set(true);
    this.service.cancel(this.procedure()!.id, { reason }).subscribe({
      next: (d) => {
        this.procedure.set(d);
        this.saving.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi');
        this.saving.set(false);
      },
    });
  }

  statusClass(s: string): string {
    const m: Record<string, string> = {
      Scheduled: 'bg-blue-100 text-blue-700',
      InProgress: 'bg-yellow-100 text-yellow-700',
      Completed: 'bg-green-100 text-green-700',
      Cancelled: 'bg-red-100 text-red-600',
    };
    return m[s] || 'bg-gray-100 text-gray-600';
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
  formatTime(d?: string) {
    return d
      ? new Date(d).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
      : '—';
  }
}
