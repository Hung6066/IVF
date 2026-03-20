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
        path: 'injection/log/new',
        loadComponent: () =>
          import('./features/injection/injection-log/injection-log.component').then(
            (m) => m.InjectionLogComponent,
          ),
      },
      {
        path: 'injection/trigger-shot',
        loadComponent: () =>
          import('./features/injection/trigger-shot-record/trigger-shot-record.component').then(
            (m) => m.TriggerShotRecordComponent,
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

      // ─── FET ───
      {
        path: 'fet',
        loadComponent: () =>
          import('./features/fet/fet-list/fet-list.component').then((m) => m.FetListComponent),
      },
      {
        path: 'fet/create',
        loadComponent: () =>
          import('./features/fet/fet-create/fet-create.component').then(
            (m) => m.FetCreateComponent,
          ),
      },
      {
        path: 'fet/:id',
        loadComponent: () =>
          import('./features/fet/fet-detail/fet-detail.component').then(
            (m) => m.FetDetailComponent,
          ),
      },
      {
        path: 'fet/:id/hormone-therapy',
        loadComponent: () =>
          import('./features/fet/hormone-therapy/hormone-therapy.component').then(
            (m) => m.HormoneTherapyComponent,
          ),
      },
      {
        path: 'fet/:id/transfer',
        loadComponent: () =>
          import('./features/fet/fet-transfer/fet-transfer.component').then(
            (m) => m.FetTransferComponent,
          ),
      },

      // ─── Procedure ───
      {
        path: 'procedure',
        loadComponent: () =>
          import('./features/procedure/procedure-list/procedure-list.component').then(
            (m) => m.ProcedureListComponent,
          ),
      },
      {
        path: 'procedure/create',
        loadComponent: () =>
          import('./features/procedure/procedure-create/procedure-create.component').then(
            (m) => m.ProcedureCreateComponent,
          ),
      },
      {
        path: 'procedure/:id',
        loadComponent: () =>
          import('./features/procedure/procedure-detail/procedure-detail.component').then(
            (m) => m.ProcedureDetailComponent,
          ),
      },
      {
        path: 'procedure/opu/:cycleId',
        loadComponent: () =>
          import('./features/procedure/procedure-opu/procedure-opu.component').then(
            (m) => m.ProcedureOpuComponent,
          ),
      },
      {
        path: 'procedure/iui/:cycleId',
        loadComponent: () =>
          import('./features/procedure/procedure-iui/procedure-iui.component').then(
            (m) => m.ProcedureIuiComponent,
          ),
      },

      // ─── Pregnancy ───
      {
        path: 'pregnancy/:cycleId/beta-hcg',
        loadComponent: () =>
          import('./features/pregnancy/pregnancy-beta-hcg/pregnancy-beta-hcg.component').then(
            (m) => m.PregnancyBetaHcgComponent,
          ),
      },
      {
        path: 'pregnancy/:cycleId/result',
        loadComponent: () =>
          import('./features/pregnancy/pregnancy-result/pregnancy-result.component').then(
            (m) => m.PregnancyResultComponent,
          ),
      },
      {
        path: 'pregnancy/:cycleId/prenatal',
        loadComponent: () =>
          import('./features/pregnancy/pregnancy-prenatal/pregnancy-prenatal.component').then(
            (m) => m.PregnancyPrenatalComponent,
          ),
      },
      {
        path: 'pregnancy/:cycleId/discharge',
        loadComponent: () =>
          import('./features/pregnancy/pregnancy-discharge/pregnancy-discharge.component').then(
            (m) => m.PregnancyDischargeComponent,
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
        path: 'admin/lynis',
        loadComponent: () =>
          import('./features/admin/lynis-dashboard/lynis-dashboard.component').then(
            (m) => m.LynisDashboardComponent,
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

      // ─── Stimulation ───────────────────────────────────────────────────────
      {
        path: 'stimulation/:cycleId',
        loadComponent: () =>
          import('./features/stimulation/stimulation-tracking/stimulation-tracking.component').then(
            (m) => m.StimulationTrackingComponent,
          ),
      },

      // ─── Egg Donor ─────────────────────────────────────────────────────────
      {
        path: 'egg-donor',
        loadComponent: () =>
          import('./features/egg-donor/egg-donor-list/egg-donor-list.component').then(
            (m) => m.EggDonorListComponent,
          ),
      },
      {
        path: 'egg-donor/register',
        loadComponent: () =>
          import('./features/egg-donor/egg-donor-register/egg-donor-register.component').then(
            (m) => m.EggDonorRegisterComponent,
          ),
      },
      {
        path: 'egg-donor/matching',
        loadComponent: () =>
          import('./features/egg-donor/egg-donor-matching/egg-donor-matching.component').then(
            (m) => m.EggDonorMatchingComponent,
          ),
      },
      {
        path: 'egg-donor/:id',
        loadComponent: () =>
          import('./features/egg-donor/egg-donor-detail/egg-donor-detail.component').then(
            (m) => m.EggDonorDetailComponent,
          ),
      },
      {
        path: 'egg-donor/:id/samples',
        loadComponent: () =>
          import('./features/egg-donor/egg-donor-samples/egg-donor-samples.component').then(
            (m) => m.EggDonorSamplesComponent,
          ),
      },

      // ─── Inventory ─────────────────────────────────────────────────────────
      {
        path: 'inventory',
        loadComponent: () =>
          import('./features/inventory/inventory-stock/inventory-stock.component').then(
            (m) => m.InventoryStockComponent,
          ),
      },
      {
        path: 'inventory/import',
        loadComponent: () =>
          import('./features/inventory/inventory-import/inventory-import.component').then(
            (m) => m.InventoryImportComponent,
          ),
      },
      {
        path: 'inventory/usage',
        loadComponent: () =>
          import('./features/inventory/inventory-usage/inventory-usage.component').then(
            (m) => m.InventoryUsageComponent,
          ),
      },
      {
        path: 'inventory/alerts',
        loadComponent: () =>
          import('./features/inventory/inventory-alerts/inventory-alerts.component').then(
            (m) => m.InventoryAlertsComponent,
          ),
      },
      {
        path: 'inventory/requests',
        loadComponent: () =>
          import('./features/inventory/inventory-request-list/inventory-request-list.component').then(
            (m) => m.InventoryRequestListComponent,
          ),
      },

      // ─── Consent ───────────────────────────────────────────────────────────
      {
        path: 'consent',
        loadComponent: () =>
          import('./features/consent/consent-list/consent-list.component').then(
            (m) => m.ConsentListComponent,
          ),
      },
      {
        path: 'consent/create',
        loadComponent: () =>
          import('./features/consent/consent-form-create/consent-form-create.component').then(
            (m) => m.ConsentFormCreateComponent,
          ),
      },
      {
        path: 'consent/:id',
        loadComponent: () =>
          import('./features/consent/consent-detail/consent-detail.component').then(
            (m) => m.ConsentDetailComponent,
          ),
      },

      // ─── Andrology (sub-pages) ─────────────────────────────────────────────
      {
        path: 'andrology/analysis/new',
        loadComponent: () =>
          import('./features/andrology/semen-analysis-form/semen-analysis-form.component').then(
            (m) => m.SemenAnalysisFormComponent,
          ),
      },
      {
        path: 'andrology/wash/new',
        loadComponent: () =>
          import('./features/andrology/sperm-wash-form/sperm-wash-form.component').then(
            (m) => m.SpermWashFormComponent,
          ),
      },
      {
        path: 'andrology/analysis/:id',
        loadComponent: () =>
          import('./features/andrology/andrology-result-detail/andrology-result-detail.component').then(
            (m) => m.AndrologyResultDetailComponent,
          ),
      },

      // ─── Billing (sub-pages) ───────────────────────────────────────────────
      {
        path: 'billing/create',
        loadComponent: () =>
          import('./features/billing/invoice-create/invoice-create.component').then(
            (m) => m.InvoiceCreateComponent,
          ),
      },
      {
        path: 'billing/history',
        loadComponent: () =>
          import('./features/billing/billing-history/billing-history.component').then(
            (m) => m.BillingHistoryComponent,
          ),
      },
      {
        path: 'billing/payment/:id',
        loadComponent: () =>
          import('./features/billing/payment-form/payment-form.component').then(
            (m) => m.PaymentFormComponent,
          ),
      },
      {
        path: 'billing/:id',
        loadComponent: () =>
          import('./features/billing/invoice-detail/invoice-detail.component').then(
            (m) => m.InvoiceDetailComponent,
          ),
      },

      // ─── Sperm Bank (sub-pages) ────────────────────────────────────────────
      {
        path: 'sperm-bank/screening/:id',
        loadComponent: () =>
          import('./features/sperm-bank/donor-screening/donor-screening.component').then(
            (m) => m.DonorScreeningComponent,
          ),
      },
      {
        path: 'sperm-bank/approve/:id',
        loadComponent: () =>
          import('./features/sperm-bank/donor-approval/donor-approval.component').then(
            (m) => m.DonorApprovalComponent,
          ),
      },
      {
        path: 'sperm-bank/sample/:donorId/collect',
        loadComponent: () =>
          import('./features/sperm-bank/sample-collection/sample-collection.component').then(
            (m) => m.SampleCollectionComponent,
          ),
      },
      {
        path: 'sperm-bank/samples',
        loadComponent: () =>
          import('./features/sperm-bank/sample-inventory/sample-inventory.component').then(
            (m) => m.SampleInventoryComponent,
          ),
      },
      {
        path: 'sperm-bank/sample-usage/:cycleId',
        loadComponent: () =>
          import('./features/sperm-bank/sperm-sample-usage/sperm-sample-usage.component').then(
            (m) => m.SpermSampleUsageComponent,
          ),
      },
      {
        path: 'sperm-bank/hiv-retest/:donorId',
        loadComponent: () =>
          import('./features/sperm-bank/hiv-retest/hiv-retest.component').then(
            (m) => m.HivRetestComponent,
          ),
      },

      // ─── Lab (sub-pages) ──────────────────────────────────────────────────
      {
        path: 'lab/sample-handover',
        loadComponent: () =>
          import('./features/lab/sample-handover/sample-handover.component').then(
            (m) => m.SampleHandoverComponent,
          ),
      },

      // ─── Embryology ───────────────────────────────────────────────────────
      {
        path: 'embryology/ivm/:cycleId',
        loadComponent: () =>
          import('./features/embryology/ivm-maturation/ivm-maturation.component').then(
            (m) => m.IvmMaturationComponent,
          ),
      },

      // ─── Reports (sub-pages) ───────────────────────────────────────────────
      {
        path: 'reports/clinical',
        loadComponent: () =>
          import('./features/reports/clinical-report/clinical-report.component').then(
            (m) => m.ClinicalReportComponent,
          ),
      },
      {
        path: 'reports/financial',
        loadComponent: () =>
          import('./features/reports/financial-report/financial-report.component').then(
            (m) => m.FinancialReportComponent,
          ),
      },
      {
        path: 'reports/inventory',
        loadComponent: () =>
          import('./features/reports/inventory-report/inventory-report.component').then(
            (m) => m.InventoryReportComponent,
          ),
      },

      // ─── Appointments (sub-pages) ──────────────────────────────────────────
      {
        path: 'appointments/calendar',
        loadComponent: () =>
          import('./features/appointments/appointment-calendar/appointment-calendar.component').then(
            (m) => m.AppointmentCalendarComponent,
          ),
      },
      {
        path: 'appointments/new',
        loadComponent: () =>
          import('./features/appointments/appointment-form/appointment-form.component').then(
            (m) => m.AppointmentFormComponent,
          ),
      },

      // ─── Ultrasounds (sub-pages) ───────────────────────────────────────────
      {
        path: 'follicle-chart/:cycleId',
        loadComponent: () =>
          import('./features/ultrasounds/follicle-chart/follicle-chart.component').then(
            (m) => m.FollicleChartComponent,
          ),
      },
      {
        path: 'endometrium-scan/:cycleId',
        loadComponent: () =>
          import('./features/ultrasounds/endometrium-scan-form/endometrium-scan-form.component').then(
            (m) => m.EndometriumScanFormComponent,
          ),
      },

      // ─── Drug Catalog ──────────────────────────────────────────────────────
      {
        path: 'admin/drug-catalog',
        loadComponent: () =>
          import('./features/drug-catalog/drug-catalog-list/drug-catalog-list.component').then(
            (m) => m.DrugCatalogListComponent,
          ),
      },

      // ─── Prescription Templates ────────────────────────────────────────────
      {
        path: 'admin/prescription-templates',
        loadComponent: () =>
          import('./features/prescription-templates/template-list/template-list.component').then(
            (m) => m.TemplateListComponent,
          ),
      },

      // ─── File Tracking ─────────────────────────────────────────────────────
      {
        path: 'file-tracking',
        loadComponent: () =>
          import('./features/file-tracking/file-tracking-list/file-tracking-list.component').then(
            (m) => m.FileTrackingListComponent,
          ),
      },

      // ─── Cycle Fees ────────────────────────────────────────────────────────
      {
        path: 'cycle-fees/:cycleId',
        loadComponent: () =>
          import('./features/cycle-fees/cycle-fee-list/cycle-fee-list.component').then(
            (m) => m.CycleFeeListComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
