import { Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class RecentItemsService {
    private readonly MAX_ITEMS = 5;

    getItems(type: 'patient' | 'cycle'): any[] {
        const key = `recent_${type}s`;
        const stored = localStorage.getItem(key);
        return stored ? JSON.parse(stored) : [];
    }

    addItem(type: 'patient' | 'cycle', item: any): void {
        if (!item || !item.id) return;

        const key = `recent_${type}s`;
        let items = this.getItems(type);

        // Remove existing if present to move to top
        items = items.filter(i => i.id !== item.id);

        // Add to top
        items.unshift(item);

        // Limit size
        if (items.length > this.MAX_ITEMS) {
            items = items.slice(0, this.MAX_ITEMS);
        }

        localStorage.setItem(key, JSON.stringify(items));
    }

    clearItems(type: 'patient' | 'cycle'): void {
        localStorage.removeItem(`recent_${type}s`);
    }
    removeItem(type: 'patient' | 'cycle', id: string): void {
        const key = `recent_${type}s`;
        let items = this.getItems(type);
        items = items.filter(i => i.id !== id);
        localStorage.setItem(key, JSON.stringify(items));
    }
}
