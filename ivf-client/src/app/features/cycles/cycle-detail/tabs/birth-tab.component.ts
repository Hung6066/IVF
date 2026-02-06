import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
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
          <div class="form-group">
            <label>Giới tính (M/F)</label>
            <input type="text" formControlName="babyGenders" placeholder="VD: M, F"/>
          </div>
          <div class="form-group">
            <label>Cân nặng (g)</label>
            <input type="text" formControlName="birthWeights" placeholder="VD: 3200, 3100"/>
          </div>
        </div>
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
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input, .form-group select, .form-group textarea { 
      padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; 
      background: var(--surface); font-size: 0.9rem; font-family: inherit;
    }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
  `]
})
export class BirthTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);
  form!: FormGroup;
  loading = false;

  ngOnInit(): void {
    this.form = this.fb.group({
      deliveryDate: [''],
      gestationalWeeks: [null],
      deliveryMethod: [''],
      liveBirths: [0],
      stillbirths: [0],
      babyGenders: [''],
      birthWeights: [''],
      complications: ['']
    });
    this.loadData();
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
            babyGenders: data.babyGenders || '',
            birthWeights: data.birthWeights || '',
            complications: data.complications || ''
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

    this.cycleService.updateCycleBirth(this.cycleId, formValue).subscribe({
      next: () => { this.loading = false; this.saved.emit(); },
      error: () => { this.loading = false; }
    });
  }
}
