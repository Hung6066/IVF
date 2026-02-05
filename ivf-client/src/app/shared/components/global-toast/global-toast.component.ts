import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GlobalNotificationService, Toast } from '../../../core/services/global-notification.service';
import { animate, style, transition, trigger } from '@angular/animations';

@Component({
    selector: 'app-global-toast',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="toast-container">
      @for (toast of notificationService.toasts(); track toast.id) {
        <div class="toast-item" [ngClass]="toast.type" @slideIn>
          <div class="toast-icon">
            @switch (toast.type) {
              @case ('success') { ✅ }
              @case ('error') { ❌ }
              @case ('warning') { ⚠️ }
              @case ('info') { ℹ️ }
            }
          </div>
          <div class="toast-content">
            <div class="toast-title">{{ toast.title }}</div>
            <div class="toast-message">{{ toast.message }}</div>
          </div>
          <button class="toast-close" (click)="notificationService.remove(toast.id)">×</button>
        </div>
      }
    </div>
  `,
    styles: [`
    .toast-container {
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      gap: 10px;
      pointer-events: none; /* Allow clicking through container */
    }

    .toast-item {
      pointer-events: auto;
      min-width: 300px;
      max-width: 400px;
      background: white;
      border-radius: 12px;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
      padding: 16px;
      display: flex;
      align-items: flex-start;
      gap: 12px;
      border-left: 4px solid transparent;
      overflow: hidden;

      &.success { border-left-color: #10b981; }
      &.error { border-left-color: #ef4444; }
      &.warning { border-left-color: #f59e0b; }
      &.info { border-left-color: #3b82f6; }

      .toast-icon {
        font-size: 1.25rem;
        line-height: 1;
      }

      .toast-content {
        flex: 1;
        
        .toast-title {
          font-weight: 600;
          color: #1f2937;
          margin-bottom: 4px;
          font-size: 0.95rem;
        }

        .toast-message {
          color: #6b7280;
          font-size: 0.875rem;
          line-height: 1.4;
        }
      }

      .toast-close {
        background: none;
        border: none;
        color: #9ca3af;
        cursor: pointer;
        padding: 4px;
        font-size: 1.25rem;
        line-height: 1;
        
        &:hover {
          color: #4b5563;
        }
      }
    }
  `],
    animations: [
        trigger('slideIn', [
            transition(':enter', [
                style({ transform: 'translateX(100%)', opacity: 0 }),
                animate('300ms ease-out', style({ transform: 'translateX(0)', opacity: 1 }))
            ]),
            transition(':leave', [
                animate('200ms ease-in', style({ transform: 'translateX(100%)', opacity: 0 }))
            ])
        ])
    ]
})
export class GlobalToastComponent {
    notificationService = inject(GlobalNotificationService);
}
