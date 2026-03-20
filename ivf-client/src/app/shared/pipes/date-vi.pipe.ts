import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'dateVi',
  standalone: true,
})
export class DateViPipe implements PipeTransform {
  transform(
    value: string | Date | null | undefined,
    format: 'short' | 'long' | 'datetime' = 'short',
  ): string {
    if (!value) return '';
    const date = value instanceof Date ? value : new Date(value);
    if (isNaN(date.getTime())) return '';

    switch (format) {
      case 'long':
        return date.toLocaleDateString('vi-VN', {
          weekday: 'long',
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
        });
      case 'datetime':
        return date.toLocaleString('vi-VN', {
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
        });
      default:
        return date.toLocaleDateString('vi-VN', {
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
        });
    }
  }
}
