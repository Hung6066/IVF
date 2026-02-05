# Angular Testing Patterns

Comprehensive testing strategies for enterprise Angular applications.

## Component Testing

### Setup with Testing Library

```typescript
import { render, screen, fireEvent } from '@testing-library/angular';
import { PatientCardComponent } from './patient-card.component';

describe('PatientCardComponent', () => {
  it('should display patient name', async () => {
    await render(PatientCardComponent, {
      inputs: {
        patient: { id: '1', fullName: 'John Doe', status: 'active' },
      },
    });

    expect(screen.getByText('John Doe')).toBeInTheDocument();
  });

  it('should emit select event on click', async () => {
    const onSelect = jest.fn();
    
    await render(PatientCardComponent, {
      inputs: {
        patient: { id: '1', fullName: 'John Doe', status: 'active' },
      },
      on: {
        select: onSelect,
      },
    });

    fireEvent.click(screen.getByRole('button'));
    expect(onSelect).toHaveBeenCalledWith('1');
  });

  it('should show inactive badge when patient is inactive', async () => {
    await render(PatientCardComponent, {
      inputs: {
        patient: { id: '1', fullName: 'John Doe', status: 'inactive' },
      },
    });

    expect(screen.getByText('Inactive')).toHaveClass('badge-inactive');
  });
});
```

## Service Testing

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { PatientService } from './patient.service';

describe('PatientService', () => {
  let service: PatientService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PatientService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(PatientService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch patients', () => {
    const mockPatients = [
      { id: '1', fullName: 'John Doe' },
      { id: '2', fullName: 'Jane Doe' },
    ];

    service.getPatients().subscribe(patients => {
      expect(patients).toEqual(mockPatients);
    });

    const req = httpMock.expectOne('/api/patients');
    expect(req.request.method).toBe('GET');
    req.flush(mockPatients);
  });

  it('should handle errors', () => {
    service.getPatients().subscribe({
      error: err => {
        expect(err.message).toContain('Error 500');
      },
    });

    const req = httpMock.expectOne('/api/patients');
    req.flush('Server error', { status: 500, statusText: 'Server Error' });
  });
});
```

## Store Testing

```typescript
import { TestBed } from '@angular/core/testing';
import { PatientStore } from './patient.store';
import { PatientService } from './patient.service';
import { of, throwError } from 'rxjs';

describe('PatientStore', () => {
  let store: PatientStore;
  let mockService: jest.Mocked<PatientService>;

  beforeEach(() => {
    mockService = {
      getAll: jest.fn(),
      search: jest.fn(),
    } as any;

    TestBed.configureTestingModule({
      providers: [
        PatientStore,
        { provide: PatientService, useValue: mockService },
      ],
    });

    store = TestBed.inject(PatientStore);
  });

  it('should load patients', async () => {
    const patients = [{ id: '1', fullName: 'John' }];
    mockService.getAll.mockReturnValue(of(patients));

    await store.loadPatients();

    expect(store.patients()).toEqual(patients);
    expect(store.loading()).toBe(false);
  });

  it('should handle load error', async () => {
    mockService.getAll.mockReturnValue(throwError(() => new Error('Network error')));

    await store.loadPatients();

    expect(store.error()).toBe('Network error');
    expect(store.loading()).toBe(false);
  });

  it('should filter patients', () => {
    store['state'].set({
      patients: [
        { id: '1', fullName: 'John Doe' },
        { id: '2', fullName: 'Jane Smith' },
      ],
      loading: false,
      error: null,
      filter: '',
      selectedId: null,
    });

    store.setFilter('john');

    expect(store.filteredPatients()).toHaveLength(1);
    expect(store.filteredPatients()[0].fullName).toBe('John Doe');
  });
});
```

## E2E Testing with Playwright

```typescript
import { test, expect } from '@playwright/test';

test.describe('Patient Management', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/patients');
  });

  test('should display patient list', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Patients' })).toBeVisible();
    await expect(page.getByRole('list')).toBeVisible();
  });

  test('should search patients', async ({ page }) => {
    await page.getByPlaceholder('Search patients').fill('John');
    await page.waitForTimeout(300); // debounce
    
    const rows = page.getByRole('listitem');
    await expect(rows).toHaveCount(1);
    await expect(rows.first()).toContainText('John');
  });

  test('should create new patient', async ({ page }) => {
    await page.getByRole('button', { name: 'Add Patient' }).click();
    
    await page.getByLabel('Full Name').fill('New Patient');
    await page.getByLabel('Date of Birth').fill('1990-01-01');
    await page.getByLabel('Email').fill('new@example.com');
    
    await page.getByRole('button', { name: 'Save' }).click();
    
    await expect(page.getByText('Patient created successfully')).toBeVisible();
    await expect(page.getByText('New Patient')).toBeVisible();
  });

  test('should handle validation errors', async ({ page }) => {
    await page.getByRole('button', { name: 'Add Patient' }).click();
    await page.getByRole('button', { name: 'Save' }).click();
    
    await expect(page.getByText('Full Name is required')).toBeVisible();
  });
});
```

## Accessibility Testing

```typescript
import { render } from '@testing-library/angular';
import { axe, toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

describe('PatientCardComponent Accessibility', () => {
  it('should have no accessibility violations', async () => {
    const { container } = await render(PatientCardComponent, {
      inputs: {
        patient: { id: '1', fullName: 'John Doe', status: 'active' },
      },
    });

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```
