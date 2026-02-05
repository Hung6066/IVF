import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ApiService } from '../../../../core/services/api.service';

@Component({
    selector: 'app-culture-tab',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thống kê phôi</h3>
        <div class="stats-grid">
          <div class="stat-card">
            <label>Tổng phôi đông</label>
            <input type="number" formControlName="totalFreezedEmbryo" min="0"/>
          </div>
          <div class="stat-card">
            <label>Tổng phôi rã</label>
            <input type="number" formControlName="totalThawedEmbryo" min="0"/>
          </div>
          <div class="stat-card">
            <label>Tổng phôi chuyển</label>
            <input type="number" formControlName="totalTransferedEmbryo" min="0"/>
          </div>
          <div class="stat-card highlight">
            <label>Phôi còn lại</label>
            <input type="number" formControlName="remainFreezedEmbryo" min="0"/>
          </div>
        </div>
      </div>
      <div class="form-actions">
        <button type="submit" class="btn btn-primary" [disabled]="loading">
          {{ loading ? 'Đang lưu...' : '💾 Lưu' }}
        </button>
      </div>
    </form>
  `,
    styles: [`
    .phase-form { padding: 1rem; }
    .form-section { margin-bottom: 1.5rem; padding: 1rem; background: var(--surface-elevated); border-radius: 8px; }
    .form-section h3 { margin: 0 0 1rem; font-size: 1rem; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 1rem; }
    .stat-card { display: flex; flex-direction: column; gap: 0.5rem; padding: 1rem; background: var(--surface); border-radius: 8px; text-align: center; }
    .stat-card.highlight { background: linear-gradient(135deg, var(--primary) 0%, #667eea 100%); color: white; }
    .stat-card.highlight label { color: rgba(255,255,255,0.9); }
    .stat-card label { font-size: 0.85rem; color: var(--text-secondary); }
    .stat-card input { padding: 0.75rem; border: 1px solid var(--border); border-radius: 6px; font-size: 1.25rem; font-weight: 600; text-align: center; background: var(--surface); }
    .stat-card.highlight input { background: rgba(255,255,255,0.2); border-color: rgba(255,255,255,0.3); color: white; }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
  `]
})
export class CultureTabComponent implements OnInit {
    @Input() cycleId!: string;
    @Output() saved = new EventEmitter<void>();

    private fb = inject(FormBuilder);
    private api = inject(ApiService);
    form!: FormGroup;
    loading = false;

    ngOnInit(): void {
        this.form = this.fb.group({
            totalFreezedEmbryo: [0],
            totalThawedEmbryo: [0],
            totalTransferedEmbryo: [0],
            remainFreezedEmbryo: [0]
        });
        this.loadData();
    }

    loadData(): void {
        this.api.getCycleCulture(this.cycleId).subscribe({
            next: (data) => data && this.form.patchValue(data),
            error: () => { }
        });
    }

    onSubmit(): void {
        if (this.loading) return;
        this.loading = true;
        this.api.updateCycleCulture(this.cycleId, this.form.value).subscribe({
            next: () => { this.loading = false; this.saved.emit(); },
            error: () => { this.loading = false; }
        });
    }
}
