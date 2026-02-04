import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
    {
        path: 'login',
        loadComponent: () => import('./auth/login/login.component').then(m => m.LoginComponent),
        canActivate: [guestGuard]
    },
    {
        path: '',
        loadComponent: () => import('./layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
        canActivate: [authGuard],
        children: [
            {
                path: 'dashboard',
                loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            {
                path: 'patients',
                loadComponent: () => import('./features/patients/patient-list/patient-list.component').then(m => m.PatientListComponent)
            },
            {
                path: 'patients/new',
                loadComponent: () => import('./features/patients/patient-form/patient-form.component').then(m => m.PatientFormComponent)
            },
            {
                path: 'patients/:id',
                loadComponent: () => import('./features/patients/patient-detail/patient-detail.component').then(m => m.PatientDetailComponent)
            },
            {
                path: 'couples',
                loadComponent: () => import('./features/couples/couple-list/couple-list.component').then(m => m.CoupleListComponent)
            },
            {
                path: 'couples/new',
                loadComponent: () => import('./features/couples/couple-form/couple-form.component').then(m => m.CoupleFormComponent)
            },
            {
                path: 'couples/:coupleId/cycles/new',
                loadComponent: () => import('./features/cycles/cycle-form/cycle-form.component').then(m => m.CycleFormComponent)
            },
            {
                path: 'cycles/:id',
                loadComponent: () => import('./features/cycles/cycle-detail/cycle-detail.component').then(m => m.CycleDetailComponent)
            },
            {
                path: 'cycles/:cycleId/ultrasound/new',
                loadComponent: () => import('./features/ultrasounds/ultrasound-form/ultrasound-form.component').then(m => m.UltrasoundFormComponent)
            },
            {
                path: 'billing',
                loadComponent: () => import('./features/billing/invoice-list/invoice-list.component').then(m => m.InvoiceListComponent)
            },
            {
                path: 'queue/:departmentCode',
                loadComponent: () => import('./features/queue/queue-display/queue-display.component').then(m => m.QueueDisplayComponent)
            },
            { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
    },
    { path: '**', redirectTo: 'login' }
];
