import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';

@Component({
  selector: 'app-birth-tab',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thông tin sinh</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Ngày sinh</label>
            <input type="date" formControlName="deliveryDate"/>
          </div>
          <div class="form-group">
            <label>Tuần thai</label>
            <input type="number" formControlName="gestationalWeeks" min="20" max="45"/>
          </div>
          <div class="form-group">
            <label>Phương pháp sinh</label>
            <select formControlName="deliveryMethod">
              <option value="">-- Chọn --</option>
              <option value="Vaginal">Sinh thường</option>
              <option value="Cesarean">Mổ lấy thai</option>
              <option value="Assisted">Hỗ trợ</option>
            </select>
          </div>
        </div>
      </div>
      <div class="form-section">
        <h3>Kết quả</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Số trẻ sống</label>
            <input type="number" formControlName="liveBirths" min="0"/>
          </div>
          <div class="form-group">
            <label>Thai lưu</label>
            <input type="number" formControlName="stillbirths" min="0"/>
          </div>
        </div>
      </div>
      <div class="form-section">
        <h3>Chi tiết trẻ</h3>
        <div class="outcome-list" formArrayName="outcomes">
          @for (outcome of outcomesArray.controls; track $index) {
          <div class="outcome-row" [formGroupName]="$index">
            <div class="form-group">
              <label>Giới tính</label>
              <select formControlName="gender">
                <option value="">--</option>
                <option value="M">Nam</option>
                <option value="F">Nữ</option>
              </select>
            </div>
            <div class="form-group">
              <label>Cân nặng (g)</label>
              <input type="number" formControlName="weight" min="0" step="1"/>
            </div>
            <div class="form-group">
              <label>Sống</label>
              <input type="checkbox" formControlName="isLiveBirth"/>
            </div>
            <div class="form-group outcome-actions">
              <button type="button" class="btn btn-icon btn-danger" (click)="removeOutcome($index)" title="Xóa">✕</button>
            </div>
          </div>
          }
        </div>
        <button type="button" class="btn btn-secondary btn-sm" (click)="addOutcome()">+ Thêm trẻ</button>
      </div>
      <div class="form-section">
        <h3>Biến chứng</h3>
        <div class="form-group full-width">
          <textarea formControlName="complications" rows="3" placeholder="Ghi chú biến chứng..."></textarea>
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
    .outcome-list { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 0.75rem; }
    .outcome-row { display: grid; grid-template-columns: 1fr 1fr auto auto; gap: 0.75rem; padding: 0.5rem; background: var(--surface); border-radius: 6px; align-items: end; }
    .outcome-actions { justify-content: flex-end; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input, .form-group select, .form-group textarea { 
      padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; 
      background: var(--surface); font-size: 0.9rem; font-family: inherit;
    }
    .form-group input[type="checkbox"] { width: 1.2rem; height: 1.2rem; margin-top: 0.25rem; }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-secondary { background: var(--surface-elevated); color: var(--text-primary); border: 1px dashed var(--border); }
    .btn-sm { padding: 0.35rem 0.75rem; font-size: 0.85rem; }
    .btn-icon { padding: 0.25rem 0.5rem; font-size: 0.8rem; line-height: 1; }
    .btn-danger { background: transparent; color: var(--danger, #e53e3e); }
  `]
})
export class BirthTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);
  form!: FormGroup;
  loading = false;

  get outcomesArray(): FormArray {
    return this.form.get('outcomes') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      deliveryDate: [''],
      gestationalWeeks: [null],
      deliveryMethod: [''],
      liveBirths: [0],
      stillbirths: [0],
      outcomes: this.fb.array([]),
      complications: ['']
    });
    this.loadData();
  }

  createOutcomeGroup(outcome?: { gender?: string; weight?: number | null; isLiveBirth?: boolean }): FormGroup {
    return this.fb.group({
      gender: [outcome?.gender || ''],
      weight: [outcome?.weight ?? null],
      isLiveBirth: [outcome?.isLiveBirth ?? true]
    });
  }

  addOutcome(): void {
    this.outcomesArray.push(this.createOutcomeGroup());
  }

  removeOutcome(index: number): void {
    this.outcomesArray.removeAt(index);
  }

  loadData(): void {
    this.cycleService.getCycleBirth(this.cycleId).subscribe({
      next: (data) => {
        if (data) {
          this.form.patchValue({
            deliveryDate: data.deliveryDate?.split('T')[0] || '',
            gestationalWeeks: data.gestationalWeeks,
            deliveryMethod: data.deliveryMethod || '',
            liveBirths: data.liveBirths,
            stillbirths: data.stillbirths,
            complications: data.complications || ''
          });
          // Populate outcomes
          this.outcomesArray.clear();
          if (data.outcomes?.length) {
            data.outcomes.forEach((o: any) => this.outcomesArray.push(this.createOutcomeGroup(o)));
          }
        }
      },
      error: () => { }
    });
  }

  onSubmit(): void {
    if (this.loading) return;
    this.loading = true;

    const formValue = this.form.value;
    const payload: any = { ...formValue };
    // Convert empty strings to null for scalar fields
    Object.keys(payload).forEach(key => {
      if (key !== 'outcomes' && payload[key] === '') {
        payload[key] = null;
      }
    });
    // Assign sortOrder and filter empty
    payload.outcomes = (formValue.outcomes || [])
      .filter((o: any) => o.gender?.trim())
      .map((o: any, i: number) => ({ ...o, sortOrder: i }));

    this.cycleService.updateCycleBirth(this.cycleId, payload).subscribe({
      next: () => { this.loading = false; this.saved.emit(); },
      error: () => { this.loading = false; }
    });
  }
}
