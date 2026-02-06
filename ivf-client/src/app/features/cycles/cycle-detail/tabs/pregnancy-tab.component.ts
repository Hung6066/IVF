import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';

@Component({
  selector: 'app-pregnancy-tab',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Kết quả thử thai</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Beta HCG</label>
            <input type="number" formControlName="betaHcg" min="0" step="0.01"/>
          </div>
          <div class="form-group">
            <label>Ngày xét nghiệm</label>
            <input type="date" formControlName="betaHcgDate"/>
          </div>
          <div class="form-group checkbox-group">
            <label class="checkbox-label">
              <input type="checkbox" formControlName="isPregnant"/>
              <span class="checkmark"></span>
              Có thai
            </label>
          </div>
        </div>
      </div>
      <div class="form-section">
        <h3>Siêu âm thai</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Số túi thai</label>
            <input type="number" formControlName="gestationalSacs" min="0"/>
          </div>
          <div class="form-group">
            <label>Tim thai</label>
            <input type="number" formControlName="fetalHeartbeats" min="0"/>
          </div>
          <div class="form-group">
            <label>Ngày dự sinh</label>
            <input type="date" formControlName="dueDate"/>
          </div>
        </div>
      </div>
      <div class="form-section">
        <h3>Ghi chú</h3>
        <div class="form-group full-width">
          <textarea formControlName="notes" rows="3" placeholder="Ghi chú..."></textarea>
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
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; align-items: end; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input, .form-group textarea { 
      padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; 
      background: var(--surface); font-size: 0.9rem; font-family: inherit;
    }
    .checkbox-group { justify-content: flex-end; }
    .checkbox-label { display: flex; align-items: center; gap: 0.5rem; cursor: pointer; font-weight: 500; color: var(--text-primary); }
    .checkbox-label input[type="checkbox"] { width: 18px; height: 18px; accent-color: var(--primary); }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
  `]
})
export class PregnancyTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);
  form!: FormGroup;
  loading = false;

  ngOnInit(): void {
    this.form = this.fb.group({
      betaHcg: [null],
      betaHcgDate: [''],
      isPregnant: [false],
      gestationalSacs: [null],
      fetalHeartbeats: [null],
      dueDate: [''],
      notes: ['']
    });
    this.loadData();
  }

  loadData(): void {
    this.cycleService.getCyclePregnancy(this.cycleId).subscribe({
      next: (data) => {
        if (data) {
          this.form.patchValue({
            betaHcg: data.betaHcg,
            betaHcgDate: data.betaHcgDate?.split('T')[0] || '',
            isPregnant: data.isPregnant,
            gestationalSacs: data.gestationalSacs,
            fetalHeartbeats: data.fetalHeartbeats,
            dueDate: data.dueDate?.split('T')[0] || '',
            notes: data.notes || ''
          });
        }
      },
      error: () => { }
    });
  }

  onSubmit(): void {
    if (this.loading) return;
    this.loading = true;

    const formValue = { ...this.form.value };
    Object.keys(formValue).forEach(key => {
      if (formValue[key] === '') formValue[key] = null;
    });

    this.cycleService.updateCyclePregnancy(this.cycleId, formValue).subscribe({
      next: () => { this.loading = false; this.saved.emit(); },
      error: () => { this.loading = false; }
    });
  }
}
