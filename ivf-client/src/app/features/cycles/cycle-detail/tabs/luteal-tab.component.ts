import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';

@Component({
  selector: 'app-luteal-tab',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thuốc hỗ trợ hoàng thể</h3>
        <div class="drug-list" formArrayName="drugs">
          @for (drug of lutealDrugs; track $index) {
          <div class="drug-row" [formGroupName]="drug.index">
            <div class="form-group">
              <label>Thuốc {{ $index + 1 }}</label>
              <input type="text" formControlName="drugName" placeholder="Tên thuốc"/>
            </div>
            <div class="form-group drug-actions">
              <button type="button" class="btn btn-icon btn-danger" (click)="removeDrug(drug.index)" title="Xóa">✕</button>
            </div>
          </div>
          }
        </div>
        <button type="button" class="btn btn-secondary btn-sm" (click)="addDrug('Luteal')">+ Thêm thuốc</button>
      </div>
      <div class="form-section">
        <h3>Thuốc hỗ trợ nội mạc</h3>
        <div class="drug-list" formArrayName="drugs">
          @for (drug of endometriumDrugs; track $index) {
          <div class="drug-row" [formGroupName]="drug.index">
            <div class="form-group">
              <label>Thuốc {{ $index + 1 }}</label>
              <input type="text" formControlName="drugName" placeholder="Tên thuốc"/>
            </div>
            <div class="form-group drug-actions">
              <button type="button" class="btn btn-icon btn-danger" (click)="removeDrug(drug.index)" title="Xóa">✕</button>
            </div>
          </div>
          }
        </div>
        <button type="button" class="btn btn-secondary btn-sm" (click)="addDrug('Endometrium')">+ Thêm thuốc</button>
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
    .drug-list { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 0.75rem; }
    .drug-row { display: grid; grid-template-columns: 1fr auto; gap: 0.75rem; padding: 0.5rem; background: var(--surface); border-radius: 6px; align-items: end; }
    .drug-actions { justify-content: flex-end; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input { padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; background: var(--surface); font-size: 0.9rem; }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-secondary { background: var(--surface-elevated); color: var(--text-primary); border: 1px dashed var(--border); }
    .btn-sm { padding: 0.35rem 0.75rem; font-size: 0.85rem; }
    .btn-icon { padding: 0.25rem 0.5rem; font-size: 0.8rem; line-height: 1; }
    .btn-danger { background: transparent; color: var(--danger, #e53e3e); }
  `]
})
export class LutealTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);
  form!: FormGroup;
  loading = false;

  get drugsArray(): FormArray {
    return this.form.get('drugs') as FormArray;
  }

  get lutealDrugs(): { index: number; control: FormGroup }[] {
    return this.drugsArray.controls
      .map((c, i) => ({ index: i, control: c as FormGroup }))
      .filter(d => d.control.get('category')?.value === 'Luteal');
  }

  get endometriumDrugs(): { index: number; control: FormGroup }[] {
    return this.drugsArray.controls
      .map((c, i) => ({ index: i, control: c as FormGroup }))
      .filter(d => d.control.get('category')?.value === 'Endometrium');
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      drugs: this.fb.array([])
    });
    this.loadData();
  }

  createDrugGroup(drug?: { drugName?: string; category?: string }): FormGroup {
    return this.fb.group({
      drugName: [drug?.drugName || ''],
      category: [drug?.category || 'Luteal']
    });
  }

  addDrug(category: string): void {
    this.drugsArray.push(this.createDrugGroup({ category }));
  }

  removeDrug(index: number): void {
    this.drugsArray.removeAt(index);
  }

  loadData(): void {
    this.cycleService.getCycleLutealPhase(this.cycleId).subscribe({
      next: (data) => {
        if (!data) return;
        this.drugsArray.clear();
        if (data.drugs?.length) {
          data.drugs.forEach((d: any) => this.drugsArray.push(this.createDrugGroup(d)));
        } else {
          // Default 2+2 empty rows
          this.addDrug('Luteal');
          this.addDrug('Luteal');
          this.addDrug('Endometrium');
          this.addDrug('Endometrium');
        }
      },
      error: () => { }
    });
  }

  onSubmit(): void {
    if (this.loading) return;
    this.loading = true;

    const drugs = (this.drugsArray.value || [])
      .filter((d: any) => d.drugName?.trim())
      .map((d: any, i: number) => ({ ...d, sortOrder: i }));

    this.cycleService.updateCycleLutealPhase(this.cycleId, { drugs }).subscribe({
      next: () => { this.loading = false; this.saved.emit(); },
      error: () => { this.loading = false; }
    });
  }
}
