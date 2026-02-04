import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { TreatmentCycle, Embryo, Ultrasound } from '../../../core/models/api.models';

@Component({
    selector: 'app-cycle-detail',
    standalone: true,
    imports: [CommonModule, RouterModule],
    template: `
    <div class="cycle-detail">
      <header class="page-header">
        <div class="header-info">
          <a routerLink="/couples" class="back-link">← Quay lại</a>
          <h1>Chu kỳ {{ cycle()?.cycleCode }}</h1>
          <div class="cycle-meta">
            <span class="badge method">{{ getMethodName(cycle()?.method) }}</span>
            <span class="badge phase">{{ getPhaseName(cycle()?.phase) }}</span>
            <span class="badge outcome" [class]="(cycle()?.outcome || '').toLowerCase()">
              {{ getOutcomeName(cycle()?.outcome) }}
            </span>
          </div>
        </div>
      </header>

      <div class="content-grid">
        <!-- Timeline -->
        <section class="timeline-section">
          <h2>Tiến trình điều trị</h2>
          <div class="phase-timeline">
            @for (phase of phases; track phase.key; let i = $index) {
              <div class="phase-item" [class.active]="isActivePhase(phase.key)" [class.completed]="isCompletedPhase(phase.key, i)">
                <div class="phase-dot"></div>
                <span class="phase-label">{{ phase.name }}</span>
              </div>
            }
          </div>
          @if (cycle()?.outcome === 'Ongoing') {
            <button class="btn-advance" (click)="advancePhase()">⏭️ Chuyển giai đoạn tiếp</button>
          }
        </section>

        <!-- Ultrasounds -->
        <section class="ultrasound-section">
          <h2>Siêu âm ({{ ultrasounds().length }})</h2>
          <div class="ultrasound-list">
            @for (us of ultrasounds(); track us.id) {
              <div class="ultrasound-card">
                <div class="us-header">
                  <span class="us-date">{{ formatDate(us.examDate) }}</span>
                  <span class="us-day">Ngày {{ us.dayOfCycle || '?' }}</span>
                </div>
                <div class="us-stats">
                  <div class="stat">
                    <span class="value">{{ us.leftOvaryCount || 0 }}</span>
                    <span class="label">Trái</span>
                  </div>
                  <div class="stat">
                    <span class="value">{{ us.rightOvaryCount || 0 }}</span>
                    <span class="label">Phải</span>
                  </div>
                  <div class="stat">
                    <span class="value">{{ us.endometriumThickness || 0 }}mm</span>
                    <span class="label">NMTC</span>
                  </div>
                </div>
              </div>
            } @empty {
              <p class="empty">Chưa có siêu âm</p>
            }
          </div>
        </section>

        <!-- Embryos -->
        <section class="embryo-section">
          <h2>Phôi ({{ embryos().length }})</h2>
          <div class="embryo-grid">
            @for (embryo of embryos(); track embryo.id) {
              <div class="embryo-card" [class]="(embryo.status || '').toLowerCase()">
                <div class="embryo-number">#{{ embryo.embryoNumber }}</div>
                <div class="embryo-grade">{{ embryo.grade || '?' }}</div>
                <div class="embryo-day">{{ embryo.day }}</div>
                <div class="embryo-status">{{ getEmbryoStatus(embryo.status) }}</div>
              </div>
            } @empty {
              <p class="empty">Chưa có phôi</p>
            }
          </div>
        </section>
      </div>
    </div>
  `,
    styles: [`
    .cycle-detail { max-width: 1400px; }

    .page-header { margin-bottom: 2rem; }
    .back-link { color: #6b7280; text-decoration: none; font-size: 0.875rem; }
    .page-header h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0.5rem 0; }

    .cycle-meta { display: flex; gap: 0.5rem; flex-wrap: wrap; }

    .badge {
      padding: 0.25rem 0.75rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 500;
    }

    .badge.method { background: #dbeafe; color: #1d4ed8; }
    .badge.phase { background: #fef3c7; color: #92400e; }
    .badge.ongoing { background: #d1fae5; color: #065f46; }
    .badge.pregnant { background: #10b981; color: white; }
    .badge.notpregnant { background: #fecaca; color: #991b1b; }
    .badge.cancelled { background: #e5e7eb; color: #374151; }

    .content-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
    }

    .timeline-section, .ultrasound-section, .embryo-section {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .timeline-section { grid-column: 1 / -1; }

    h2 { font-size: 1rem; color: #374151; margin: 0 0 1rem; }

    .phase-timeline {
      display: flex;
      justify-content: space-between;
      position: relative;
      margin-bottom: 1.5rem;
    }

    .phase-timeline::before {
      content: '';
      position: absolute;
      top: 12px;
      left: 0;
      right: 0;
      height: 2px;
      background: #e5e7eb;
    }

    .phase-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      z-index: 1;
    }

    .phase-dot {
      width: 24px;
      height: 24px;
      border-radius: 50%;
      background: #e5e7eb;
      border: 3px solid white;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
      margin-bottom: 0.5rem;
    }

    .phase-item.active .phase-dot { background: linear-gradient(135deg, #667eea, #764ba2); }
    .phase-item.completed .phase-dot { background: #10b981; }

    .phase-label { font-size: 0.6875rem; color: #6b7280; text-align: center; max-width: 60px; }
    .phase-item.active .phase-label, .phase-item.completed .phase-label { color: #1e1e2f; font-weight: 500; }

    .btn-advance {
      padding: 0.75rem 1.25rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 8px;
      cursor: pointer;
    }

    .ultrasound-list { display: flex; flex-direction: column; gap: 0.75rem; }

    .ultrasound-card {
      background: #f8fafc;
      border-radius: 8px;
      padding: 1rem;
    }

    .us-header { display: flex; justify-content: space-between; margin-bottom: 0.75rem; }
    .us-date { font-weight: 500; }
    .us-day { color: #6b7280; font-size: 0.875rem; }

    .us-stats { display: flex; gap: 1.5rem; }
    .stat { display: flex; flex-direction: column; align-items: center; }
    .stat .value { font-size: 1.25rem; font-weight: 600; color: #1e1e2f; }
    .stat .label { font-size: 0.75rem; color: #6b7280; }

    .embryo-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(100px, 1fr)); gap: 0.75rem; }

    .embryo-card {
      background: #f8fafc;
      border-radius: 12px;
      padding: 1rem;
      text-align: center;
      border: 2px solid transparent;
    }

    .embryo-card.developing { border-color: #fbbf24; }
    .embryo-card.transferred { border-color: #10b981; }
    .embryo-card.frozen { border-color: #06b6d4; }
    .embryo-card.discarded { border-color: #ef4444; opacity: 0.6; }

    .embryo-number { font-size: 0.75rem; color: #6b7280; }
    .embryo-grade { font-size: 1.5rem; font-weight: 700; color: #1e1e2f; }
    .embryo-day { font-size: 0.875rem; color: #6b7280; }
    .embryo-status { font-size: 0.6875rem; margin-top: 0.25rem; color: #6b7280; }

    .empty { color: #9ca3af; text-align: center; padding: 1rem; }
  `]
})
export class CycleDetailComponent implements OnInit {
    cycle = signal<TreatmentCycle | null>(null);
    ultrasounds = signal<Ultrasound[]>([]);
    embryos = signal<Embryo[]>([]);

