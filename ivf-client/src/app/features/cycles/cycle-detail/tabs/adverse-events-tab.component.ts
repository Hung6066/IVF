import { Component, Input, Output, EventEmitter, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CycleService } from '../../../../core/services/cycle.service';
import { AdverseEventData } from '../../../../core/models/api.models';

@Component({
  selector: 'app-adverse-events-tab',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="phase-form">
      <div class="form-section">
        <div class="section-header">
          <h3>Danh sách biến chứng</h3>
          <button type="button" class="btn btn-sm btn-primary" (click)="showForm = !showForm">
            {{ showForm ? '✕ Đóng' : '+ Thêm biến chứng' }}
          </button>
        </div>

        @if (showForm) {
        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="add-form">
          <div class="form-grid">
            <div class="form-group">
              <label>Ngày xảy ra</label>
              <input type="date" formControlName="eventDate"/>
            </div>
            <div class="form-group">
              <label>Loại biến chứng</label>
              <select formControlName="eventType">
                <option value="">-- Chọn --</option>
                <option value="OHSS">OHSS</option>
                <option value="Bleeding">Chảy máu</option>
                <option value="Infection">Nhiễm trùng</option>
                <option value="Ectopic">Thai ngoài tử cung</option>
                <option value="Miscarriage">Sảy thai</option>
                <option value="Other">Khác</option>
              </select>
            </div>
            <div class="form-group">
              <label>Mức độ</label>
              <select formControlName="severity">
                <option value="">-- Chọn --</option>
                <option value="Mild">Nhẹ</option>
                <option value="Moderate">Trung bình</option>
                <option value="Severe">Nặng</option>
              </select>
            </div>
          </div>
          <div class="form-grid">
            <div class="form-group full-width">
              <label>Mô tả</label>
              <textarea formControlName="description" rows="2" placeholder="Mô tả chi tiết..."></textarea>
            </div>
            <div class="form-group full-width">
              <label>Điều trị</label>
              <textarea formControlName="treatment" rows="2" placeholder="Phương pháp điều trị..."></textarea>
            </div>
            <div class="form-group">
              <label>Kết quả</label>
              <input type="text" formControlName="outcome" placeholder="Kết quả điều trị"/>
            </div>
          </div>
          <div class="form-actions-inline">
            <button type="submit" class="btn btn-primary" [disabled]="loading">
              {{ loading ? 'Đang lưu...' : '💾 Lưu' }}
            </button>
          </div>
        </form>
        }

        <div class="events-list">
          @for (event of events(); track event.id) {
          <div class="event-card" [class]="event.severity?.toLowerCase()">
            <div class="event-header">
              <span class="event-type">{{ getEventTypeName(event.eventType) }}</span>
              <span class="event-severity">{{ getSeverityName(event.severity) }}</span>
              <span class="event-date">{{ formatDate(event.eventDate) }}</span>
            </div>
            <p class="event-desc">{{ event.description }}</p>
            @if (event.treatment) {
            <p class="event-treatment"><strong>Điều trị:</strong> {{ event.treatment }}</p>
            }
            @if (event.outcome) {
            <p class="event-outcome"><strong>Kết quả:</strong> {{ event.outcome }}</p>
            }
          </div>
          } @empty {
          <p class="empty-state">Chưa có biến chứng nào được ghi nhận</p>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .phase-form { padding: 1rem; }
    .form-section { padding: 1rem; background: var(--surface-elevated); border-radius: 8px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .section-header h3 { margin: 0; font-size: 1rem; }
    .add-form { padding: 1rem; background: var(--surface); border-radius: 8px; margin-bottom: 1rem; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; margin-bottom: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.25rem; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; color: var(--text-secondary); }
    .form-group input, .form-group select, .form-group textarea { 
      padding: 0.5rem; border: 1px solid var(--border); border-radius: 6px; 
      background: var(--surface-elevated); font-size: 0.9rem; font-family: inherit;
    }
    .form-actions-inline { display: flex; justify-content: flex-end; }
    .events-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .event-card { padding: 1rem; background: var(--surface); border-radius: 8px; border-left: 4px solid var(--border); }
    .event-card.mild { border-left-color: #10b981; }
    .event-card.moderate { border-left-color: #f59e0b; }
    .event-card.severe { border-left-color: #ef4444; }
    .event-header { display: flex; gap: 1rem; align-items: center; margin-bottom: 0.5rem; flex-wrap: wrap; }
    .event-type { font-weight: 600; color: var(--text-primary); }
    .event-severity { font-size: 0.8rem; padding: 2px 8px; border-radius: 12px; background: var(--surface-elevated); }
    .event-date { font-size: 0.85rem; color: var(--text-secondary); margin-left: auto; }
    .event-desc, .event-treatment, .event-outcome { margin: 0.25rem 0; font-size: 0.9rem; color: var(--text-secondary); }
    .empty-state { text-align: center; color: var(--text-tertiary); padding: 2rem; }
    .btn { padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; border: none; font-size: 0.9rem; }
    .btn-sm { padding: 0.35rem 0.75rem; font-size: 0.85rem; }
    .btn-primary { background: var(--primary); color: white; }
  `]
})
export class AdverseEventsTabComponent implements OnInit {
  @Input() cycleId!: string;
  @Output() saved = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private cycleService = inject(CycleService);

  form!: FormGroup;
  events = signal<AdverseEventData[]>([]);
  loading = false;
  showForm = false;

  ngOnInit(): void {
    this.form = this.fb.group({
      eventDate: [''],
      eventType: [''],
      severity: [''],
      description: [''],
      treatment: [''],
      outcome: ['']
    });
    this.loadData();
  }

  loadData(): void {
    this.cycleService.getCycleAdverseEvents(this.cycleId).subscribe({
      next: (data) => this.events.set(data || []),
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

    this.cycleService.createCycleAdverseEvent(this.cycleId, formValue).subscribe({
      next: () => {
        this.loading = false;
        this.form.reset();
        this.showForm = false;
        this.loadData();
        this.saved.emit();
      },
      error: () => { this.loading = false; }
    });
  }

  getEventTypeName(type?: string): string {
    const names: Record<string, string> = {
      'OHSS': 'OHSS', 'Bleeding': 'Chảy máu', 'Infection': 'Nhiễm trùng',
      'Ectopic': 'Thai ngoài TC', 'Miscarriage': 'Sảy thai', 'Other': 'Khác'
    };
    return names[type || ''] || type || '';
  }

  getSeverityName(severity?: string): string {
    const names: Record<string, string> = { 'Mild': 'Nhẹ', 'Moderate': 'Trung bình', 'Severe': 'Nặng' };
    return names[severity || ''] || severity || '';
  }

  formatDate(date?: string): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString('vi-VN');
  }
}
