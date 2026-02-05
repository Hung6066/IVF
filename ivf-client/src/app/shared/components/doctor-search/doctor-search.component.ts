import { Component, EventEmitter, Input, OnInit, Output, signal, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NG_VALUE_ACCESSOR, ControlValueAccessor } from '@angular/forms';
import { UserService } from '../../../core/services/user.service';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, tap } from 'rxjs/operators';

@Component({
  selector: 'app-doctor-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="doctor-search-container">
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
        @if (selectedDoctor() && !isLoading()) {
          <button type="button" class="clear-btn" (click)="clearSelection()">×</button>
        }
      </div>

      @if (showDropdown && (results().length > 0 || searchTerm)) {
        <div class="search-dropdown" (click)="$event.stopPropagation()">
          @for (d of results(); track d.id) {
            <div class="dropdown-item" (click)="selectDoctor(d)">
              <div class="doctor-avatar">👨‍⚕️</div>
              <div class="doctor-info">
                <div class="fw-bold">{{ d.fullName }}</div>
                <div class="text-muted small">{{ d.department || 'Bác sĩ' }}</div>
              </div>
            </div>
          }
          @if (results().length === 0 && searchTerm) {
            <div class="dropdown-item empty">
              Không tìm thấy bác sĩ "{{ searchTerm }}"
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
    .doctor-search-container { position: relative; width: 100%; }
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
    .doctor-avatar { font-size: 1.5rem; background: #e0e7ff; padding: 4px; border-radius: 50%; width: 36px; height: 36px; display: flex; align-items: center; justify-content: center; }
    .text-muted { color: #64748b; }
    .small { font-size: 0.75rem; }
    .fw-bold { font-weight: 600; }
    .dropdown-item.empty { color: #64748b; padding: 1.5rem; text-align: center; display: block; cursor: default; }
    .backdrop { position: fixed; inset: 0; z-index: 999; cursor: default; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DoctorSearchComponent),
      multi: true
    }
  ]
})
export class DoctorSearchComponent implements OnInit, ControlValueAccessor {
  @Input() placeholder = 'Tìm kiếm bác sĩ...';
  @Input() invalid = false;

  @Output() doctorSelected = new EventEmitter<any>();

  searchTerm = '';
  results = signal<any[]>([]);
  isLoading = signal(false);
  showDropdown = false;
  selectedDoctor = signal<any>(null);

  private searchSubject = new Subject<string>();

  onChange: any = () => { };
  onTouched: any = () => { };

  get isInvalid() { return this.invalid; }

  constructor(private userService: UserService) { }

  ngOnInit() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.isLoading.set(true)),
      switchMap(term => {
        if (!term) {
          this.isLoading.set(false);
          return [];
        }
        return this.userService.searchDoctors(term);
      })
    ).subscribe({
      next: (data) => {
        this.isLoading.set(false);
        this.results.set(data || []);
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

  selectDoctor(doctor: any) {
    this.selectedDoctor.set(doctor);
    this.searchTerm = doctor.fullName;
    this.showDropdown = false;
    this.results.set([]);

    this.onChange(doctor.id);
    this.doctorSelected.emit(doctor);
  }

  clearSelection() {
    this.selectedDoctor.set(null);
    this.searchTerm = '';
    this.onChange(null);
    this.doctorSelected.emit(null);
  }

  writeValue(obj: any): void {
    // If we want to load initial value we need getDoctor endpoint or passed object
    // For now simplistic clear
    if (!obj) {
      this.selectedDoctor.set(null);
      this.searchTerm = '';
    }
  }

  registerOnChange(fn: any): void { this.onChange = fn; }
  registerOnTouched(fn: any): void { this.onTouched = fn; }
  setDisabledState?(isDisabled: boolean): void { }
}
