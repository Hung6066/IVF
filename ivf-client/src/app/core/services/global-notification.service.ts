import { Injectable, signal, computed } from '@angular/core';

export interface Toast {
    id: string;
    type: 'success' | 'error' | 'info' | 'warning';
    title: string;
    message: string;
    duration?: number;
    action?: {
        label: string;
        callback: () => void;
    };
}

@Injectable({ providedIn: 'root' })
export class GlobalNotificationService {
    private toastsSignal = signal<Toast[]>([]);
    readonly toasts = computed(() => this.toastsSignal());

    show(type: Toast['type'], title: string, message: string, duration = 5000) {
        const id = this.generateId();
        const toast: Toast = { id, type, title, message, duration };
        this.addToast(toast);

        if (duration > 0) {
            setTimeout(() => this.remove(id), duration);
        }
    }

    success(title: string, message: string, duration = 5000) {
        this.show('success', title, message, duration);
    }

    error(title: string, message: string, duration = 7000) {
        this.show('error', title, message, duration);
    }

    info(title: string, message: string, duration = 5000) {
        this.show('info', title, message, duration);
    }

    warning(title: string, message: string, duration = 6000) {
        this.show('warning', title, message, duration);
    }

    remove(id: string) {
        this.toastsSignal.update(toasts => toasts.filter(t => t.id !== id));
    }

    private addToast(toast: Toast) {
        this.toastsSignal.update(toasts => [toast, ...toasts].slice(0, 5)); // Keep max 5
    }

    private generateId(): string {
        return Math.random().toString(36).substring(2, 9);
    }
}