    phases = [
        { key: 'Consultation', name: 'Tư vấn' },
        { key: 'OvarianStimulation', name: 'Kích thích' },
        { key: 'TriggerShot', name: 'Trigger' },
        { key: 'EggRetrieval', name: 'Chọc hút' },
        { key: 'EmbryoCulture', name: 'Nuôi phôi' },
        { key: 'EmbryoTransfer', name: 'Chuyển phôi' },
        { key: 'LutealSupport', name: 'Hậu chuyển' },
        { key: 'PregnancyTest', name: 'Thử thai' },
        { key: 'Completed', name: 'Hoàn thành' }
    ];

    constructor(private route: ActivatedRoute, private api: ApiService) { }

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            const id = params['id'];
            this.api.getCycle(id).subscribe(c => this.cycle.set(c));
            this.api.getUltrasoundsByCycle(id).subscribe(u => this.ultrasounds.set(u));
            this.api.getEmbryosByCycle(id).subscribe(e => this.embryos.set(e));
        });
    }

    getMethodName(method?: string): string {
        const names: Record<string, string> = { 'QHTN': 'Quan hệ', 'IUI': 'IUI', 'ICSI': 'ICSI', 'IVM': 'IVM' };
        return names[method || ''] || method || '';
    }

    getPhaseName(phase?: string): string {
        return this.phases.find(p => p.key === phase)?.name || phase || '';
    }

    getOutcomeName(outcome?: string): string {
        const names: Record<string, string> = {
            'Ongoing': 'Đang điều trị', 'Pregnant': 'Có thai', 'NotPregnant': 'Không thai',
            'Cancelled': 'Huỷ', 'FrozenAll': 'Trữ phôi toàn bộ'
        };
        return names[outcome || ''] || outcome || '';
    }

    getEmbryoStatus(status: string): string {
        const names: Record<string, string> = {
            'Developing': 'Đang phát triển', 'Transferred': 'Đã chuyển', 'Frozen': 'Đông lạnh',
            'Thawed': 'Đã rã', 'Discarded': 'Loại bỏ', 'Arrested': 'Ngừng phát triển'
        };
        return names[status] || status;
    }

    isActivePhase(phase: string): boolean {
        return this.cycle()?.phase === phase;
    }

    isCompletedPhase(phase: string, index: number): boolean {
        const currentIndex = this.phases.findIndex(p => p.key === this.cycle()?.phase);
        return index < currentIndex;
    }

    advancePhase(): void {
        const currentIndex = this.phases.findIndex(p => p.key === this.cycle()?.phase);
        if (currentIndex < this.phases.length - 1) {
            const nextPhase = this.phases[currentIndex + 1].key;
            this.api.advanceCyclePhase(this.cycle()!.id, nextPhase).subscribe(c => this.cycle.set(c));
        }
    }

    formatDate(date: string): string {
        return new Date(date).toLocaleDateString('vi-VN');
    }
}
