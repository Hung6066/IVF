import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DateViPipe } from '../../pipes/date-vi.pipe';

export interface TimelineEvent {
  date: string | Date;
  title: string;
  description?: string;
  status?: 'completed' | 'current' | 'upcoming' | 'cancelled';
  icon?: string;
}

@Component({
  selector: 'app-timeline',
  standalone: true,
  imports: [CommonModule, DateViPipe],
  template: `
    <div class="timeline">
      @for (event of events; track $index) {
        <div class="timeline-item" [ngClass]="event.status || 'upcoming'">
          <div class="timeline-marker">
            @if (event.icon) {
              <i [class]="event.icon"></i>
            } @else {
              @switch (event.status) {
                @case ('completed') {
                  <i class="fa-solid fa-check"></i>
                }
                @case ('current') {
                  <i class="fa-solid fa-circle"></i>
                }
                @case ('cancelled') {
                  <i class="fa-solid fa-xmark"></i>
                }
                @default {
                  <i class="fa-regular fa-circle"></i>
                }
              }
            }
          </div>
          <div class="timeline-connector" [class.last]="$last"></div>
          <div class="timeline-content">
            <div class="timeline-date">{{ event.date | dateVi: 'datetime' }}</div>
            <div class="timeline-title">{{ event.title }}</div>
            @if (event.description) {
              <div class="timeline-desc">{{ event.description }}</div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styleUrls: ['./timeline.component.scss'],
})
export class TimelineComponent {
  @Input() events: TimelineEvent[] = [];
}
