import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScheduleItem } from '../lab-dashboard.models';

@Component({
    selector: 'app-lab-schedule',
    standalone: true,
    imports: [CommonModule],
    template: `
    <section class="content-section card">
      <div class="section-header">
        <h2>Lịch thủ thuật - {{ currentDate | date:'dd/MM/yyyy' }}</h2>
      </div>
      <div class="schedule-grid">
        <div class="schedule-column">
          <h3>🥚 Chọc hút ({{ getByType('retrieval').length }})</h3>
          @for (item of getByType('retrieval'); track item.id) {
            <div class="schedule-item retrieval" [class.done]="item.status === 'done'">
              <span class="time">{{ item.time }}</span>
              <div class="details">
                <strong>{{ item.patientName }}</strong>
                <span>{{ item.cycleCode }}</span>
              </div>
              <button class="btn-status" (click)="onToggle(item)">{{ item.status === 'done' ? '✓' : '○' }}</button>
            </div>
          } @empty { <div class="empty-schedule">Không có lịch</div> }
        </div>

        <div class="schedule-column">
          <h3>💉 Chuyển phôi ({{ getByType('transfer').length }})</h3>
          @for (item of getByType('transfer'); track item.id) {
            <div class="schedule-item transfer" [class.done]="item.status === 'done'">
              <span class="time">{{ item.time }}</span>
              <div class="details">
                <strong>{{ item.patientName }}</strong>
                <span>{{ item.cycleCode }} - {{ item.procedure }}</span>
              </div>
              <button class="btn-status" (click)="onToggle(item)">{{ item.status === 'done' ? '✓' : '○' }}</button>
            </div>
          } @empty { <div class="empty-schedule">Không có lịch</div> }
        </div>

        <div class="schedule-column">
          <h3>📋 Báo phôi ({{ getByType('report').length }})</h3>
          @for (item of getByType('report'); track item.id) {
            <div class="schedule-item report" [class.done]="item.status === 'done'">
              <span class="time">{{ item.time }}</span>
              <div class="details">
                <strong>{{ item.patientName }}</strong>
                <span>{{ item.cycleCode }} - {{ item.procedure }}</span>
              </div>
              <button class="btn-status" (click)="onToggle(item)">{{ item.status === 'done' ? '✓' : '○' }}</button>
            </div>
          } @empty { <div class="empty-schedule">Không có lịch</div> }
        </div>
      </div>
    </section>
  `,
    styles: [`
    .schedule-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1.5rem; }
    .schedule-column h3 { font-size: 0.9rem; color: var(--text-main); margin: 0 0 1rem; }
    .schedule-item { 
      display: flex; align-items: center; gap: 0.75rem; padding: 0.75rem; 
      background: var(--bg-card); border-radius: var(--radius-md); 
      margin-bottom: 0.5rem; border-left: 3px solid var(--primary);
    }
    .schedule-item.retrieval { border-left-color: var(--warning); }
    .schedule-item.transfer { border-left-color: var(--success); }
    .schedule-item.report { border-left-color: var(--secondary); }
    .schedule-item.done { opacity: 0.6; }
    
    .time { font-weight: 600; color: var(--primary); font-size: 0.8rem; min-width: 45px; }
    .details { flex: 1; }
    .details strong { display: block; font-size: 0.8rem; }
    .details span { font-size: 0.7rem; color: var(--text-secondary); }
    
    .btn-status { 
      width: 24px; height: 24px; border-radius: 50%; border: 2px solid var(--primary); 
      background: white; cursor: pointer; font-size: 0.75rem; color: var(--primary); 
      display: flex; align-items: center; justify-content: center;
    }
    .empty-schedule { text-align: center; color: var(--text-light); padding: 1rem; font-size: 0.8rem; }
  `]
})
export class LabScheduleComponent {
    @Input() schedule: ScheduleItem[] = [];
    @Input() currentDate: Date = new Date();
    @Output() toggleStatus = new EventEmitter<ScheduleItem>();

    getByType(type: string): ScheduleItem[] {
        return this.schedule.filter(s => s.type === type);
    }

    onToggle(item: ScheduleItem) {
        this.toggleStatus.emit(item);
    }
}
