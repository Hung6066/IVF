import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TableColumn<T = any> {
  key: string;
  label: string;
  sortable?: boolean;
  align?: 'left' | 'center' | 'right';
  width?: string;
  format?: (value: any, row: T) => string;
  cssClass?: (value: any, row: T) => string;
}

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
      <table class="w-full text-sm">
        <thead class="bg-gray-50 border-b border-gray-200">
          <tr>
            @for (col of columns; track col.key) {
              <th
                class="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide"
                [class.text-left]="col.align !== 'center' && col.align !== 'right'"
                [class.text-center]="col.align === 'center'"
                [class.text-right]="col.align === 'right'"
                [class.cursor-pointer]="col.sortable"
                [class.select-none]="col.sortable"
                [style.width]="col.width ?? 'auto'"
                (click)="col.sortable && sort(col.key)"
              >
                {{ col.label }}
                @if (col.sortable) {
                  <i
                    class="fas ml-1 text-gray-400"
                    [class.fa-sort]="sortKey() !== col.key"
                    [class.fa-sort-up]="sortKey() === col.key && sortDir() === 'asc'"
                    [class.fa-sort-down]="sortKey() === col.key && sortDir() === 'desc'"
                  ></i>
                }
              </th>
            }
            @if (actions.length > 0) {
              <th class="px-4 py-3 text-xs font-semibold text-gray-500 uppercase"></th>
            }
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-50">
          @if (loading) {
            <tr>
              <td
                [attr.colspan]="columns.length + (actions.length ? 1 : 0)"
                class="text-center py-12 text-gray-400"
              >
                <i class="fas fa-spinner fa-spin fa-lg"></i>
              </td>
            </tr>
          } @else if (sortedData().length === 0) {
            <tr>
              <td
                [attr.colspan]="columns.length + (actions.length ? 1 : 0)"
                class="text-center py-12 text-gray-400"
              >
                <i class="fas fa-inbox fa-lg mb-2 block"></i>
                {{ emptyMessage }}
              </td>
            </tr>
          } @else {
            @for (row of sortedData(); track trackFn(row)) {
              <tr class="hover:bg-gray-50 transition-colors" (click)="rowClick.emit(row)">
                @for (col of columns; track col.key) {
                  <td
                    class="px-4 py-3"
                    [class.text-left]="col.align !== 'center' && col.align !== 'right'"
                    [class.text-center]="col.align === 'center'"
                    [class.text-right]="col.align === 'right'"
                    [class]="col.cssClass ? col.cssClass(getCellValue(row, col.key), row) : ''"
                  >
                    {{
                      col.format
                        ? col.format(getCellValue(row, col.key), row)
                        : getCellValue(row, col.key)
                    }}
                  </td>
                }
                @if (actions.length > 0) {
                  <td class="px-4 py-3 text-right whitespace-nowrap">
                    @for (action of actions; track action.label) {
                      <button
                        class="px-2 py-1 text-xs rounded mr-1"
                        [class]="action.cssClass ?? 'text-blue-600 hover:text-blue-800'"
                        (click)="
                          $event.stopPropagation(); actionClick.emit({ action: action.key, row })
                        "
                      >
                        {{ action.label }}
                      </button>
                    }
                  </td>
                }
              </tr>
            }
          }
        </tbody>
      </table>

      @if (pageSize > 0 && totalCount > pageSize) {
        <div
          class="px-4 py-3 border-t border-gray-100 flex items-center justify-between text-sm text-gray-500"
        >
          <span
            >{{ (currentPage - 1) * pageSize + 1 }}–{{
              Math.min(currentPage * pageSize, totalCount)
            }}
            / {{ totalCount }}</span
          >
          <div class="flex gap-1">
            <button
              (click)="pageChange.emit(currentPage - 1)"
              [disabled]="currentPage <= 1"
              class="px-2 py-1 border border-gray-200 rounded disabled:opacity-40 hover:bg-gray-50"
            >
              <i class="fas fa-chevron-left text-xs"></i>
            </button>
            <button
              (click)="pageChange.emit(currentPage + 1)"
              [disabled]="currentPage * pageSize >= totalCount"
              class="px-2 py-1 border border-gray-200 rounded disabled:opacity-40 hover:bg-gray-50"
            >
              <i class="fas fa-chevron-right text-xs"></i>
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [],
})
export class DataTableComponent<T = any> {
  protected readonly Math = Math;
  @Input() columns: TableColumn<T>[] = [];
  @Input() data: T[] = [];
  @Input() loading = false;
  @Input() emptyMessage = 'Không có dữ liệu';
  @Input() trackBy: keyof T | null = null;
  @Input() actions: { key: string; label: string; cssClass?: string }[] = [];
  @Input() pageSize = 0;
  @Input() currentPage = 1;
  @Input() totalCount = 0;

  @Output() rowClick = new EventEmitter<T>();
  @Output() actionClick = new EventEmitter<{ action: string; row: T }>();
  @Output() pageChange = new EventEmitter<number>();

  sortKey = signal('');
  sortDir = signal<'asc' | 'desc'>('asc');

  sortedData = computed(() => {
    const key = this.sortKey();
    if (!key) return this.data;
    return [...this.data].sort((a, b) => {
      const av = this.getCellValue(a, key);
      const bv = this.getCellValue(b, key);
      const cmp = av < bv ? -1 : av > bv ? 1 : 0;
      return this.sortDir() === 'asc' ? cmp : -cmp;
    });
  });

  sort(key: string) {
    if (this.sortKey() === key) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(key);
      this.sortDir.set('asc');
    }
  }

  getCellValue(row: T, key: string): any {
    return (key as string).split('.').reduce((obj: any, k) => obj?.[k], row);
  }

  trackFn(row: T): any {
    return this.trackBy ? row[this.trackBy] : row;
  }
}
