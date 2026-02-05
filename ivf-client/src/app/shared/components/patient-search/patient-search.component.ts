import { Component, EventEmitter, Input, OnInit, Output, signal, OnChanges, SimpleChanges, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NG_VALUE_ACCESSOR, ControlValueAccessor } from '@angular/forms';
import { PatientService } from '../../../core/services/patient.service';
import { Patient } from '../../../core/models/api.models';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap } from 'rxjs/operators';

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
          (focus)="showDropdown = true"
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

      @if (showDropdown && (results().length > 0 || searchTerm)) {
        <div class="search-dropdown" (click)="$event.stopPropagation()">
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
    .patient-search-container {
      position: relative;
      width: 100%;
    }

    .search-input-wrapper {
      position: relative;
      display: flex;
      align-items: center;
    }

    .form-control {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 0.875rem;
      transition: all 0.2s;
    }

    .form-control:focus {
      outline: none;
      border-color: #6366f1;
      box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
    }

    .form-control.has-error {
      border-color: #ef4444;
    }

    .spinner {
      position: absolute;
      right: 12px;
      width: 16px;
      height: 16px;
      border: 2px solid #e2e8f0;
      border-top-color: #6366f1;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    .clear-btn {
      position: absolute;
      right: 12px;
      padding: 0 4px;
      background: none;
      border: none;
      color: #94a3b8;
      font-size: 1.25rem;
      cursor: pointer;
      line-height: 1;
    }

    .clear-btn:hover {
      color: #ef4444;
    }

    .search-dropdown {
      position: absolute;
      top: 100%;
      left: 0;
      right: 0;
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      margin-top: 4px;
      box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1);
      z-index: 1000;
      max-height: 300px;
      overflow-y: auto;
    }

    .dropdown-item {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      cursor: pointer;
      display: flex;
      gap: 1rem;
      align-items: center;
    }

    .dropdown-item:last-child {
      border-bottom: none;
    }

    .dropdown-item:hover {
      background: #f8fafc;
    }

    .patient-code {
      font-family: monospace;
      font-weight: 600;
      color: #6366f1;
      background: #e0e7ff;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
    }

    .text-muted {
      color: #64748b;
    }

    .small {
      font-size: 0.75rem;
    }

    .fw-bold {
      font-weight: 600;
    }

    .dropdown-item.empty {
      color: #64748b;
      padding: 1.5rem;
      text-align: center;
      display: block;
      cursor: default;
    }

    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 999;
      cursor: default;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PatientSearchComponent),
      multi: true
    }
  ]
})
export class PatientSearchComponent implements OnInit, ControlValueAccessor {
  @Input() placeholder = 'Tìm kiếm theo tên / mã / SĐT...';
  @Input() invalid = false;
  @Input() genderFilter: 'Male' | 'Female' | null = null;

  @Output() patientSelected = new EventEmitter<Patient | null>();

  searchTerm = '';
  results = signal<Patient[]>([]);
  isLoading = signal(false);
  showDropdown = false;
  selectedPatient = signal<Patient | null>(null);

  private searchSubject = new Subject<string>();

  // ControlValueAccessor callbacks
  onChange: any = () => { };
  onTouched: any = () => { };

  get isInvalid() {
    return this.invalid;
  }

  constructor(private patientService: PatientService) { }

  ngOnInit() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.isLoading.set(true)),
      switchMap(term => {
        if (!term || term.length < 2) {
          this.isLoading.set(false);
          return [];
        }
        return this.patientService.searchPatients(term);
      })
    ).subscribe({
      next: (response: any) => { // Type assertion as searchPatients might return wrapper
        this.isLoading.set(false);
        // Handle both paginated response and array if needed, assuming current API returns wrapper
        let items = response.items || [];
        if (this.genderFilter) {
          items = items.filter((p: Patient) => p.gender === this.genderFilter);
        }
        this.results.set(items);
        this.showDropdown = true;
      },
      error: () => this.isLoading.set(false)
    });
  }

  onSearch(event: Event) {
    const term = (event.target as HTMLInputElement).value;
    this.searchTerm = term;
    this.searchSubject.next(term);
    this.showDropdown = true;
  }

  selectPatient(patient: Patient) {
    this.selectedPatient.set(patient);
    this.searchTerm = `${patient.fullName}`;
    this.showDropdown = false;
    this.results.set([]);

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

  // Value Accessor Standard Methods
  writeValue(obj: any): void {
    if (obj) {
      // If we have an ID, we might need to fetch the patient name if not already loaded
      this.patientService.getPatient(obj).subscribe(p => {
        this.selectedPatient.set(p);
        this.searchTerm = p.fullName;
      });
    } else {
      this.selectedPatient.set(null);
      this.searchTerm = '';
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    // Implement if needed
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    return new Date(dateStr).toLocaleDateString('vi-VN');
  }
}
