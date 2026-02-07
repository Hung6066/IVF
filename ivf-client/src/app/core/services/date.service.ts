import { Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class DateService {

    /**
     * Converts a Date object or ISO string to 'YYYY-MM-DD' format for input[type="date"].
     */
    toInputDate(value: Date | string | null | undefined): string {
        if (!value) return '';
        const date = typeof value === 'string' ? new Date(value) : value;
        if (isNaN(date.getTime())) return '';
        return date.toISOString().split('T')[0];
    }

    /**
     * Converts 'YYYY-MM-DD' string to ISO string (start of day UTC or local depending on requirement).
     * Usually input[type="date"] gives 'YYYY-MM-DD'.
     * Backend often expects ISO.
     */
    toISOString(value: string): string | null {
        if (!value) return null;
        return new Date(value).toISOString();
    }
}
