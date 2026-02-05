import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';
import { TreatmentIndication } from '../../../../core/models/api.models';

@Component({
    selector: 'app-indication-tab',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="phase-form">
      <div class="form-section">
        <h3>Thông tin chỉ định</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Ngày kinh cuối</label>
            <input type="date" formControlName="lastMenstruation" />
          </div>
          <div class="form-group">
            <label>Loại điều trị</label>
            <select formControlName="treatmentType">
              <option value="">-- Chọn --</option>
              <option value="IVF">IVF</option>
              <option value="ICSI">ICSI</option>
              <option value="IUI">IUI</option>
              <option value="FET">FET</option>
            </select>
          </div>
          <div class="form-group">
            <label>Phác đồ</label>
            <select formControlName="regimen">
              <option value="">-- Chọn --</option>
              <option value="Long">Long protocol</option>
              <option value="Short">Short protocol</option>
              <option value="Antagonist">Antagonist</option>
              <option value="Natural">Tự nhiên</option>
            </select>
          </div>
          <div class="form-group">
            <label>Loại phụ</label>
            <input type="text" formControlName="subType" placeholder="Loại phụ"/>
          </div>
        </div>
      </div>

      <div class="form-section">
        <h3>Chẩn đoán</h3>
        <div class="form-grid">
          <div class="form-group full-width">
            <label>Chẩn đoán vợ</label>
            <input type="text" formControlName="wifeDiagnosis" placeholder="Chẩn đoán chính"/>
          </div>
          <div class="form-group full-width">
            <label>Chẩn đoán vợ 2</label>
            <input type="text" formControlName="wifeDiagnosis2" placeholder="Chẩn đoán phụ"/>
          </div>
          <div class="form-group full-width">
            <label>Chẩn đoán chồng</label>
            <input type="text" formControlName="husbandDiagnosis" placeholder="Chẩn đoán chính"/>
          </div>
          <div class="form-group full-width">
            <label>Chẩn đoán chồng 2</label>
            <input type="text" formControlName="husbandDiagnosis2" placeholder="Chẩn đoán phụ"/>
          </div>
        </div>
      </div>

      <div class="form-section">
        <h3>Các tuỳ chọn</h3>
        <div class="checkbox-grid">
          <label class="checkbox-item">
            <input type="checkbox" formControlName="freezeAll"/> Trữ toàn bộ
          </label>
          <label class="checkbox-item">
            <input type="checkbox" formControlName="sis"/> SIS
          </label>
          <label class="checkbox-item">
            <input type="checkbox" formControlName="timelapse"/> Timelapse
          </label>
          <label class="checkbox-item">
            <input type="checkbox" formControlName="pgtA"/> PGT-A
          </label>
          <label class="checkbox-item">
            <input type="checkbox" formControlName="pgtSr"/> PGT-SR
          </label>
          <label class="checkbox-item">
            <input type="checkbox" formControlName="pgtM"/> PGT-M
          </label>
        </div>
      </div>

      <div class="form-section">
        <h3>Nguồn gốc</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>Nguồn</label>
            <input type="text" formControlName="source" placeholder="Nguồn giới thiệu"/>
          </div>
          <div class="form-group">
            <label>Nơi thực hiện</label>
            <input type="text" formControlName="procedurePlace" placeholder="Cơ sở"/>
          </div>
          <div class="form-group">
            <label>Số lần điều trị tại đây</label>
            <input type="number" formControlName="previousTreatmentsAtSite" min="0"/>
          </div>
          <div class="form-group">
            <label>Số lần điều trị nơi khác</label>
            <input type="number" formControlName="previousTreatmentsOther" min="0"/>
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
    .form-section h3 { margin: 0 0 1rem; font-size: 1rem; color: var(--text-primary); }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input, .form-group select { 
      padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; 
      background: var(--surface); font-size: 0.9rem;
    }
    .checkbox-grid { display: flex; flex-wrap: wrap; gap: 1rem; }
    .checkbox-item { display: flex; align-items: center; gap: 0.5rem; font-size: 0.9rem; cursor: pointer; }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.5rem; padding-top: 1rem; border-top: 1px solid var(--border); }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
  `]
})
export class IndicationTabComponent implements OnInit {
    @Input() cycleId!: string;
    @Output() saved = new EventEmitter<void>();

    private fb = inject(FormBuilder);
    private cycleService = inject(CycleService);

    form!: FormGroup;
    loading = false;

    ngOnInit(): void {
        this.form = this.fb.group({
            lastMenstruation: [''],
            treatmentType: [''],
            regimen: [''],
            subType: [''],
            wifeDiagnosis: [''],
            wifeDiagnosis2: [''],
            husbandDiagnosis: [''],
            husbandDiagnosis2: [''],
            freezeAll: [false],
            sis: [false],
            timelapse: [false],
            pgtA: [false],
            pgtSr: [false],
            pgtM: [false],
            source: [''],
            procedurePlace: [''],
            previousTreatmentsAtSite: [0],
            previousTreatmentsOther: [0]
        });

        this.loadData();
    }

    loadData(): void {
        this.cycleService.getCycleIndication(this.cycleId).subscribe({
            next: (data) => {
                if (data) {
                    this.form.patchValue({
                        lastMenstruation: data.lastMenstruation?.split('T')[0] || '',
                        treatmentType: data.treatmentType || '',
                        regimen: data.regimen || '',
                        subType: data.subType || '',
                        wifeDiagnosis: data.wifeDiagnosis || '',
                        wifeDiagnosis2: data.wifeDiagnosis2 || '',
                        husbandDiagnosis: data.husbandDiagnosis || '',
                        husbandDiagnosis2: data.husbandDiagnosis2 || '',
                        freezeAll: data.freezeAll,
                        sis: data.sis,
                        timelapse: data.timelapse,
                        pgtA: data.pgtA,
                        pgtSr: data.pgtSr,
                        pgtM: data.pgtM,
                        source: data.source || '',
                        procedurePlace: data.procedurePlace || '',
                        previousTreatmentsAtSite: data.previousTreatmentsAtSite,
                        previousTreatmentsOther: data.previousTreatmentsOther
                    });
                }
            },
            error: () => { /* no existing data */ }
        });
    }

    onSubmit(): void {
        if (this.loading) return;
        this.loading = true;

        const formVal = this.form.value;
        this.cycleService.updateCycleIndication(this.cycleId, {
            ...formVal,
            lastMenstruation: formVal.lastMenstruation || undefined
        }).subscribe({
            next: () => {
                this.loading = false;
                this.saved.emit();
            },
            error: () => { this.loading = false; }
        });
    }
}
