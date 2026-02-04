import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-cycle-form',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="cycle-form">
      <header class="page-header">
        <a (click)="goBack()" class="back-link">← Quay lại</a>
        <h1>Tạo chu kỳ mới</h1>
      </header>

      <form (ngSubmit)="submit()" class="form-card">
        <div class="form-group">
          <label>Phương pháp điều trị *</label>
          <div class="method-grid">
            @for (method of methods; track method.value) {
              <label class="method-option" [class.selected]="formData.method === method.value">
                <input type="radio" [(ngModel)]="formData.method" [value]="method.value" name="method" />
                <span class="method-icon">{{ method.icon }}</span>
                <span class="method-name">{{ method.label }}</span>
                <span class="method-desc">{{ method.desc }}</span>
              </label>
            }
          </div>
        </div>

        <div class="form-group">
          <label>Ngày bắt đầu *</label>
          <input type="date" [(ngModel)]="formData.startDate" name="startDate" required />
        </div>

        <div class="form-group">
          <label>Ghi chú</label>
          <textarea [(ngModel)]="formData.notes" name="notes" rows="3" placeholder="Ghi chú về chu kỳ điều trị..."></textarea>
        </div>

        <div class="form-actions">
          <button type="button" class="btn-cancel" (click)="goBack()">Huỷ</button>
          <button type="submit" class="btn-submit" [disabled]="saving() || !formData.method">
            {{ saving() ? 'Đang tạo...' : 'Tạo chu kỳ' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .cycle-form { max-width: 600px; margin: 0 auto; }

    .page-header { margin-bottom: 2rem; }
    .back-link { color: #6b7280; cursor: pointer; font-size: 0.875rem; display: inline-block; margin-bottom: 0.5rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .form-card {
      background: white;
      border-radius: 16px;
      padding: 2rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .form-group { margin-bottom: 1.5rem; }
    label { display: block; font-size: 0.875rem; color: #374151; margin-bottom: 0.5rem; font-weight: 500; }

    .method-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; }

    .method-option {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 1.5rem;
      border: 2px solid #e2e8f0;
      border-radius: 12px;
      cursor: pointer;
      transition: all 0.2s;
      text-align: center;
    }

    .method-option:hover { border-color: #667eea; }
    .method-option.selected { border-color: #667eea; background: linear-gradient(135deg, rgba(102,126,234,0.1), rgba(118,75,162,0.1)); }
    .method-option input { display: none; }

    .method-icon { font-size: 2rem; margin-bottom: 0.5rem; }
    .method-name { font-weight: 600; color: #1e1e2f; margin-bottom: 0.25rem; }
    .method-desc { font-size: 0.75rem; color: #6b7280; }

    input[type="date"], textarea {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 1rem;
    }

    input:focus, textarea:focus {
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
export class CycleFormComponent implements OnInit {
    saving = signal(false);
    coupleId = '';

    methods = [
        { value: 'QHTN', label: 'Quan hệ', icon: '💑', desc: 'Tự nhiên / KTBT' },
        { value: 'IUI', label: 'IUI', icon: '💉', desc: 'Bơm tinh trùng' },
        { value: 'ICSI', label: 'ICSI', icon: '🔬', desc: 'Thụ tinh vi thao tác' },
        { value: 'IVM', label: 'IVM', icon: '🧫', desc: 'Trưởng thành in vitro' }
    ];

    formData = {
        method: '',
        startDate: new Date().toISOString().split('T')[0],
        notes: ''
    };

    constructor(
        private route: ActivatedRoute,
        private router: Router,
        private api: ApiService
    ) { }

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            this.coupleId = params['coupleId'];
        });
    }

    submit(): void {
        if (!this.formData.method || !this.coupleId) return;

        this.saving.set(true);
        this.api.createCycle({
            coupleId: this.coupleId,
            method: this.formData.method,
            notes: this.formData.notes || undefined
        }).subscribe({
            next: (cycle) => {
                this.saving.set(false);
                this.router.navigate(['/cycles', cycle.id]);
            },
            error: () => this.saving.set(false)
        });
    }

    goBack(): void {
        history.back();
    }
}
