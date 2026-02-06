import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';

@Component({
  selector: 'app-luteal-tab',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thuốc hỗ trợ hoàng thể</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Thuốc 1</label>
            <input type="text" formControlName="lutealDrug1" placeholder="Tên thuốc"/>
          </div>
          <div class="form-group">
            <label>Thuốc 2</label>
            <input type="text" formControlName="lutealDrug2" placeholder="Tên thuốc"/>
          </div>
        </div>
      </div>
      <div class="form-section">
        <h3>Thuốc hỗ trợ nội mạc</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Thuốc 1</label>
            <input type="text" formControlName="endometriumDrug1" placeholder="Tên thuốc"/>
          </div>
          <div class="form-group">
            <label>Thuốc 2</label>
            <input type="text" formControlName="endometriumDrug2" placeholder="Tên thuốc"/>
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
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input { padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; background: var(--surface); font-size: 0.9rem; }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
  `]
})
export class LutealTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);
  form!: FormGroup;
  loading = false;

  ngOnInit(): void {
    this.form = this.fb.group({
      lutealDrug1: [''],
      lutealDrug2: [''],
      endometriumDrug1: [''],
      endometriumDrug2: ['']
    });
    this.loadData();
  }

  loadData(): void {
    this.cycleService.getCycleLutealPhase(this.cycleId).subscribe({
      next: (data) => data && this.form.patchValue(data),
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

    this.cycleService.updateCycleLutealPhase(this.cycleId, formValue).subscribe({
      next: () => { this.loading = false; this.saved.emit(); },
      error: () => { this.loading = false; }
    });
  }
}
