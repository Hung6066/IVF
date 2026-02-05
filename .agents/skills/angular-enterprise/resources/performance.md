# Angular Performance Optimization

Performance patterns for enterprise Angular applications.

## Change Detection

### OnPush Strategy

All components should use OnPush:

```typescript
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  // ...
})
export class MyComponent {
  // Use signals for reactive state
  data = input.required<Data>();
  computed = computed(() => this.data().value * 2);
}
```

### Avoiding Common Pitfalls

```typescript
// ❌ BAD: Function call in template (runs every change detection)
template: `<div>{{ calculateTotal() }}</div>`

// ✅ GOOD: Use computed signal
readonly total = computed(() => 
  this.items().reduce((sum, item) => sum + item.price, 0)
);
template: `<div>{{ total() }}</div>`
```

## Lazy Loading

### Route-Level Lazy Loading

```typescript
export const routes: Routes = [
  {
    path: 'patients',
    loadComponent: () => import('./features/patients/patient-list.component')
      .then(m => m.PatientListComponent),
  },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/admin.routes')
      .then(m => m.ADMIN_ROUTES),
    canMatch: [adminGuard],
  },
];
```

### Defer Blocks for Component-Level Lazy Loading

```typescript
@Component({
  template: `
    @defer (on viewport) {
      <app-heavy-chart [data]="chartData()" />
    } @placeholder {
      <div class="chart-placeholder">Loading chart...</div>
    } @loading (minimum 200ms) {
      <app-skeleton-chart />
    }
  `,
})
export class DashboardComponent {
  chartData = input.required<ChartData>();
}
```

### Defer with Triggers

```typescript
template: `
  <!-- Load when element enters viewport -->
  @defer (on viewport) {
    <app-comments [postId]="postId()" />
  }

  <!-- Load on user interaction -->
  @defer (on interaction) {
    <app-rich-editor />
  } @placeholder {
    <textarea placeholder="Click to enable rich editor"></textarea>
  }

  <!-- Load after idle time -->
  @defer (on idle) {
    <app-analytics />
  }

  <!-- Load after timer -->
  @defer (after 2s) {
    <app-recommendations />
  }

  <!-- Prefetch when condition met -->
  @defer (on viewport; prefetch on idle) {
    <app-related-content />
  }
`
```

## Virtual Scrolling

For large lists (100+ items):

```typescript
import { CdkVirtualScrollViewport, CdkFixedSizeVirtualScroll, CdkVirtualForOf } from '@angular/cdk/scrolling';

@Component({
  imports: [CdkVirtualScrollViewport, CdkFixedSizeVirtualScroll, CdkVirtualForOf],
  template: `
    <cdk-virtual-scroll-viewport itemSize="72" class="patient-viewport">
      <app-patient-row
        *cdkVirtualFor="let patient of patients(); trackBy: trackById"
        [patient]="patient"
        (select)="onSelect($event)" />
    </cdk-virtual-scroll-viewport>
  `,
  styles: [`
    .patient-viewport {
      height: 600px;
      width: 100%;
    }
  `],
})
export class PatientListComponent {
  patients = input.required<Patient[]>();
  
  trackById = (index: number, patient: Patient) => patient.id;
}
```

## Image Optimization

```typescript
import { NgOptimizedImage } from '@angular/common';

@Component({
  imports: [NgOptimizedImage],
  template: `
    <!-- Priority loading for above-the-fold images -->
    <img 
      ngSrc="/assets/hero.jpg" 
      width="1200" 
      height="600" 
      priority />

    <!-- Lazy loading for below-the-fold -->
    <img 
      [ngSrc]="patient().avatarUrl" 
      width="100" 
      height="100"
      placeholder="/assets/avatar-placeholder.jpg" />

    <!-- Responsive images -->
    <img 
      ngSrc="/assets/banner.jpg"
      width="1200"
      height="400"
      sizes="(max-width: 768px) 100vw, 1200px" />
  `,
})
export class ProfileComponent {
  patient = input.required<Patient>();
}
```

## Bundle Optimization

### Tree Shaking

```typescript
// ❌ BAD: Import entire library
import * as _ from 'lodash';
const result = _.debounce(fn, 300);

// ✅ GOOD: Import specific functions
import debounce from 'lodash/debounce';
const result = debounce(fn, 300);
```

### Code Splitting by Route

```typescript
// angular.json
{
  "projects": {
    "app": {
      "architect": {
        "build": {
          "options": {
            "optimization": {
              "scripts": true,
              "styles": true,
              "fonts": true
            },
            "budgets": [
              {
                "type": "initial",
                "maximumWarning": "500kb",
                "maximumError": "1mb"
              },
              {
                "type": "anyComponentStyle",
                "maximumWarning": "4kb",
                "maximumError": "8kb"
              }
            ]
          }
        }
      }
    }
  }
}
```

## Memoization

```typescript
import { computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PatientStore {
  private patients = signal<Patient[]>([]);
  private filter = signal('');

  // Computed values are automatically memoized
  readonly filteredPatients = computed(() => {
    const filter = this.filter().toLowerCase();
    const patients = this.patients();
    
    if (!filter) return patients;
    
    return patients.filter(p => 
      p.fullName.toLowerCase().includes(filter) ||
      p.email.toLowerCase().includes(filter)
    );
  });

  // Derived computations chain efficiently
  readonly patientCount = computed(() => this.filteredPatients().length);
  readonly hasPatients = computed(() => this.patientCount() > 0);
}
```

## HTTP Caching

```typescript
@Injectable({ providedIn: 'root' })
export class PatientService {
  private http = inject(HttpClient);
  private cache = new Map<string, { data: Patient; timestamp: number }>();
  private CACHE_TTL = 5 * 60 * 1000; // 5 minutes

  getPatient(id: string): Observable<Patient> {
    const cached = this.cache.get(id);
    if (cached && Date.now() - cached.timestamp < this.CACHE_TTL) {
      return of(cached.data);
    }

    return this.http.get<Patient>(`/api/patients/${id}`).pipe(
      tap(patient => this.cache.set(id, { data: patient, timestamp: Date.now() })),
      shareReplay(1),
    );
  }

  invalidateCache(id?: string) {
    if (id) {
      this.cache.delete(id);
    } else {
      this.cache.clear();
    }
  }
}
```

## TrackBy for ngFor

```typescript
@Component({
  template: `
    @for (patient of patients(); track patient.id) {
      <app-patient-card [patient]="patient" />
    }
  `,
})
export class PatientListComponent {
  patients = input.required<Patient[]>();
}
```

## Web Workers for Heavy Computation

```typescript
// patient-search.worker.ts
addEventListener('message', ({ data }) => {
  const { patients, searchTerm } = data;
  const results = patients.filter((p: Patient) =>
    p.fullName.toLowerCase().includes(searchTerm.toLowerCase())
  );
  postMessage(results);
});

// patient.service.ts
@Injectable({ providedIn: 'root' })
export class PatientSearchService {
  private worker: Worker | null = null;

  search(patients: Patient[], term: string): Observable<Patient[]> {
    return new Observable(subscriber => {
      if (typeof Worker !== 'undefined') {
        this.worker ??= new Worker(
          new URL('./patient-search.worker', import.meta.url)
        );
        
        this.worker.onmessage = ({ data }) => {
          subscriber.next(data);
          subscriber.complete();
        };
        
        this.worker.postMessage({ patients, searchTerm: term });
      } else {
        // Fallback for SSR
        const results = patients.filter(p =>
          p.fullName.toLowerCase().includes(term.toLowerCase())
        );
        subscriber.next(results);
        subscriber.complete();
      }
    });
  }
}
```
