import { Component, EventEmitter, Input, OnInit, Output, signal, OnChanges, SimpleChanges, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NG_VALUE_ACCESSOR, ControlValueAccessor } from '@angular/forms';
import { PatientService } from '../../../core/services/patient.service';
import { Patient } from '../../../core/models/api.models';
import { RecentItemsService } from '../../../core/services/recent-items.service';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap, map } from 'rxjs/operators';

@Component({
  selector: 'app-patient-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="patient-search-container">
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
        @if (selectedPatient() && !isLoading()) {
          <button type="button" class="clear-btn" (click)="clearSelection()">×</button>
        }
      </div>

      @if (showDropdown && (results().length > 0 || searchTerm || (showHistory && historyItems().length > 0))) {
        <div class="search-dropdown" (click)="$event.stopPropagation()">
          @if (showHistory && !searchTerm && historyItems().length > 0) {
             <div class="dropdown-header">Gần đây</div>
             @for (p of historyItems(); track p.id) {
                <div class="dropdown-item" (click)="selectPatient(p)">
                  <div class="patient-code">{{ p.patientCode }}</div>
                  <div class="patient-info">
                    <div class="fw-bold">{{ p.fullName }}</div>
                    <div class="text-muted small">{{ formatDate(p.dateOfBirth) }}</div>
                  </div>
                  <button type="button" class="delete-history-btn" (click)="removeHistoryItem($event, p)" title="Xoá khỏi lịch sử">×</button>
                </div>
             }
             <div class="dropdown-divider"></div>
          }

          @for (p of results(); track p.id) {
            <div class="dropdown-item" (click)="selectPatient(p)">
              <div class="patient-code">{{ p.patientCode }}</div>
              <div class="patient-info">
                <div class="fw-bold">{{ p.fullName }}</div>
                <div class="text-muted small">{{ formatDate(p.dateOfBirth) }} - {{ p.phone }}</div>
              </div>
            </div>
          }
          @if (results().length === 0 && searchTerm) {
            <div class="dropdown-item empty">
              Không tìm thấy bệnh nhân "{{ searchTerm }}"
            </div>
          }
        </div>
      }
      
      <!-- Backdrop to close dropdown -->
      @if (showDropdown) {
        <div class="backdrop" (click)="showDropdown = false"></div>
      }
    </div>
  `,
  styles: [`
    .patient-search-container { position: relative; width: 100%; }
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
    .patient-code { font-family: monospace; font-weight: 600; color: #6366f1; background: #e0e7ff; padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.75rem; }
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
      useExisting: forwardRef(() => PatientSearchComponent),
      multi: true
    }
  ]
})
export class PatientSearchComponent implements OnInit, OnChanges, ControlValueAccessor {
  @Input() placeholder = 'Tìm kiếm bệnh nhân...';
  @Input() invalid = false;
  @Input() genderFilter: 'Male' | 'Female' | null = null;
  @Input() suggestedPatients: Patient[] = [];

  @Output() patientSelected = new EventEmitter<Patient | null>();

  searchTerm = '';
  results = signal<Patient[]>([]);
  isLoading = signal(false);
  showDropdown = false;
  showHistory = false;
  selectedPatient = signal<Patient | null>(null);
  historyItems = signal<Patient[]>([]);

  private searchSubject = new Subject<string>();

  // ControlValueAccessor callbacks
  onChange: any = () => { };
  onTouched: any = () => { };

  get isInvalid() { return this.invalid; }

  constructor(
    private patientService: PatientService,
    private recentItemsService: RecentItemsService
  ) {
    this.historyItems.set(this.recentItemsService.getItems('patient'));
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['suggestedPatients'] && !changes['suggestedPatients'].firstChange) {
      if (!this.searchTerm && this.showDropdown) {
        this.results.set(this.suggestedPatients);
        this.showHistory = false;
      }
    }
  }

  ngOnInit() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => {
        this.isLoading.set(true);
        this.showHistory = false;
      }),
      switchMap(term => {
        if (!term || term.length < 2) {
          this.isLoading.set(false);
          return [];
        }
        const gender = this.genderFilter || undefined;
        return this.patientService.searchPatients(term, 1, 20, gender).pipe(
          map(response => response.items || [])
        );
      })
    ).subscribe({
      next: (items) => {
        this.isLoading.set(false);
        this.results.set(items);
        this.showDropdown = true;
      },
      error: () => this.isLoading.set(false)
    });
  }

  onSearch(event: Event) {
    const term = (event.target as HTMLInputElement).value;
    this.searchTerm = term;
    if (!term) {
      // Show history OR suggestions if available
      this.showHistory = true;
      this.historyItems.set(this.recentItemsService.getItems('patient'));
    } else {
      this.showHistory = false;
    }
    this.searchSubject.next(term);
    this.showDropdown = true;
  }

  onFocus() {
    this.showDropdown = true;
    if (!this.searchTerm) {
      if (this.suggestedPatients && this.suggestedPatients.length > 0) {
        this.results.set(this.suggestedPatients);
        this.showHistory = false;
      } else {
        this.showHistory = true;
        this.historyItems.set(this.recentItemsService.getItems('patient'));
      }
    }
  }

  selectPatient(patient: Patient) {
    this.selectedPatient.set(patient);
    this.searchTerm = patient.fullName;
    this.showDropdown = false;
    this.results.set([]);

    // Add to recent
    this.recentItemsService.addItem('patient', patient);

    // Emit value
    this.onChange(patient.id);
    this.patientSelected.emit(patient);
  }

  clearSelection() {
    this.selectedPatient.set(null);
    this.searchTerm = '';
    this.onChange(null);
    this.patientSelected.emit(null);
  }

  removeHistoryItem(event: Event, patient: Patient) {
    event.preventDefault();
    event.stopPropagation();
    this.recentItemsService.removeItem('patient', patient.id);
    this.historyItems.set(this.recentItemsService.getItems('patient'));
  }

  // Value Accessor Standard Methods
  writeValue(obj: any): void {
    if (obj && typeof obj === 'string') {
      const isUuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(obj);
      if (isUuid) {
        this.patientService.getPatient(obj).subscribe({
          next: (p) => {
            this.selectedPatient.set(p);
            this.searchTerm = p.fullName;
          },
          error: () => {
            this.selectedPatient.set(null);
            this.searchTerm = '';
          }
        });
      } else {
        this.selectedPatient.set(null);
        this.searchTerm = obj;
      }
    } else {
      this.selectedPatient.set(null);
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
