---
name: angular-enterprise
description: Enterprise Angular patterns following Google/Meta engineering standards. Use for building scalable, maintainable Angular applications with modern signal-based architecture, comprehensive testing, security hardening, and performance optimization. Triggers on component creation, service design, state management, and application architecture decisions.
version: 1.0
---

# Angular Enterprise Architecture

Enterprise-grade Angular patterns based on Google Angular team recommendations and large-scale application best practices.

## When to Use

- Building new features in enterprise applications
- Refactoring legacy Angular code to modern patterns
- Designing component architecture and state management
- Implementing security, testing, or performance requirements

## Core Principles

1. **Signals First** - Use signals for state, inputs, outputs
2. **OnPush Everywhere** - Default to `ChangeDetectionStrategy.OnPush`
3. **Standalone Components** - No NgModules for components
4. **Smart/Dumb Pattern** - Container components manage state, presentational components are pure
5. **Composition Over Inheritance** - Use composition and DI

## Architecture Overview

```
src/app/
├── core/                    # Singleton services, guards, interceptors
│   ├── services/
│   ├── guards/
│   ├── interceptors/
│   └── models/
├── shared/                  # Reusable components, pipes, directives
│   ├── components/
│   ├── directives/
│   └── pipes/
├── features/                # Feature modules (lazy-loaded)
│   └── [feature]/
│       ├── components/      # Presentational components
│       ├── containers/      # Smart components
│       ├── services/        # Feature-specific services
│       └── models/          # Feature-specific models
└── layout/                  # App shell, navigation
```

## Component Patterns

### Smart Component (Container)

```typescript
import { Component, ChangeDetectionStrategy, inject, computed } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-user-list-container',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <app-loading-spinner />
    } @else if (error()) {
      <app-error-message [error]="error()" (retry)="reload()" />
    } @else {
      <app-user-list 
        [users]="users()" 
        [selectedId]="selectedId()"
        (select)="onSelect($event)"
        (delete)="onDelete($event)" />
    }
  `,
})
export class UserListContainer {
  private userService = inject(UserService);
  private store = inject(UserStore);

  // Convert observables to signals
  users = toSignal(this.userService.getUsers(), { initialValue: [] });
  loading = this.store.loading;
  error = this.store.error;
  selectedId = this.store.selectedId;

  onSelect(id: string) {
    this.store.select(id);
  }

  onDelete(id: string) {
    this.store.delete(id);
  }

  reload() {
    this.store.reload();
  }
}
```

### Presentational Component

```typescript
import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { User } from '../models/user.model';

@Component({
  selector: 'app-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'class': 'user-list',
    'role': 'list',
    '[attr.aria-label]': '"User list"',
  },
  template: `
    @for (user of users(); track user.id) {
      <app-user-card 
        [user]="user"
        [selected]="user.id === selectedId()"
        (click)="select.emit(user.id)"
        (delete)="delete.emit(user.id)" />
    } @empty {
      <p class="empty-message">No users found</p>
    }
  `,
})
export class UserListComponent {
  users = input.required<User[]>();
  selectedId = input<string | null>(null);
  
  select = output<string>();
  delete = output<string>();
}
```

## State Management with Signals

### Signal Store Pattern

```typescript
import { Injectable, signal, computed } from '@angular/core';

interface State<T> {
  data: T[];
  loading: boolean;
  error: string | null;
  selectedId: string | null;
}

@Injectable({ providedIn: 'root' })
export class UserStore {
  // Private mutable state
  private state = signal<State<User>>({
    data: [],
    loading: false,
    error: null,
    selectedId: null,
  });

  // Public readonly selectors
  readonly users = computed(() => this.state().data);
  readonly loading = computed(() => this.state().loading);
  readonly error = computed(() => this.state().error);
  readonly selectedId = computed(() => this.state().selectedId);
  readonly selectedUser = computed(() => 
    this.state().data.find(u => u.id === this.state().selectedId)
  );

  // Actions
  setLoading(loading: boolean) {
    this.state.update(s => ({ ...s, loading, error: null }));
  }

  setUsers(data: User[]) {
    this.state.update(s => ({ ...s, data, loading: false }));
  }

  setError(error: string) {
    this.state.update(s => ({ ...s, error, loading: false }));
  }

  select(id: string | null) {
    this.state.update(s => ({ ...s, selectedId: id }));
  }

  addUser(user: User) {
    this.state.update(s => ({ ...s, data: [...s.data, user] }));
  }

  updateUser(id: string, changes: Partial<User>) {
    this.state.update(s => ({
      ...s,
      data: s.data.map(u => u.id === id ? { ...u, ...changes } : u),
    }));
  }

