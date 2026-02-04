import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-ultrasound-form',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="ultrasound-form">
      <header class="page-header">
        <a (click)="goBack()" class="back-link">← Quay lại</a>
        <h1>Ghi nhận siêu âm</h1>
      </header>

      <form (ngSubmit)="submit()" class="form-card">
        <div class="form-row">
          <div class="form-group">
            <label>Loại siêu âm</label>
            <select [(ngModel)]="formData.ultrasoundType" name="type" required>
              <option value="NangNoan">Siêu âm nang noãn</option>
              <option value="PhụKhoa">Siêu âm phụ khoa</option>
              <option value="NMTC">Siêu âm NMTC</option>
              <option value="Thai">Siêu âm thai</option>
            </select>
          </div>
          <div class="form-group">
            <label>Ngày siêu âm</label>
            <input type="date" [(ngModel)]="formData.examDate" name="examDate" required />
          </div>
        </div>

        <h3>Buồng trứng</h3>
        <div class="form-row">
          <div class="form-group">
            <label>Số nang bên trái</label>
            <input type="number" [(ngModel)]="formData.leftOvaryCount" name="leftCount" min="0" />
          </div>
          <div class="form-group">
            <label>Số nang bên phải</label>
            <input type="number" [(ngModel)]="formData.rightOvaryCount" name="rightCount" min="0" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Kích thước nang trái (mm)</label>
            <input type="text" [(ngModel)]="formData.leftFollicles" name="leftFollicles" placeholder="18, 16, 14, 12" />
          </div>
          <div class="form-group">
            <label>Kích thước nang phải (mm)</label>
            <input type="text" [(ngModel)]="formData.rightFollicles" name="rightFollicles" placeholder="17, 15, 13" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Độ dày NMTC (mm)</label>
            <input type="number" [(ngModel)]="formData.endometriumThickness" name="endo" step="0.1" min="0" />
          </div>
          <div class="form-group"></div>
        </div>

        <div class="form-group full-width">
          <label>Ghi chú / Kết luận</label>
          <textarea [(ngModel)]="formData.findings" name="findings" rows="4"></textarea>
        </div>

        <div class="form-actions">
          <button type="button" class="btn-cancel" (click)="goBack()">Huỷ</button>
          <button type="submit" class="btn-submit" [disabled]="saving()">
            {{ saving() ? 'Đang lưu...' : 'Lưu siêu âm' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .ultrasound-form { max-width: 800px; margin: 0 auto; }

    .page-header { margin-bottom: 2rem; }
    .back-link { color: #6b7280; cursor: pointer; font-size: 0.875rem; display: inline-block; margin-bottom: 0.5rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .form-card {
      background: white;
      border-radius: 16px;
      padding: 2rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    h3 { font-size: 1rem; color: #374151; margin: 1.5rem 0 1rem; border-bottom: 1px solid #f1f5f9; padding-bottom: 0.5rem; }

    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; }

    .form-group { display: flex; flex-direction: column; }
    .form-group.full-width { grid-column: 1 / -1; }

    label { font-size: 0.875rem; color: #374151; margin-bottom: 0.5rem; font-weight: 500; }

    input, select, textarea {
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 1rem;
    }

    input:focus, select:focus, textarea:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
    }

    .form-actions {
      display: flex;
      gap: 1rem;
      justify-content: flex-end;
      margin-top: 2rem;
      padding-top: 1.5rem;
      border-top: 1px solid #f1f5f9;
    }

    .btn-cancel, .btn-submit {
      padding: 0.75rem 1.5rem;
      border: none;
      border-radius: 8px;
      font-weight: 500;
      cursor: pointer;
    }

    .btn-cancel { background: #f1f5f9; color: #374151; }
    .btn-submit { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }
    .btn-submit:disabled { opacity: 0.6; cursor: not-allowed; }
  `]
})
export class UltrasoundFormComponent implements OnInit {
    saving = signal(false);
    cycleId = '';

    formData = {
        ultrasoundType: 'NangNoan',
        examDate: new Date().toISOString().split('T')[0],
        leftOvaryCount: null as number | null,
        rightOvaryCount: null as number | null,
        leftFollicles: '',
        rightFollicles: '',
        endometriumThickness: null as number | null,
        findings: ''
    };

    constructor(
        private route: ActivatedRoute,
        private router: Router,
        private api: ApiService
    ) { }

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            this.cycleId = params['cycleId'];
        });
    }

    submit(): void {
        this.saving.set(true);
        this.api.createUltrasound({
            cycleId: this.cycleId,
            ...this.formData
        } as any).subscribe({
            next: () => {
                this.saving.set(false);
                this.goBack();
            },
            error: () => this.saving.set(false)
        });
    }

    goBack(): void {
        this.router.navigate(['/cycles', this.cycleId]);
    }
}
