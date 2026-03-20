import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { signal, computed } from '@angular/core';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  type?: 'danger' | 'warning' | 'info';
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible()) {
      <div class="dialog-overlay" (click)="cancel()">
        <div
          class="dialog-container"
          [ngClass]="data()?.type || 'info'"
          (click)="$event.stopPropagation()"
        >
          <div class="dialog-header">
            <div class="dialog-icon">
              @switch (data()?.type) {
                @case ('danger') {
                  <i class="fa-solid fa-triangle-exclamation"></i>
                }
                @case ('warning') {
                  <i class="fa-solid fa-circle-exclamation"></i>
                }
                @default {
                  <i class="fa-solid fa-circle-info"></i>
                }
              }
            </div>
            <h3 class="dialog-title">{{ data()?.title }}</h3>
          </div>
          <div class="dialog-body">
            <p>{{ data()?.message }}</p>
          </div>
          <div class="dialog-footer">
            <button class="btn btn-cancel" (click)="cancel()">
              {{ data()?.cancelText || 'Hủy' }}
            </button>
            <button
              class="btn btn-confirm"
              [ngClass]="'btn-' + (data()?.type || 'info')"
              (click)="confirm()"
            >
              {{ data()?.confirmText || 'Xác nhận' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrls: ['./confirm-dialog.component.scss'],
})
export class ConfirmDialogComponent {
  visible = signal(false);
  data = signal<ConfirmDialogData | null>(null);

  private resolveRef: ((value: boolean) => void) | null = null;

  open(data: ConfirmDialogData): Promise<boolean> {
    this.data.set(data);
    this.visible.set(true);
    return new Promise<boolean>((resolve) => {
      this.resolveRef = resolve;
    });
  }

  confirm() {
    this.visible.set(false);
    this.resolveRef?.(true);
    this.resolveRef = null;
  }

  cancel() {
    this.visible.set(false);
    this.resolveRef?.(false);
    this.resolveRef = null;
  }
}
