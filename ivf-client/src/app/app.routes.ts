import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login/login.component').then((m) => m.LoginComponent),
    canActivate: [guestGuard],
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component').then((m) => m.MainLayoutComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'patients',
        loadComponent: () =>
          import('./features/patients/patient-list/patient-list.component').then(
            (m) => m.PatientListComponent,
          ),
      },
      {
        path: 'patients/new',
        loadComponent: () =>
          import('./features/patients/patient-form/patient-form.component').then(
            (m) => m.PatientFormComponent,
          ),
      },
      {
        path: 'patients/:id',
        loadComponent: () =>
          import('./features/patients/patient-detail/patient-detail.component').then(
            (m) => m.PatientDetailComponent,
          ),
      },
      {
        path: 'patients/:id/biometrics',
        loadComponent: () =>
          import('./features/patients/patient-biometrics/patient-biometrics.component').then(
            (m) => m.PatientBiometricsComponent,
          ),
      },
      {
        path: 'patients/:id/documents',
        loadComponent: () =>
          import('./features/patients/patient-documents/patient-documents.component').then(
            (m) => m.PatientDocumentsComponent,
          ),
      },
      {
        path: 'couples',
        loadComponent: () =>
          import('./features/couples/couple-list/couple-list.component').then(
            (m) => m.CoupleListComponent,
          ),
      },
      {
        path: 'couples/new',
        loadComponent: () =>
          import('./features/couples/couple-form/couple-form.component').then(
            (m) => m.CoupleFormComponent,
          ),
      },
      {
        path: 'couples/:coupleId/cycles/new',
        loadComponent: () =>
          import('./features/cycles/cycle-form/cycle-form.component').then(
            (m) => m.CycleFormComponent,
          ),
      },
      {
        path: 'cycles/:id',
        loadComponent: () =>
          import('./features/cycles/cycle-detail/cycle-detail.component').then(
            (m) => m.CycleDetailComponent,
          ),
      },
      {
        path: 'cycles/:cycleId/ultrasound/new',
        loadComponent: () =>
          import('./features/ultrasounds/ultrasound-form/ultrasound-form.component').then(
            (m) => m.UltrasoundFormComponent,
          ),
      },
      {
        path: 'billing',
        loadComponent: () =>
          import('./features/billing/invoice-list/invoice-list.component').then(
            (m) => m.InvoiceListComponent,
          ),
      },
      {
        path: 'queue/:departmentCode',
        loadComponent: () =>
          import('./features/queue/queue-display/queue-display.component').then(
            (m) => m.QueueDisplayComponent,
          ),
      },
      {
        path: 'ultrasound',
        loadComponent: () =>
          import('./features/ultrasounds/ultrasound-dashboard/ultrasound-dashboard.component').then(
            (m) => m.UltrasoundDashboardComponent,
          ),
      },
      {
        path: 'consultation',
        loadComponent: () =>
          import('./features/consultation/consultation-dashboard/consultation-dashboard.component').then(
            (m) => m.ConsultationDashboardComponent,
          ),
      },
      {
        path: 'injection',
        loadComponent: () =>
          import('./features/injection/injection-dashboard/injection-dashboard.component').then(
            (m) => m.InjectionDashboardComponent,
          ),
      },
      {
        path: 'reception',
        loadComponent: () =>
          import('./features/reception/reception-dashboard/reception-dashboard.component').then(
            (m) => m.ReceptionDashboardComponent,
          ),
      },
      {
        path: 'andrology',
        loadComponent: () =>
          import('./features/andrology/andrology-dashboard/andrology-dashboard.component').then(
            (m) => m.AndrologyDashboardComponent,
          ),
      },
      {
        path: 'sperm-bank',
        loadComponent: () =>
          import('./features/sperm-bank/sperm-bank-dashboard/sperm-bank-dashboard.component').then(
            (m) => m.SpermBankDashboardComponent,
          ),
      },
      {
        path: 'lab',
        loadComponent: () =>
          import('./features/lab/lab-dashboard/lab-dashboard.component').then(
            (m) => m.LabDashboardComponent,
          ),
      },
      {
        path: 'pharmacy',
        loadComponent: () =>
          import('./features/pharmacy/pharmacy-dashboard/pharmacy-dashboard.component').then(
            (m) => m.PharmacyDashboardComponent,
          ),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports-dashboard/reports-dashboard.component').then(
            (m) => m.ReportsDashboardComponent,
          ),
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/appointments/appointments-dashboard.component').then(
            (m) => m.AppointmentsDashboardComponent,
          ),
      },
      {
        path: 'admin/audit-logs',
        loadComponent: () =>
          import('./features/admin/audit-logs/audit-logs.component').then(
            (m) => m.AuditLogsComponent,
          ),
      },
      {
        path: 'admin/notifications',
        loadComponent: () =>
          import('./features/admin/notifications/notification-management.component').then(
            (m) => m.NotificationManagementComponent,
          ),
      },
      {
        path: 'admin/users',
        loadComponent: () =>
          import('./features/admin/users/user-management.component').then(
            (m) => m.UserManagementComponent,
          ),
      },
      {
        path: 'admin/services',
        loadComponent: () =>
          import('./features/admin/services/service-catalog.component').then(
            (m) => m.ServiceCatalogComponent,
          ),
      },
      {
        path: 'admin/permissions',
        loadComponent: () =>
          import('./features/admin/permissions/permission-management.component').then(
            (m) => m.PermissionManagementComponent,
          ),
      },
      {
        path: 'admin/menu',
        loadComponent: () =>
          import('./features/admin/menu-config/menu-config.component').then(
            (m) => m.MenuConfigComponent,
          ),
      },
      {
        path: 'admin/permission-config',
        loadComponent: () =>
          import('./features/admin/permission-config/permission-config.component').then(
            (m) => m.PermissionConfigComponent,
          ),
      },
      {
        path: 'admin/digital-signing',
        loadComponent: () =>
          import('./features/admin/digital-signing/digital-signing.component').then(
            (m) => m.DigitalSigningComponent,
          ),
      },
      {
        path: 'forms',
        loadChildren: () => import('./features/forms/forms.routes').then((m) => m.FORMS_ROUTES),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
