import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { featureGuard } from './core/guards/feature.guard';

export const routes: Routes = [
  {
    path: 'trust',
    loadComponent: () =>
      import('./features/trust/trust-page/trust-page.component').then((m) => m.TrustPageComponent),
  },
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
        path: 'patients/analytics',
        loadComponent: () =>
          import('./features/patients/patient-analytics/patient-analytics.component').then(
            (m) => m.PatientAnalyticsComponent,
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
        path: 'patients/:id/audit-trail',
        loadComponent: () =>
          import('./features/patients/patient-audit-trail/patient-audit-trail.component').then(
            (m) => m.PatientAuditTrailComponent,
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
        canActivate: [featureGuard('billing')],
        loadComponent: () =>
          import('./features/billing/invoice-list/invoice-list.component').then(
            (m) => m.InvoiceListComponent,
          ),
      },
      {
        path: 'queue/:departmentCode',
        canActivate: [featureGuard('queue')],
        loadComponent: () =>
          import('./features/queue/queue-display/queue-display.component').then(
            (m) => m.QueueDisplayComponent,
          ),
      },
      {
        path: 'ultrasound',
        canActivate: [featureGuard('ultrasound')],
        loadComponent: () =>
          import('./features/ultrasounds/ultrasound-dashboard/ultrasound-dashboard.component').then(
            (m) => m.UltrasoundDashboardComponent,
          ),
      },
      {
        path: 'consultation',
        canActivate: [featureGuard('consultation')],
        loadComponent: () =>
          import('./features/consultation/consultation-dashboard/consultation-dashboard.component').then(
            (m) => m.ConsultationDashboardComponent,
          ),
      },
      {
        path: 'injection',
        canActivate: [featureGuard('injection')],
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
        canActivate: [featureGuard('andrology')],
        loadComponent: () =>
          import('./features/andrology/andrology-dashboard/andrology-dashboard.component').then(
            (m) => m.AndrologyDashboardComponent,
          ),
      },
      {
        path: 'sperm-bank',
        canActivate: [featureGuard('sperm_bank')],
        loadComponent: () =>
          import('./features/sperm-bank/sperm-bank-dashboard/sperm-bank-dashboard.component').then(
            (m) => m.SpermBankDashboardComponent,
          ),
      },
      {
        path: 'lab',
        canActivate: [featureGuard('lab')],
        loadComponent: () =>
          import('./features/lab/lab-dashboard/lab-dashboard.component').then(
            (m) => m.LabDashboardComponent,
          ),
      },
      {
        path: 'pharmacy',
        canActivate: [featureGuard('pharmacy')],
        loadComponent: () =>
          import('./features/pharmacy/pharmacy-dashboard/pharmacy-dashboard.component').then(
            (m) => m.PharmacyDashboardComponent,
          ),
      },
      {
        path: 'reports',
        canActivate: [featureGuard('advanced_reporting')],
        loadComponent: () =>
          import('./features/reports/reports-dashboard/reports-dashboard.component').then(
            (m) => m.ReportsDashboardComponent,
          ),
      },
      {
        path: 'appointments',
        canActivate: [featureGuard('appointments')],
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
        redirectTo: 'admin/enterprise-users',
        pathMatch: 'full',
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
        path: 'admin/backup-restore',
        loadComponent: () =>
          import('./features/admin/backup-restore/backup-restore.component').then(
            (m) => m.BackupRestoreComponent,
          ),
      },
      {
        path: 'admin/infrastructure',
        loadComponent: () =>
          import('./features/admin/infrastructure-monitor/infrastructure-monitor.component').then(
            (m) => m.InfrastructureMonitorComponent,
          ),
      },
      {
        path: 'admin/waf',
        loadComponent: () =>
          import('./features/admin/waf-dashboard/waf-dashboard.component').then(
            (m) => m.WafDashboardComponent,
          ),
      },
      {
        path: 'admin/certificates',
        loadComponent: () =>
          import('./features/admin/certificate-management/certificate-management.component').then(
            (m) => m.CertificateManagementComponent,
          ),
      },
      {
        path: 'admin/vault',
        loadComponent: () =>
          import('./features/admin/vault-manager/vault-manager.component').then(
            (m) => m.VaultManagerComponent,
          ),
      },
      {
        path: 'admin/security',
        loadComponent: () =>
          import('./features/admin/advanced-security/advanced-security.component').then(
            (m) => m.AdvancedSecurityComponent,
          ),
      },
      {
        path: 'admin/enterprise-security',
        loadComponent: () =>
          import('./features/admin/enterprise-security/enterprise-security.component').then(
            (m) => m.EnterpriseSecurityComponent,
          ),
      },
      {
        path: 'admin/security-events',
        loadComponent: () =>
          import('./features/admin/security-events/security-events.component').then(
            (m) => m.SecurityEventsComponent,
          ),
      },
      {
        path: 'admin/enterprise-users',
        loadComponent: () =>
          import('./features/admin/enterprise-users/enterprise-users.component').then(
            (m) => m.EnterpriseUsersComponent,
          ),
      },
      {
        path: 'admin/tenant-ca',
        loadComponent: () =>
          import('./features/admin/tenant-ca/tenant-ca.component').then((m) => m.TenantCaComponent),
      },
      {
        path: 'admin/dns-records',
        loadComponent: () =>
          import('./features/admin/dns-records/dns-records.component').then(
            (m) => m.DnsRecordsComponent,
          ),
      },

      {
        path: 'forms',
        loadChildren: () => import('./features/forms/forms.routes').then((m) => m.FORMS_ROUTES),
      },

      // ─── Multi-tenant Management ───
      {
        path: 'admin/tenants',
        loadComponent: () =>
          import('./features/admin/tenant-management/tenant-management.component').then(
            (m) => m.TenantManagementComponent,
          ),
      },
      {
        path: 'admin/tenants/:id',
        loadComponent: () =>
          import('./features/admin/tenant-detail/tenant-detail.component').then(
            (m) => m.TenantDetailComponent,
          ),
      },
      {
        path: 'admin/domains',
        loadComponent: () =>
          import('./features/admin/domain-management/domain-management.component').then(
            (m) => m.DomainManagementComponent,
          ),
      },
      {
        path: 'pricing',
        loadComponent: () =>
          import('./features/pricing/pricing.component').then((m) => m.PricingComponent),
      },
      {
        path: 'admin/feature-plan-config',
        loadComponent: () =>
          import('./features/admin/feature-plan-config/feature-plan-config.component').then(
            (m) => m.FeaturePlanConfigComponent,
          ),
      },

      // ─── Compliance ───
      {
        path: 'compliance',
        loadComponent: () =>
          import('./features/compliance/compliance-dashboard/compliance-dashboard.component').then(
            (m) => m.ComplianceDashboardComponent,
          ),
      },
      {
        path: 'compliance/dsr',
        loadComponent: () =>
          import('./features/compliance/dsr-management/dsr-management.component').then(
            (m) => m.DsrManagementComponent,
          ),
      },
      {
        path: 'compliance/schedule',
        loadComponent: () =>
          import('./features/compliance/compliance-schedule/compliance-schedule.component').then(
            (m) => m.ComplianceScheduleComponent,
          ),
      },
      {
        path: 'compliance/assets',
        loadComponent: () =>
          import('./features/compliance/asset-inventory/asset-inventory.component').then(
            (m) => m.AssetInventoryComponent,
          ),
      },
      {
        path: 'compliance/ai',
        loadComponent: () =>
          import('./features/compliance/ai-governance/ai-governance.component').then(
            (m) => m.AiGovernanceComponent,
          ),
      },
      {
        path: 'compliance/training',
        loadComponent: () =>
          import('./features/compliance/training-management/training-management.component').then(
            (m) => m.TrainingManagementComponent,
          ),
      },
      {
        path: 'compliance/evidence',
        loadComponent: () =>
          import('./features/compliance/evidence-collection/evidence-collection.component').then(
            (m) => m.EvidenceCollectionComponent,
          ),
      },
      {
        path: 'compliance/audit',
        loadComponent: () =>
          import('./features/compliance/compliance-audit/compliance-audit.component').then(
            (m) => m.ComplianceAuditComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
