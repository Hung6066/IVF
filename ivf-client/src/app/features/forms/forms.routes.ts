import { Routes } from '@angular/router';

export const FORMS_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./form-list/form-list.component').then(m => m.FormListComponent)
    },
    {
        path: 'categories',
        loadComponent: () => import('./category-management/category-management.component').then(m => m.CategoryManagementComponent)
    },
    {
        path: 'builder',
        loadComponent: () => import('./form-builder/form-builder.component').then(m => m.FormBuilderComponent)
    },
    {
        path: 'builder/:id',
        loadComponent: () => import('./form-builder/form-builder.component').then(m => m.FormBuilderComponent)
    },
    {
        path: 'fill/:id',
        loadComponent: () => import('./form-renderer/form-renderer.component').then(m => m.FormRendererComponent)
    },
    {
        path: 'responses',
        loadComponent: () => import('./form-responses/form-responses.component').then(m => m.FormResponsesComponent)
    },
    {
        path: 'responses/:id',
        loadComponent: () => import('./form-response-detail/form-response-detail.component').then(m => m.FormResponseDetailComponent)
    },
    {
        path: 'reports',
        loadComponent: () => import('./report-builder/report-builder.component').then(m => m.ReportBuilderComponent)
    },
    {
        path: 'reports/:id',
        loadComponent: () => import('./report-viewer/report-viewer.component').then(m => m.ReportViewerComponent)
    }
];
