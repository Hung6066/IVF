import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="status-badge" [ngClass]="statusClass">
      <span class="status-dot"></span>
      {{ label || status }}
    </span>
  `,
  styles: [
    `
      .status-badge {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 2px 10px;
        border-radius: 9999px;
        font-size: 12px;
        font-weight: 500;
        white-space: nowrap;
      }

      .status-dot {
        width: 6px;
        height: 6px;
        border-radius: 50%;
        flex-shrink: 0;
      }

      .status-active,
      .status-completed,
      .status-success,
      .status-paid {
        background: #ecfdf5;
        color: #065f46;
        .status-dot {
          background: #10b981;
        }
      }

      .status-pending,
      .status-scheduled,
      .status-processing,
      .status-in-progress {
        background: #eff6ff;
        color: #1e40af;
        .status-dot {
          background: #3b82f6;
        }
      }

      .status-warning,
      .status-overdue,
      .status-expiring {
        background: #fffbeb;
        color: #92400e;
        .status-dot {
          background: #f59e0b;
        }
      }

      .status-cancelled,
      .status-failed,
      .status-error,
      .status-rejected {
        background: #fef2f2;
        color: #991b1b;
        .status-dot {
          background: #ef4444;
        }
      }

      .status-inactive,
      .status-draft,
      .status-unknown {
        background: #f3f4f6;
        color: #374151;
        .status-dot {
          background: #9ca3af;
        }
      }

      .status-frozen,
      .status-stored {
        background: #eef2ff;
        color: #3730a3;
        .status-dot {
          background: #6366f1;
        }
      }

      .status-thawed,
      .status-transferred {
        background: #fdf4ff;
        color: #86198f;
        .status-dot {
          background: #d946ef;
        }
      }
    `,
  ],
})
export class StatusBadgeComponent {
  @Input() status = '';
  @Input() label = '';

  get statusClass(): string {
    const normalized = this.status.toLowerCase().replace(/[\s_]/g, '-');
    return `status-${normalized}`;
  }
}
