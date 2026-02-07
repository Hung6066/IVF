import { Component, EventEmitter, Input, OnInit, Output, signal, forwardRef, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NG_VALUE_ACCESSOR, ControlValueAccessor } from '@angular/forms';
import { CycleService } from '../../../core/services/cycle.service';
import { RecentItemsService } from '../../../core/services/recent-items.service';
import { TreatmentCycle } from '../../../core/models/cycle.models';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap } from 'rxjs/operators';

@Component({
    selector: 'app-cycle-search',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="cycle-search-container">
      <div class="search-input-wrapper">
        <input 
          type="text" 
          [(ngModel)]="searchTerm" 
          (input)="onSearch($event)" 
          (focus)="onFocus()"
          [placeholder]="placeholder"
          [class.has-error]="isInvalid"
          class="form-control"
        >
        @if (isLoading()) {
          <div class="spinner"></div>
        }
        @if (selectedCycle() && !isLoading()) {
          <button type="button" class="clear-btn" (click)="clearSelection()">×</button>
        }
      </div>

      @if (showDropdown && (results().length > 0 || searchTerm || (showHistory && historyItems().length > 0))) {
        <div class="search-dropdown" (click)="$event.stopPropagation()">
          @if (showHistory && !searchTerm && historyItems().length > 0) {
             <div class="dropdown-header">Gần đây</div>
             @for (c of historyItems(); track c.id) {
                <div class="dropdown-item" (click)="selectCycle(c)">
                  <div class="cycle-code">{{ c.cycleCode }}</div>
                  <div class="cycle-info">
                    <div class="fw-bold">{{ c.method }}</div>
                    <div class="text-muted small">{{ formatDate(c.startDate) }}</div>
                  </div>
                  <button type="button" class="delete-history-btn" (click)="removeHistoryItem($event, c)" title="Xoá khỏi lịch sử">×</button>
                </div>
             }
             <div class="dropdown-divider"></div>
          }

          @for (c of results(); track c.id) {
            <div class="dropdown-item" (click)="selectCycle(c)">
              <div class="cycle-code">{{ c.cycleCode }}</div>
              <div class="cycle-info">
                <div class="fw-bold">{{ c.method }}</div>
                <div class="text-muted small">{{ formatDate(c.startDate) }}</div>
              </div>
            </div>
          }
          @if (results().length === 0 && searchTerm) {
            <div class="dropdown-item empty">
              Không tìm thấy chu kỳ "{{ searchTerm }}"
            </div>
          }
        </div>
      }
      
      @if (showDropdown) {
        <div class="backdrop" (click)="showDropdown = false"></div>
      }
    </div>
  `,
    styles: [`
    .cycle-search-container { position: relative; width: 100%; }
    .search-input-wrapper { position: relative; display: flex; align-items: center; }
    .form-control { width: 100%; padding: 0.75rem; border: 1px solid #e2e8f0; border-radius: 8px; font-size: 0.875rem; transition: all 0.2s; }
    .form-control:focus { outline: none; border-color: #6366f1; box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1); }
    .form-control.has-error { border-color: #ef4444; }
    .spinner { position: absolute; right: 12px; width: 16px; height: 16px; border: 2px solid #e2e8f0; border-top-color: #6366f1; border-radius: 50%; animation: spin 0.8s linear infinite; }
    .clear-btn { position: absolute; right: 12px; padding: 0 4px; background: none; border: none; color: #94a3b8; font-size: 1.25rem; cursor: pointer; line-height: 1; }
    .clear-btn:hover { color: #ef4444; }
    .search-dropdown { position: absolute; top: 100%; left: 0; right: 0; background: white; border: 1px solid #e2e8f0; border-radius: 8px; margin-top: 4px; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1); z-index: 1000; max-height: 300px; overflow-y: auto; }
    .dropdown-item { padding: 0.75rem 1rem; border-bottom: 1px solid #f1f5f9; cursor: pointer; display: flex; gap: 1rem; align-items: center; }
    .dropdown-item:last-child { border-bottom: none; }
    .dropdown-item:hover { background: #f8fafc; }
    .cycle-code { font-family: monospace; font-weight: 600; color: #059669; background: #ecfdf5; padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.75rem; }
    .text-muted { color: #64748b; }
    .small { font-size: 0.75rem; }
    .fw-bold { font-weight: 600; }
    .dropdown-item.empty { color: #64748b; padding: 1.5rem; text-align: center; display: block; cursor: default; }
    .backdrop { position: fixed; inset: 0; z-index: 999; cursor: default; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .dropdown-header { padding: 0.5rem 1rem; font-size: 0.75rem; font-weight: 700; color: #64748b; text-transform: uppercase; background: #f1f5f9; letter-spacing: 0.05em; }
    .dropdown-divider { height: 1px; background: #e2e8f0; margin: 0; }
    .delete-history-btn { margin-left: auto; padding: 2px 8px; border: none; background: transparent; color: #94a3b8; cursor: pointer; border-radius: 4px; font-size: 1.2rem; line-height: 1; }
    .delete-history-btn:hover { color: #ef4444; background: #fee2e2; }
  `],
    providers: [
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => CycleSearchComponent),
            multi: true
        }
    ]
})
export class CycleSearchComponent implements OnInit, OnChanges, ControlValueAccessor {
    @Input() placeholder = 'Tìm kiếm chu kỳ...';
    @Input() invalid = false;
    @Input() patientId: string | null = null;

    @Output() cycleSelected = new EventEmitter<TreatmentCycle | null>();

    searchTerm = '';
    // ...

    ngOnChanges(changes: SimpleChanges) {
        if (changes['patientId']) {
            const prev = changes['patientId'].previousValue;
            const curr = changes['patientId'].currentValue;

            if (prev !== curr) {
                this.clearSelection();
                // If dropdown is open, refresh the list with new filter
                if (this.showDropdown) {
                    this.searchSubject.next(this.searchTerm);
                }
            }
        }
    }
    results = signal<TreatmentCycle[]>([]);
    isLoading = signal(false);
    showDropdown = false;
    showHistory = false;
    selectedCycle = signal<TreatmentCycle | null>(null);
    historyItems = signal<TreatmentCycle[]>([]);

    private searchSubject = new Subject<string>();

    // ControlValueAccessor callbacks
    onChange: any = () => { };
    onTouched: any = () => { };

    get isInvalid() { return this.invalid; }

    constructor(
        private cycleService: CycleService,
        private recentItemsService: RecentItemsService
    ) {
        this.historyItems.set(this.recentItemsService.getItems('cycle'));
    }

    ngOnInit() {
        this.searchSubject.pipe(
            debounceTime(300),
            distinctUntilChanged(),
            tap((term) => {
                // Logic for loading/history state
                // If we have patientId, we might want to search immediately even without term?
                // Or just rely on user typing.
                // Let's assume user must type OR if patientId is present, maybe we trigger search?
                // For now standard behavior + patient filter.

                if (term && term.length >= 2) {
                    this.isLoading.set(true);
                    this.showHistory = false;
                } else {
                    // Only show history if NOT filtering by patient (or maybe show filtered history?)
                    // For simplicity, disable history if filtering by specific patient to avoid confusion
                    if (!this.patientId) {
                        this.showHistory = true;
                        this.historyItems.set(this.recentItemsService.getItems('cycle'));
                    } else {
                        this.showHistory = false;
                    }
                }
            }),
            switchMap(term => {
                if ((!term || term.length < 2) && !this.patientId) {
                    this.isLoading.set(false);
                    return [];
                }
                // If we have patientId, we allow empty term to fetch all cycles for that patient
                if (this.patientId && (!term || term.length === 0)) {
                    this.isLoading.set(true);
                    return this.cycleService.searchCycles('', this.patientId);
                }

                if (term && term.length >= 2) {
                    return this.cycleService.searchCycles(term, this.patientId || undefined);
                }

                return [];
            })
        ).subscribe({
            next: (data: any) => { // Assuming data is array or wrapper
                this.isLoading.set(false);
                this.results.set(Array.isArray(data) ? data : (data.items || []));
                this.showDropdown = true;
            },
            error: () => this.isLoading.set(false)
        });
    }

    onSearch(event: Event) {
        const term = (event.target as HTMLInputElement).value;
        this.searchTerm = term;
        if (!term) {
            if (!this.patientId) { // Only show history if no patientId filter
                this.showHistory = true;
                this.historyItems.set(this.recentItemsService.getItems('cycle'));
            } else {
                this.showHistory = false;
            }
        } else {
            this.showHistory = false;
        }
        this.searchSubject.next(term);
        this.showDropdown = true;
    }

    onFocus() {
        this.showDropdown = true;
        if (this.patientId) {
            // Auto search for patient's cycles on focus
            this.searchSubject.next(this.searchTerm);
        } else if (!this.searchTerm) {
            this.showHistory = true;
            this.historyItems.set(this.recentItemsService.getItems('cycle'));
        }
    }

    selectCycle(cycle: TreatmentCycle) {
        this.selectedCycle.set(cycle);
        this.searchTerm = `${cycle.cycleCode}`;
        this.showDropdown = false;
        this.results.set([]);

        // Add to recent
        this.recentItemsService.addItem('cycle', cycle);

        // Emit value
        this.onChange(cycle.id);
        this.cycleSelected.emit(cycle);
    }

    clearSelection() {
        this.selectedCycle.set(null);
        this.searchTerm = '';
        this.onChange(null);
        this.cycleSelected.emit(null);
    }

    removeHistoryItem(event: Event, cycle: TreatmentCycle) {
        event.preventDefault();
        event.stopPropagation();
        this.recentItemsService.removeItem('cycle', cycle.id);
        this.historyItems.set(this.recentItemsService.getItems('cycle'));
    }

    // Value Accessor Standard Methods
    writeValue(obj: any): void {
        if (obj) {
            // Fetch cycle details if just ID passed (optional, or rely on implementation to pass full object if possible, 
            // but value accessor usually deals with ID). 
            // For now assume we might need to fetch it if not already known.
            this.cycleService.getCycle(obj).subscribe(c => {
                this.selectedCycle.set(c);
                this.searchTerm = c.cycleCode;
            });
        } else {
            this.selectedCycle.set(null);
            this.searchTerm = '';
        }
    }

    registerOnChange(fn: any): void { this.onChange = fn; }
    registerOnTouched(fn: any): void { this.onTouched = fn; }
    setDisabledState?(isDisabled: boolean): void { }

    formatDate(dateStr: string | undefined): string {
        if (!dateStr) return '';
        return new Date(dateStr).toLocaleDateString('vi-VN');
    }
}
