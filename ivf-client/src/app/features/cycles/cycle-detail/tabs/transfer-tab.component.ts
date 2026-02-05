import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';

@Component({
    selector: 'app-transfer-tab',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thông tin chuyển phôi</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Ngày chuyển phôi</label>
            <input type="date" formControlName="transferDate"/>
          </div>
          <div class="form-group">
            <label>Ngày rã phôi</label>
            <input type="date" formControlName="thawingDate"/>
          </div>
          <div class="form-group">
            <label>Ngày phôi (D?)</label>
            <input type="number" formControlName="dayOfTransfered" min="1" max="7"/>
          </div>
        </div>
      </div>
      <div class="form-section">
        <h3>Ghi chú Lab</h3>
        <div class="form-group full-width">
          <textarea formControlName="labNote" rows="4" placeholder="Ghi chú từ phòng Lab..."></textarea>
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
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input, .form-group textarea { 
      padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; 
      background: var(--surface); font-size: 0.9rem; font-family: inherit;
    }
    .form-group textarea { resize: vertical; min-height: 100px; }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
  `]
})
export class TransferTabComponent implements OnInit {
    @Input() cycleId!: string;
    @Output() saved = new EventEmitter<void>();

    private fb = inject(FormBuilder);
    private cycleService = inject(CycleService);
    form!: FormGroup;
    loading = false;

    ngOnInit(): void {
        this.form = this.fb.group({
            transferDate: [''],
            thawingDate: [''],
            dayOfTransfered: [null],
            labNote: ['']
        });
        this.loadData();
    }

    loadData(): void {
        this.cycleService.getCycleTransfer(this.cycleId).subscribe({
            next: (data) => {
                if (data) {
                    this.form.patchValue({
                        transferDate: data.transferDate?.split('T')[0] || '',
                        thawingDate: data.thawingDate?.split('T')[0] || '',
                        dayOfTransfered: data.dayOfTransfered,
                        labNote: data.labNote || ''
                    });
                }
            },
            error: () => { }
        });
    }

    onSubmit(): void {
        if (this.loading) return;
        this.loading = true;
        this.cycleService.updateCycleTransfer(this.cycleId, this.form.value).subscribe({
            next: () => { this.loading = false; this.saved.emit(); },
            error: () => { this.loading = false; }
        });
    }
}
