import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EmbryoCard } from '../lab-dashboard.models';

@Component({
    selector: 'app-lab-embryos',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="content-section card">
      <div class="section-header">
        <h2>Bảng theo dõi phôi - {{ currentDate | date:'dd/MM' }}</h2>
        <div class="filters">
          <select [(ngModel)]="filterDay" class="form-control" style="width: auto;">
            <option value="all">Tất cả</option>
            <option value="D3">Ngày 3</option>
            <option value="D5">Ngày 5</option>
            <option value="D6">Ngày 6</option>
          </select>
        </div>
      </div>
      <div class="embryo-grid">
        @for (embryo of filteredEmbryos(); track embryo.id) {
          <div class="embryo-card" [class]="getCardClass(embryo.status)" (click)="onSelect(embryo)">
            <div class="embryo-header">
              <span class="cycle">{{ embryo.cycleCode }}</span>
              <span class="day-badge">{{ embryo.day }}</span>
            </div>
            <div class="embryo-number">#{{ embryo.embryoNumber }}</div>
            <div class="embryo-grade">{{ embryo.grade }}</div>
            <div class="embryo-patient">{{ embryo.patientName }}</div>
            <div class="embryo-status">{{ getStatusName(embryo.status) }}</div>
            @if (embryo.location) {
              <div class="embryo-location">📍 {{ embryo.location }}</div>
            }
          </div>
        } @empty {
          <div class="empty-full">Không có phôi đang nuôi</div>
        }
      </div>
    </section>
  `,
    styles: [`
    .embryo-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: 1rem;
    }
    .embryo-card {
      background: var(--bg-card);
      border-radius: var(--radius-md);
      padding: 1rem;
      text-align: center;
      border: 2px solid transparent;
      cursor: pointer;
      transition: all 0.2s;

      &:hover {
        transform: translateY(-2px);
        box-shadow: var(--shadow-md);
      }

      &.developing { border-color: var(--warning); }
      &.frozen { border-color: var(--info); background: #ecfeff; }
      &.transferred { border-color: var(--success); background: #ecfdf5; }
    }
    .embryo-header { display: flex; justify-content: space-between; font-size: 0.7rem; margin-bottom: 0.5rem; }
    .cycle { color: var(--text-secondary); }
    .day-badge { background: var(--primary); color: white; padding: 0.125rem 0.375rem; border-radius: 4px; font-size: 0.625rem; }
    .embryo-number { font-size: 0.8rem; color: var(--text-secondary); }
    .embryo-grade { font-size: 1.5rem; font-weight: 700; color: var(--text-main); }
    .embryo-patient { font-size: 0.75rem; color: var(--text-secondary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin: 0.25rem 0; }
    .embryo-status { font-size: 0.65rem; color: var(--text-light); }
    .embryo-location { font-size: 0.65rem; color: var(--primary); margin-top: 0.25rem; }
    .empty-full { grid-column: 1 / -1; text-align: center; color: var(--text-light); padding: 3rem; }
  `]
})
export class LabEmbryosComponent {
    @Input() embryos: EmbryoCard[] = [];
    @Input() currentDate: Date = new Date();
    @Output() selectEmbryo = new EventEmitter<EmbryoCard>();

    filterDay = signal('all'); // Using signal for local state

    // Computed signal for derivation
    filteredEmbryos = computed(() => {
        const filter = this.filterDay();
        if (filter === 'all') return this.embryos;
        return this.embryos.filter(e => e.day === filter);
    });

    getCardClass(status: string): string {
        return status.toLowerCase();
    }

    getStatusName(status: string): string {
        const names: Record<string, string> = {
            'Developing': 'Đang nuôi',
            'Frozen': 'Đông lạnh',
            'Transferred': 'Đã chuyển',
            'Discarded': 'Loại bỏ'
        };
        return names[status] || status;
    }

    onSelect(embryo: EmbryoCard) {
        this.selectEmbryo.emit(embryo);
    }
}
