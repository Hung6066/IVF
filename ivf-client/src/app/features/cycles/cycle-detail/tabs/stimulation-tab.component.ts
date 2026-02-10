import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';

@Component({
  selector: 'app-stimulation-tab',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thông tin kích thích</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Ngày kinh cuối</label>
            <input type="date" formControlName="lastMenstruation" />
          </div>
          <div class="form-group">
            <label>Ngày bắt đầu</label>
            <input type="date" formControlName="startDate" />
          </div>
          <div class="form-group">
            <label>Ngày trong chu kỳ</label>
            <input type="number" formControlName="startDay" min="1" />
          </div>
        </div>
      </div>

      <div class="form-section">
        <h3>Thuốc sử dụng</h3>
        <div class="drug-grid" formArrayName="drugs">
          @for (drug of drugsArray.controls; track $index) {
          <div class="drug-row" [formGroupName]="$index">
            <div class="form-group">
              <label>Thuốc {{ $index + 1 }}</label>
              <input type="text" formControlName="drugName" placeholder="Tên thuốc"/>
            </div>
            <div class="form-group">
              <label>Số ngày</label>
              <input type="number" formControlName="duration" min="0"/>
            </div>
            <div class="form-group">
              <label>Liều dùng</label>
              <input type="text" formControlName="posology" placeholder="Liều"/>
            </div>
            <div class="form-group drug-actions">
              <button type="button" class="btn btn-icon btn-danger" (click)="removeDrug($index)" title="Xóa">✕</button>
            </div>
          </div>
          }
        </div>
        <button type="button" class="btn btn-secondary btn-sm" (click)="addDrug()">+ Thêm thuốc</button>
      </div>

      <div class="form-section">
        <h3>Theo dõi nang</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Nang ≥12mm</label>
            <input type="number" formControlName="size12Follicle" min="0"/>
          </div>
          <div class="form-group">
            <label>Nang ≥14mm</label>
            <input type="number" formControlName="size14Follicle" min="0"/>
          </div>
          <div class="form-group">
            <label>Nội mạc tử cung (mm)</label>
            <input type="number" formControlName="endometriumThickness" min="0" step="0.1"/>
          </div>
        </div>
      </div>

      <div class="form-section">
        <h3>Trigger & Chọc hút</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Thuốc trigger</label>
            <input type="text" formControlName="triggerDrug" placeholder="Tên thuốc"/>
          </div>
          <div class="form-group">
            <label>Thuốc trigger 2</label>
            <input type="text" formControlName="triggerDrug2" placeholder="Tên thuốc"/>
          </div>
          <div class="form-group">
            <label>Ngày HCG</label>
            <input type="date" formControlName="hcgDate"/>
          </div>
          <div class="form-group">
            <label>Giờ HCG</label>
            <input type="time" formControlName="hcgTime"/>
          </div>
          <div class="form-group">
            <label>Ngày chọc hút</label>
            <input type="date" formControlName="aspirationDate"/>
          </div>
          <div class="form-group">
            <label>Số lần chọc hút</label>
            <input type="number" formControlName="aspirationNo" min="1"/>
          </div>
        </div>
      </div>

      <div class="form-section">
        <h3>Xét nghiệm</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>LH</label>
            <input type="number" formControlName="lhLab" min="0" step="0.01"/>
          </div>
          <div class="form-group">
            <label>E2</label>
            <input type="number" formControlName="e2Lab" min="0" step="0.01"/>
          </div>
          <div class="form-group">
            <label>P4</label>
            <input type="number" formControlName="p4Lab" min="0" step="0.01"/>
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
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; }
    .drug-grid { display: flex; flex-direction: column; gap: 0.75rem; margin-bottom: 0.75rem; }
    .drug-row { display: grid; grid-template-columns: 2fr 1fr 1.5fr auto; gap: 0.75rem; padding: 0.5rem; background: var(--surface); border-radius: 6px; align-items: end; }
    .drug-actions { justify-content: flex-end; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input { padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; background: var(--surface); font-size: 0.9rem; }
    .form-actions { display: flex; justify-content: flex-end; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-primary:disabled { opacity: 0.6; }
    .btn-secondary { background: var(--surface-elevated); color: var(--text-primary); border: 1px dashed var(--border); }
    .btn-sm { padding: 0.35rem 0.75rem; font-size: 0.85rem; }
    .btn-icon { padding: 0.25rem 0.5rem; font-size: 0.8rem; line-height: 1; }
    .btn-danger { background: transparent; color: var(--danger, #e53e3e); }
  `]
})
export class StimulationTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);

  form!: FormGroup;
  loading = false;

  get drugsArray(): FormArray {
    return this.form.get('drugs') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      lastMenstruation: [''],
      startDate: [''],
      startDay: [null],
      drugs: this.fb.array([]),
      size12Follicle: [null],
      size14Follicle: [null],
      endometriumThickness: [null],
      triggerDrug: [''],
      triggerDrug2: [''],
      hcgDate: [''],
      hcgTime: [''],
      aspirationDate: [''],
      aspirationNo: [null],
      lhLab: [null],
      e2Lab: [null],
      p4Lab: [null]
    });

    this.loadData();
  }

  createDrugGroup(drug?: { drugName?: string; duration?: number; posology?: string }): FormGroup {
    return this.fb.group({
      drugName: [drug?.drugName || ''],
      duration: [drug?.duration || 0],
      posology: [drug?.posology || '']
    });
  }

  addDrug(): void {
    this.drugsArray.push(this.createDrugGroup());
  }

  removeDrug(index: number): void {
    this.drugsArray.removeAt(index);
  }

  loadData(): void {
    this.cycleService.getCycleStimulation(this.cycleId).subscribe({
      next: (data) => this.patchForm(data),
      error: () => { }
    });
  }

  patchForm(data: any): void {
    if (!data) return;
    this.form.patchValue({
      lastMenstruation: data.lastMenstruation?.split('T')[0] || '',
      startDate: data.startDate?.split('T')[0] || '',
      startDay: data.startDay,
      size12Follicle: data.size12Follicle,
      size14Follicle: data.size14Follicle,
      endometriumThickness: data.endometriumThickness,
      triggerDrug: data.triggerDrug || '',
      triggerDrug2: data.triggerDrug2 || '',
      hcgDate: data.hcgDate?.split('T')[0] || '',
      hcgTime: data.hcgTime || '',
      aspirationDate: data.aspirationDate?.split('T')[0] || '',
      aspirationNo: data.aspirationNo,
      lhLab: data.lhLab,
      e2Lab: data.e2Lab,
      p4Lab: data.p4Lab
    });

    // Populate drugs FormArray
    this.drugsArray.clear();
    if (data.drugs?.length) {
      data.drugs.forEach((d: any) => this.drugsArray.push(this.createDrugGroup(d)));
    } else {
      // Default 4 empty drug rows for new records
      for (let i = 0; i < 4; i++) this.addDrug();
    }
  }

  onSubmit(): void {
    if (this.loading) return;
    this.loading = true;

    const formValue = this.form.value;
    // Build payload with drugs array
    const payload: any = { ...formValue };
    // Convert empty strings to null for scalar fields
    Object.keys(payload).forEach(key => {
      if (key !== 'drugs' && payload[key] === '') {
        payload[key] = null;
      }
    });
    // Filter out empty drug rows and assign sortOrder
    payload.drugs = (formValue.drugs || [])
      .filter((d: any) => d.drugName?.trim())
      .map((d: any, i: number) => ({ ...d, sortOrder: i }));

    this.cycleService.updateCycleStimulation(this.cycleId, payload).subscribe({
      next: () => { this.loading = false; this.saved.emit(); },
      error: () => { this.loading = false; }
    });
  }
}
