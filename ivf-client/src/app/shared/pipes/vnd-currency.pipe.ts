import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'vndCurrency',
  standalone: true,
})
export class VndCurrencyPipe implements PipeTransform {
  transform(value: number | null | undefined, showSymbol = true): string {
    if (value == null) return '';
    const formatted = new Intl.NumberFormat('vi-VN').format(value);
    return showSymbol ? `${formatted} ₫` : formatted;
  }
}