  removeUser(id: string) {
    this.state.update(s => ({
      ...s,
      data: s.data.filter(u => u.id !== id),
    }));
  }
}
```

## Service Patterns

### API Service with Error Handling

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, retry, throwError } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private baseUrl = '/api/users';

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(this.baseUrl).pipe(
      retry({ count: 2, delay: 1000 }),
      catchError(this.handleError),
    );
  }

  getUser(id: string): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/${id}`).pipe(
      catchError(this.handleError),
    );
  }

  createUser(user: CreateUserRequest): Observable<User> {
    return this.http.post<User>(this.baseUrl, user).pipe(
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let message = 'An error occurred';
    if (error.error instanceof ErrorEvent) {
      message = error.error.message;
    } else {
      message = error.error?.message || `Error ${error.status}: ${error.statusText}`;
    }
    console.error('API Error:', message);
    return throwError(() => new Error(message));
  }
}
```

## Testing Patterns

### Component Testing

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserListComponent } from './user-list.component';
import { By } from '@angular/platform-browser';

describe('UserListComponent', () => {
  let component: UserListComponent;
  let fixture: ComponentFixture<UserListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserListComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(UserListComponent);
    component = fixture.componentInstance;
  });

  it('should render users', () => {
    const users = [
      { id: '1', name: 'Alice' },
      { id: '2', name: 'Bob' },
    ];
    fixture.componentRef.setInput('users', users);
    fixture.detectChanges();

    const cards = fixture.debugElement.queryAll(By.css('app-user-card'));
    expect(cards.length).toBe(2);
  });

  it('should emit select event on click', () => {
    const users = [{ id: '1', name: 'Alice' }];
    fixture.componentRef.setInput('users', users);
    fixture.detectChanges();

    const selectSpy = jest.spyOn(component.select, 'emit');
    const card = fixture.debugElement.query(By.css('app-user-card'));
    card.triggerEventHandler('click');

    expect(selectSpy).toHaveBeenCalledWith('1');
  });

  it('should show empty message when no users', () => {
    fixture.componentRef.setInput('users', []);
    fixture.detectChanges();

    const empty = fixture.debugElement.query(By.css('.empty-message'));
    expect(empty).toBeTruthy();
  });
});
```

## Performance Patterns

### Lazy Loading Routes

```typescript
export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { 
    path: 'dashboard', 
    loadComponent: () => import('./features/dashboard/dashboard.component')
      .then(m => m.DashboardComponent),
  },
  { 
    path: 'users', 
    loadChildren: () => import('./features/users/users.routes')
      .then(m => m.USER_ROUTES),
    canActivate: [authGuard],
  },
];
```

### Virtual Scrolling

```typescript
import { CdkVirtualScrollViewport, CdkFixedSizeVirtualScroll, CdkVirtualForOf } from '@angular/cdk/scrolling';

@Component({
  imports: [CdkVirtualScrollViewport, CdkFixedSizeVirtualScroll, CdkVirtualForOf],
  template: `
    <cdk-virtual-scroll-viewport itemSize="48" class="viewport">
      <div *cdkVirtualFor="let item of items()" class="item">
        {{ item.name }}
      </div>
    </cdk-virtual-scroll-viewport>
  `,
  styles: [`
    .viewport { height: 400px; }
    .item { height: 48px; }
  `],
})
export class VirtualListComponent {
  items = input.required<Item[]>();
}
```

## Security Patterns

### Auth Guard

```typescript
import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  router.navigate(['/login'], { 
    queryParams: { returnUrl: state.url } 
  });
  return false;
};

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const requiredRoles = route.data['roles'] as string[];
  
  return requiredRoles.some(role => auth.hasRole(role));
};
```

### HTTP Interceptor

```typescript
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.getToken();

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        auth.logout();
      }
      return throwError(() => error);
    }),
  );
};
```

## Accessibility Requirements

All components MUST:
- Pass AXE accessibility checks (WCAG 2.1 AA)
- Support keyboard navigation (Tab, Enter, Space, Esc)
- Include proper ARIA attributes
- Maintain visible focus indicators
- Support screen readers

```typescript
@Component({
  selector: 'app-modal',
  host: {
    'role': 'dialog',
    'aria-modal': 'true',
    '[attr.aria-labelledby]': 'titleId',
    '(keydown.escape)': 'close()',
  },
  template: `
    <h2 [id]="titleId">{{ title() }}</h2>
    <ng-content />
    <button (click)="close()" aria-label="Close dialog">×</button>
  `,
})
export class ModalComponent {
  title = input.required<string>();
  titleId = `modal-title-${crypto.randomUUID().slice(0, 8)}`;
  closed = output<void>();

  close() {
    this.closed.emit();
  }
}
```

## Code Style

- **File naming**: `feature-name.component.ts`, `feature-name.service.ts`
- **Class naming**: PascalCase (`UserListComponent`, `AuthService`)
- **Method naming**: camelCase, verb-first (`getUsers`, `handleClick`)
- **Signal naming**: noun for values (`users`, `loading`), verb for actions (`select`, `delete`)
- **Max file length**: 300 lines (split if larger)
- **Max function length**: 30 lines

See `resources/` for detailed patterns:
- `resources/state-management.md` - Advanced signal store patterns
- `resources/testing.md` - Comprehensive testing strategies
- `resources/performance.md` - Performance optimization techniques
