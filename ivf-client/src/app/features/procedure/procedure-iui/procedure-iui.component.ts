import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ProcedureService } from '../../../core/services/procedure.service';
import { ProcedureDto } from '../../../core/models/procedure.models';

@Component({
  selector: 'app-procedure-iui',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './procedure-iui.component.html',
  styleUrls: ['./procedure-iui.component.scss'],
})
export class ProcedureIuiComponent implements OnInit {
  private service = inject(ProcedureService);
  private route = inject(ActivatedRoute);

  procedure = signal<ProcedureDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  iuiForm = {
    spermVolume: 0,
    spermConcentration: 0,
    motility: 0,
    morphology: 0,
    totalMotileCount: 0,
    catheterType: '',
    difficulty: 'Easy',
    durationMinutes: 10,
    findings: '',
    complications: '',
  };

  difficultyOptions = ['Easy', 'Moderate', 'Difficult'];

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

  completeIui() {
    this.saving.set(true);
    this.service
      .complete(this.procedure()!.id, {
        intraOpFindings: `IUI: Vol=${this.iuiForm.spermVolume}ml, Conc=${this.iuiForm.spermConcentration}M/ml, Motility=${this.iuiForm.motility}%, TMC=${this.iuiForm.totalMotileCount}M. Catheter: ${this.iuiForm.catheterType || 'N/A'}, Difficulty: ${this.iuiForm.difficulty}. ${this.iuiForm.findings}`,
        complications: this.iuiForm.complications || undefined,
        durationMinutes: this.iuiForm.durationMinutes,
      })
      .subscribe({
        next: (d) => {
          this.procedure.set(d);
          this.saving.set(false);
          this.successMsg.set('IUI hoàn thành');
        },
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi');
          this.saving.set(false);
        },
      });
  }
}
