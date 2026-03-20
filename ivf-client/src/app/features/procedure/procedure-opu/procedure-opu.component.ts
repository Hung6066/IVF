import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ProcedureService } from '../../../core/services/procedure.service';
import { ProcedureDto } from '../../../core/models/procedure.models';

@Component({
  selector: 'app-procedure-opu',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './procedure-opu.component.html',
  styleUrls: ['./procedure-opu.component.scss'],
})
export class ProcedureOpuComponent implements OnInit {
  private service = inject(ProcedureService);
  private route = inject(ActivatedRoute);

  procedure = signal<ProcedureDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  opuForm = {
    leftOocytes: 0,
    rightOocytes: 0,
    totalOocytes: 0,
    aspirationVolume: 0,
    durationMinutes: 15,
    findings: '',
    complications: '',
  };

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
          this.error.set('Không tìm thấy');
          this.loading.set(false);
        },
      });
    }
  }

  updateTotal() {
    this.opuForm.totalOocytes = this.opuForm.leftOocytes + this.opuForm.rightOocytes;
  }

  completeOpu() {
    this.saving.set(true);
    this.service
      .complete(this.procedure()!.id, {
        intraOpFindings: `OPU: Trái ${this.opuForm.leftOocytes} noãn, Phải ${this.opuForm.rightOocytes} noãn. Tổng: ${this.opuForm.totalOocytes}. ${this.opuForm.findings}`,
        postOpNotes: `V dịch chọc: ${this.opuForm.aspirationVolume}ml`,
        complications: this.opuForm.complications || undefined,
        durationMinutes: this.opuForm.durationMinutes,
      })
      .subscribe({
        next: (d) => {
          this.procedure.set(d);
          this.saving.set(false);
          this.successMsg.set('OPU hoàn thành');
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi');
          this.saving.set(false);
        },
      });
  }
}
