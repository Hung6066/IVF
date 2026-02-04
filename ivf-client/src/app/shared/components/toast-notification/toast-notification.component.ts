import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { trigger, transition, style, animate } from '@angular/animations';

@Component({
    selector: 'app-toast-notification',
    standalone: true,
    imports: [CommonModule],
    template: `
    @if (show) {
      <div class="toast" [@slideIn] (click)="handleClick()">
        <div class="toast-icon">{{ getIcon() }}</div>
        <div class="toast-content">
          <div class="toast-title">{{ title }}</div>
          <div class="toast-message">{{ message }}</div>
        </div>
        <button class="toast-close" (click)="close($event)">×</button>
      </div>
    }
  `,
    styles: [`
    .toast {
      position: fixed;
      top: 80px;
      right: 20px;
      min-width: 320px;
      max-width: 400px;
      background: white;
      border-radius: 12px;
      box-shadow: 0 10px 25px rgba(0,0,0,0.15);
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 16px;
      cursor: pointer;
      z-index: 9999;
      border-left: 4px solid #6366f1;
    }

    .toast:hover {
      box-shadow: 0 12px 30px rgba(0,0,0,0.2);
      transform: translateY(-2px);
      transition: all 0.2s;
    }

    .toast-icon {
      font-size: 1.5rem;
      flex-shrink: 0;
    }

    .toast-content {
      flex: 1;
    }

    .toast-title {
      font-weight: 600;
      font-size: 0.95rem;
      color: #1e293b;
      margin-bottom: 4px;
    }

    .toast-message {
      font-size: 0.85rem;
      color: #64748b;
      line-height: 1.4;
    }

    .toast-close {
      background: none;
      border: none;
      font-size: 1.5rem;
      cursor: pointer;
      color: #94a3b8;
      padding: 0;
      width: 24px;
      height: 24px;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 4px;
      flex-shrink: 0;
    }

    .toast-close:hover {
      background: #f1f5f9;
      color: #64748b;
    }
  `],
    animations: [
        trigger('slideIn', [
            transition(':enter', [
                style({ transform: 'translateX(400px)', opacity: 0 }),
                animate('300ms ease-out', style({ transform: 'translateX(0)', opacity: 1 }))
            ]),
            transition(':leave', [
                animate('200ms ease-in', style({ transform: 'translateX(400px)', opacity: 0 }))
            ])
        ])
    ]
})
export class ToastNotificationComponent {
    @Input() title: string = '';
    @Input() message: string = '';
    @Input() type: string = 'Info';
    @Input() show: boolean = false;
    @Output() dismissed = new EventEmitter<void>();
    @Output() clicked = new EventEmitter<void>();

    getIcon(): string {
        switch (this.type) {
            case 'AppointmentReminder': return '📅';
            case 'QueueCalled': return '🔔';
            case 'CycleUpdate': return '🔄';
            case 'PaymentDue': return '💰';
            case 'Success': return '✅';
            case 'Warning': return '⚠️';
            case 'Error': return '❌';
            default: return '🧾';
        }
    }

    close(event: Event): void {
        event.stopPropagation();
        this.show = false;
        this.dismissed.emit();
    }

    handleClick(): void {
        this.clicked.emit();
    }
}
