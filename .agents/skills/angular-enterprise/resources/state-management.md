# Angular State Management

Advanced signal-based state management patterns for enterprise applications.

## Signal Store with Effects

```typescript
import { Injectable, signal, computed, inject, effect } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class PatientStore {
  private api = inject(PatientService);
  private notify = inject(NotificationService);

  // State
  private readonly state = signal<PatientState>({
    patients: [],
    loading: false,
    error: null,
    filter: '',
    selectedId: null,
  });

  // Selectors
  readonly patients = computed(() => this.state().patients);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);
  readonly filter = computed(() => this.state().filter);
  readonly selectedId = computed(() => this.state().selectedId);

  readonly filteredPatients = computed(() => {
    const filter = this.filter().toLowerCase();
    if (!filter) return this.patients();
    return this.patients().filter(p => 
      p.fullName.toLowerCase().includes(filter)
    );
  });

  readonly selectedPatient = computed(() =>
    this.patients().find(p => p.id === this.selectedId())
  );

  // Auto-search effect
  private searchEffect = effect(() => {
    const filter$ = toObservable(this.filter);
    filter$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => this.api.search(term))
    ).subscribe({
      next: patients => this.setPatients(patients),
      error: err => this.setError(err.message),
    });
  });

  // Actions
  setFilter(filter: string) {
    this.state.update(s => ({ ...s, filter }));
  }

  select(id: string | null) {
    this.state.update(s => ({ ...s, selectedId: id }));
  }

  async loadPatients() {
    this.state.update(s => ({ ...s, loading: true, error: null }));
    try {
      const patients = await firstValueFrom(this.api.getAll());
      this.setPatients(patients);
    } catch (err) {
      this.setError(err instanceof Error ? err.message : 'Load failed');
    }
  }

  private setPatients(patients: Patient[]) {
    this.state.update(s => ({ ...s, patients, loading: false }));
  }

  private setError(error: string) {
    this.state.update(s => ({ ...s, error, loading: false }));
    this.notify.error(error);
  }
}
```

## Component Store Pattern

For feature-specific state that doesn't need to be global:

```typescript
import { Injectable, signal, computed } from '@angular/core';

@Injectable() // Not providedIn: 'root' - scoped to component
export class PatientFormStore {
  private readonly state = signal<FormState>({
    data: null,
    dirty: false,
    submitting: false,
    errors: {},
  });

  readonly data = computed(() => this.state().data);
  readonly dirty = computed(() => this.state().dirty);
  readonly submitting = computed(() => this.state().submitting);
  readonly errors = computed(() => this.state().errors);
  readonly canSubmit = computed(() => 
    this.dirty() && !this.submitting() && Object.keys(this.errors()).length === 0
  );

  init(data: Patient) {
    this.state.set({ data, dirty: false, submitting: false, errors: {} });
  }

  update(changes: Partial<Patient>) {
    this.state.update(s => ({
      ...s,
      data: s.data ? { ...s.data, ...changes } : null,
      dirty: true,
    }));
  }

  setErrors(errors: Record<string, string>) {
    this.state.update(s => ({ ...s, errors }));
  }

  setSubmitting(submitting: boolean) {
    this.state.update(s => ({ ...s, submitting }));
  }
}

// Usage in component
@Component({
  providers: [PatientFormStore],
  template: `...`,
})
export class PatientFormComponent {
  private store = inject(PatientFormStore);
  // ...
}
```

## Derived State

```typescript
readonly stats = computed(() => {
  const patients = this.patients();
  return {
    total: patients.length,
    active: patients.filter(p => p.status === 'active').length,
    inactive: patients.filter(p => p.status === 'inactive').length,
    averageAge: patients.length 
      ? patients.reduce((sum, p) => sum + this.calculateAge(p.dob), 0) / patients.length
      : 0,
  };
});
```
