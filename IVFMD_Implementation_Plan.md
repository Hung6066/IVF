# KẾ HOẠCH TRIỂN KHAI HỆ THỐNG IVFMD

## MỤC LỤC

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Database Schema](#2-database-schema)
3. [Phân chia Phase triển khai](#3-phân-chia-phase-triển-khai)
4. [Phase 1 — Nền tảng & Quản lý bệnh nhân](#4-phase-1)
5. [Phase 2 — Quy trình Khám & Tư vấn](#5-phase-2)
6. [Phase 3 — Kích thích buồng trứng & Theo dõi](#6-phase-3)
7. [Phase 4 — Thủ thuật (Chọc hút, IUI, IVM)](#7-phase-4)
8. [Phase 5 — Phôi học (LABO)](#8-phase-5)
9. [Phase 5b — Chuyển phôi trữ / FET (CBNMTC)](#9-phase-5b) ← MỚI
10. [Phase 6 — Thai kỳ sớm](#10-phase-6)
11. [Phase 7 — Người cho trứng](#11-phase-7)
12. [Phase 8 — Ngân hàng tinh trùng](#12-phase-8)
13. [Phase 9 — Quản lý Thuốc & Vật tư](#13-phase-9)
14. [Phase 10 — Tài chính & Báo cáo](#14-phase-10)
15. [API Endpoints tổng hợp](#15-api-endpoints)
16. [Checklist theo dõi tiến độ](#16-checklist)

---

## 1. KIẾN TRÚC TỔNG QUAN

### 1.1 Tech Stack đề xuất

```
┌──────────────────────────────────────────────────────────────┐
│                         FRONTEND                             │
│  Angular 17+ (Standalone Components) + TypeScript strict     │
│  Styling  : Tailwind CSS 3 + Angular CDK                     │
│  State    : NgRx Signal Store (hoặc NgRx Store + Effects)    │
│  Forms    : Angular Reactive Forms + custom validators       │
│  UI Kit   : Angular Material 17+ (hoặc PrimeNG 17+)         │
│  Table    : AG Grid Community hoặc PrimeNG Table             │
│  HTTP     : Angular HttpClient + Interceptors                │
│  Auth     : @auth0/angular-jwt │ Angular Guards + RBAC       │
│  Charts   : ngx-echarts (ECharts) hoặc Chart.js via ng2-charts│
│  Calendar : FullCalendar Angular                              │
│  Print    : ngx-print │ jsPDF + html2canvas                  │
│  i18n     : @ngx-translate/core (vi/en)                      │
│  Testing  : Jest (unit) + Cypress (e2e)                      │
├──────────────────────────────────────────────────────────────┤
│                         BACKEND                              │
│  Node.js (NestJS) hoặc .NET Core Web API                     │
│  ORM: Prisma (Node) hoặc Entity Framework (.NET)             │
│  Auth: JWT + Role-Based Access Control                       │
├──────────────────────────────────────────────────────────────┤
│                         DATABASE                             │
│  PostgreSQL (chính) │ Redis (cache/queue)                     │
│  MinIO/S3 (file storage: ảnh, tài liệu)                      │
├──────────────────────────────────────────────────────────────┤
│                       INFRASTRUCTURE                         │
│  Docker │ CI/CD │ Message Queue (Bull/RabbitMQ)               │
│  Print Service │ Barcode/QR Generator                         │
│  Nginx (reverse proxy + serve Angular build)                  │
└──────────────────────────────────────────────────────────────┘
```

### 1.1.1 Angular Project Structure

```
ivfmd-frontend/
├── angular.json
├── tailwind.config.js
├── tsconfig.json
├── src/
│   ├── main.ts
│   ├── app/
│   │   ├── app.component.ts
│   │   ├── app.config.ts                    # provideRouter, provideHttpClient...
│   │   ├── app.routes.ts                    # Top-level lazy routes
│   │   │
│   │   ├── core/                            # Singleton services, guards, interceptors
│   │   │   ├── auth/
│   │   │   │   ├── auth.service.ts
│   │   │   │   ├── auth.guard.ts            # canActivate — check JWT
│   │   │   │   ├── role.guard.ts            # canActivate — check RBAC role
│   │   │   │   ├── auth.interceptor.ts      # Attach Bearer token
│   │   │   │   └── token.service.ts         # JWT storage, refresh
│   │   │   ├── services/
│   │   │   │   ├── api.service.ts           # Base HTTP wrapper
│   │   │   │   ├── notification.service.ts  # Toast / snackbar
│   │   │   │   ├── print.service.ts         # In ấn
│   │   │   │   └── websocket.service.ts     # Realtime (STT, phôi...)
│   │   │   ├── models/                      # Shared interfaces & enums
│   │   │   │   ├── patient.model.ts
│   │   │   │   ├── cycle.model.ts
│   │   │   │   ├── embryo.model.ts
│   │   │   │   ├── invoice.model.ts
│   │   │   │   └── ...
│   │   │   └── interceptors/
│   │   │       ├── error.interceptor.ts     # Global error handling
│   │   │       └── loading.interceptor.ts   # Auto loading spinner
│   │   │
│   │   ├── shared/                          # Reusable UI components
│   │   │   ├── components/
│   │   │   │   ├── data-table/              # Wrapper AG Grid / PrimeNG Table
│   │   │   │   ├── confirm-dialog/
│   │   │   │   ├── file-upload/
│   │   │   │   ├── patient-search/          # Autocomplete tìm BN
│   │   │   │   ├── timeline/                # Timeline component (chu kỳ)
│   │   │   │   ├── status-badge/
│   │   │   │   └── print-layout/            # Layout chung cho in ấn
│   │   │   ├── directives/
│   │   │   │   ├── has-role.directive.ts     # *hasRole="['BS_TU_VAN']"
│   │   │   │   └── autofocus.directive.ts
│   │   │   ├── pipes/
│   │   │   │   ├── vnd-currency.pipe.ts     # Format tiền VND
│   │   │   │   └── date-vi.pipe.ts          # Format ngày tiếng Việt
│   │   │   └── validators/
│   │   │       ├── phone.validator.ts
│   │   │       └── id-card.validator.ts
│   │   │
│   │   ├── layout/                          # Shell layout
│   │   │   ├── main-layout/
│   │   │   │   ├── main-layout.component.ts
│   │   │   │   ├── sidebar/
│   │   │   │   ├── header/
│   │   │   │   └── breadcrumb/
│   │   │   └── auth-layout/                 # Layout cho login
│   │   │
│   │   └── features/                        # Feature modules (lazy-loaded)
│   │       ├── dashboard/
│   │       ├── patients/
│   │       ├── queue/
│   │       ├── appointments/
│   │       ├── consultation/
│   │       ├── ultrasound/
│   │       ├── lab/
│   │       ├── prescription/
│   │       ├── pharmacy/
│   │       ├── stimulation/
│   │       ├── procedure/
│   │       ├── embryology/
│   │       ├── andrology/
│   │       ├── sperm-bank/
│   │       ├── egg-donor/
│   │       ├── pregnancy/
│   │       ├── endometrium-prep/          # ★ MỚI: FET / CBNMTC
│   │       ├── consent/                   # ★ MỚI: Consent forms management
│   │       ├── billing/
│   │       ├── inventory/
│   │       ├── reports/
│   │       ├── notifications/             # ★ MỚI: SMS/Zalo/In-app notifications
│   │       └── admin/
│   │
│   ├── assets/
│   │   ├── images/
│   │   ├── icons/
│   │   └── print-templates/                 # HTML templates cho in ấn
│   │
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   │
│   └── styles/
│       ├── styles.scss                      # Global styles
│       ├── _tailwind.scss                   # @tailwind base/components/utilities
│       ├── _variables.scss                  # CSS custom properties / SCSS vars
│       ├── _angular-material-theme.scss     # Custom Angular Material theme
│       └── _print.scss                      # Print-specific styles
```

### 1.1.2 Angular Routing Strategy (Lazy Loading)

```typescript
// app.routes.ts
export const APP_ROUTES: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./features/auth/login/login.component') },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes')
      },
      {
        path: 'patients',
        loadChildren: () => import('./features/patients/patients.routes'),
        canActivate: [roleGuard],
        data: { roles: ['ADMIN','BS_TU_VAN','BS_SIEU_AM','NHS_HANH_CHANH','TIEP_DON_THU_NGAN'] }
      },
      {
        path: 'queue',
        loadChildren: () => import('./features/queue/queue.routes'),
      },
      {
        path: 'appointments',
        loadChildren: () => import('./features/appointments/appointments.routes'),
      },
      {
        path: 'consultation',
        loadChildren: () => import('./features/consultation/consultation.routes'),
        data: { roles: ['BS_TU_VAN','NHS_HANH_CHANH'] }
      },
      {
        path: 'ultrasound',
        loadChildren: () => import('./features/ultrasound/ultrasound.routes'),
        data: { roles: ['BS_SIEU_AM','BS_TU_VAN'] }
      },
      {
        path: 'lab',
        loadChildren: () => import('./features/lab/lab.routes'),
        data: { roles: ['XET_NGHIEM','NHS_HANH_CHANH'] }
      },
      {
        path: 'prescription',
        loadChildren: () => import('./features/prescription/prescription.routes'),
        data: { roles: ['BS_TU_VAN','BS_SIEU_AM','NHS'] }
      },
      {
        path: 'pharmacy',
        loadChildren: () => import('./features/pharmacy/pharmacy.routes'),
        data: { roles: ['NHA_THUOC'] }
      },
      {
        path: 'stimulation',
        loadChildren: () => import('./features/stimulation/stimulation.routes'),
        data: { roles: ['BS_TU_VAN','BS_SIEU_AM','NHS'] }
      },
      {
        path: 'procedure',
        loadChildren: () => import('./features/procedure/procedure.routes'),
        data: { roles: ['BS_HIEM_MUON','KTV_GAY_ME','NHS'] }
      },
      {
        path: 'embryology',
        loadChildren: () => import('./features/embryology/embryology.routes'),
        data: { roles: ['LABO','BS_HIEM_MUON'] }
      },
      {
        path: 'andrology',
        loadChildren: () => import('./features/andrology/andrology.routes'),
        data: { roles: ['NAM_KHOA'] }
      },
      {
        path: 'sperm-bank',
        loadChildren: () => import('./features/sperm-bank/sperm-bank.routes'),
        data: { roles: ['NAM_KHOA','NHS_HANH_CHANH','LABO'] }
      },
      {
        path: 'egg-donor',
        loadChildren: () => import('./features/egg-donor/egg-donor.routes'),
        data: { roles: ['BS_TU_VAN','NHS_HANH_CHANH','THU_KY_Y_KHOA'] }
      },
      {
        path: 'pregnancy',
        loadChildren: () => import('./features/pregnancy/pregnancy.routes'),
        data: { roles: ['BS_TU_VAN','NHS'] }
      },
      {
        path: 'endometrium-prep',
        loadChildren: () => import('./features/endometrium-prep/endometrium-prep.routes'),
        data: { roles: ['BS_TU_VAN','BS_SIEU_AM','NHS_HANH_CHANH'] }
      },
      {
        path: 'consent',
        loadChildren: () => import('./features/consent/consent.routes'),
        data: { roles: ['NHS_HANH_CHANH','BS_TU_VAN','BS_HIEM_MUON','LABO'] }
      },
      {
        path: 'billing',
        loadChildren: () => import('./features/billing/billing.routes'),
        data: { roles: ['TIEP_DON_THU_NGAN','ADMIN'] }
      },
      {
        path: 'inventory',
        loadChildren: () => import('./features/inventory/inventory.routes'),
        data: { roles: ['NHS','KHOA_DUOC','KTV_GAY_ME'] }
      },
      {
        path: 'reports',
        loadChildren: () => import('./features/reports/reports.routes'),
        data: { roles: ['ADMIN','BS_TU_VAN'] }
      },
      {
        path: 'admin',
        loadChildren: () => import('./features/admin/admin.routes'),
        data: { roles: ['ADMIN'] }
      },
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
```

### 1.1.3 Ví dụ Feature Module Structure (patients)

```
features/patients/
├── patients.routes.ts
├── services/
│   └── patient.service.ts                   # HttpClient calls
├── store/                                   # NgRx Signal Store
│   └── patient.store.ts
├── pages/
│   ├── patient-list/
│   │   ├── patient-list.component.ts        # Standalone component
│   │   ├── patient-list.component.html
│   │   └── patient-list.component.spec.ts
│   ├── patient-detail/
│   │   ├── patient-detail.component.ts
│   │   └── patient-detail.component.html
│   └── patient-form/
│       ├── patient-form.component.ts
│       └── patient-form.component.html
└── components/                              # Feature-scoped components
    ├── patient-search-bar/
    ├── couple-info-card/
    └── patient-history-timeline/
```

### 1.1.4 Key Angular Patterns được sử dụng

```typescript
// ═══════════════════════════════════════════════════
// 1. STANDALONE COMPONENT + Signals (Angular 17+)
// ═══════════════════════════════════════════════════
@Component({
  standalone: true,
  selector: 'app-patient-list',
  imports: [CommonModule, RouterModule, SharedModule, AgGridModule],
  templateUrl: './patient-list.component.html',
})
export class PatientListComponent {
  private patientService = inject(PatientService);
  
  patients = signal<Patient[]>([]);
  loading = signal(false);
  searchTerm = signal('');
  
  filteredPatients = computed(() =>
    this.patients().filter(p =>
      p.fullName.toLowerCase().includes(this.searchTerm().toLowerCase())
    )
  );
  
  ngOnInit() {
    this.loadPatients();
  }
  
  async loadPatients() {
    this.loading.set(true);
    this.patients.set(await firstValueFrom(this.patientService.getAll()));
    this.loading.set(false);
  }
}

// ═══════════════════════════════════════════════════
// 2. REACTIVE FORMS + Tailwind template
// ═══════════════════════════════════════════════════
@Component({ ... })
export class PatientFormComponent {
  private fb = inject(FormBuilder);
  
  form = this.fb.group({
    fullName:     ['', [Validators.required, Validators.maxLength(200)]],
    idCardNumber: ['', [Validators.required, idCardValidator]],
    dateOfBirth:  [null as Date | null, Validators.required],
    gender:       ['FEMALE', Validators.required],
    phone:        ['', [Validators.required, phoneValidator]],
    address:      [''],
  });
}

// template:
// <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-6">
//   <div>
//     <label class="block text-sm font-medium text-gray-700">Họ và tên</label>
//     <input formControlName="fullName"
//            class="mt-1 block w-full rounded-md border-gray-300 shadow-sm
//                   focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
//            [class.border-red-500]="form.get('fullName')?.invalid && form.get('fullName')?.touched" />
//     @if (form.get('fullName')?.hasError('required') && form.get('fullName')?.touched) {
//       <p class="mt-1 text-sm text-red-600">Họ tên là bắt buộc</p>
//     }
//   </div>
// </form>

// ═══════════════════════════════════════════════════
// 3. ROLE-BASED DIRECTIVE
// ═══════════════════════════════════════════════════
@Directive({
  standalone: true,
  selector: '[hasRole]',
})
export class HasRoleDirective {
  private authService = inject(AuthService);
  private templateRef = inject(TemplateRef<unknown>);
  private viewContainer = inject(ViewContainerRef);
  
  @Input() set hasRole(roles: string[]) {
    if (this.authService.hasAnyRole(roles)) {
      this.viewContainer.createEmbeddedView(this.templateRef);
    } else {
      this.viewContainer.clear();
    }
  }
}
// Usage: <button *hasRole="['BS_TU_VAN','BS_SIEU_AM']">Chỉ định XN</button>

// ═══════════════════════════════════════════════════
// 4. HTTP SERVICE + Interceptor
// ═══════════════════════════════════════════════════
@Injectable({ providedIn: 'root' })
export class PatientService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/patients`;
  
  getAll(params?: { search?: string; type?: string }): Observable<Patient[]> {
    return this.http.get<Patient[]>(this.baseUrl, { params: params as any });
  }
  
  getById(id: string): Observable<Patient> {
    return this.http.get<Patient>(`${this.baseUrl}/${id}`);
  }
  
  create(data: CreatePatientDto): Observable<Patient> {
    return this.http.post<Patient>(this.baseUrl, data);
  }
  
  update(id: string, data: UpdatePatientDto): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/${id}`, data);
  }
  
  uploadPhoto(id: string, file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('photo', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/${id}/photo`, formData);
  }
}

// Auth interceptor (functional style Angular 17+)
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(TokenService).getAccessToken();
  if (token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        inject(AuthService).logout();
      }
      return throwError(() => error);
    })
  );
};

// ═══════════════════════════════════════════════════
// 5. NgRx SIGNAL STORE (lightweight state)
// ═══════════════════════════════════════════════════
export const PatientStore = signalStore(
  { providedIn: 'root' },
  withState<PatientState>({
    patients: [],
    selectedPatient: null,
    loading: false,
    error: null,
  }),
  withComputed((store) => ({
    patientCount: computed(() => store.patients().length),
  })),
  withMethods((store, patientService = inject(PatientService)) => ({
    loadAll: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true })),
        switchMap(() => patientService.getAll()),
        tap((patients) => patchState(store, { patients, loading: false })),
      )
    ),
    selectPatient(patient: Patient) {
      patchState(store, { selectedPatient: patient });
    },
  }))
);
```

### 1.2 Hệ thống modules

```
ivfmd-system/
├── modules/
│   ├── auth/                  # Xác thực, phân quyền
│   ├── patient/               # Quản lý bệnh nhân
│   ├── appointment/           # Lịch hẹn & STT (số thứ tự)
│   ├── consultation/          # Khám & tư vấn
│   ├── ultrasound/            # Siêu âm (phụ khoa, nang noãn, thai, niêm mạc)
│   ├── lab/                   # Xét nghiệm (máu, nội tiết)
│   ├── stimulation/           # Kích thích buồng trứng
│   ├── procedure/             # Thủ thuật (chọc hút, IUI, chuyển phôi)
│   ├── embryology/            # Phôi học (LABO) — nuôi phôi, trữ phôi
│   ├── andrology/             # Nam khoa (TDĐ, lọc rửa TT)
│   ├── sperm-bank/            # Ngân hàng tinh trùng (NHTT)
│   ├── egg-donor/             # Người cho trứng
│   ├── pregnancy/             # Theo dõi thai
│   ├── endometrium-prep/      # ★ MỚI: Chuyển phôi trữ / FET (CBNMTC)
│   ├── consent/               # ★ MỚI: Consent forms & hợp đồng
│   ├── notification/          # ★ MỚI: SMS/Zalo nhắc lịch, alert mốc thời gian
│   ├── pharmacy/              # Nhà thuốc & toa thuốc
│   ├── billing/               # Thu ngân, hóa đơn, thanh toán
│   ├── inventory/             # Kho — thuốc bù tủ trực, VTTH
│   ├── reporting/             # Báo cáo & thống kê
│   ├── medical-audit/         # ★ MỚI: Audit log y khoa (pháp lý)
│   └── printing/              # In ấn (STT, hóa đơn, KQ XN, toa thuốc)
```

### 1.3 Vai trò người dùng (Roles)

| Role ID | Tên vai trò | Mô tả |
|---------|------------|-------|
| R01 | Admin | Quản trị hệ thống |
| R02 | BS Tư vấn | Bác sĩ khám, tư vấn, ra toa, chỉ định |
| R03 | BS Siêu âm | Bác sĩ siêu âm phụ khoa, nang noãn, thai |
| R04 | BS Hiếm muộn | Bác sĩ thực hiện thủ thuật (chọc hút, chuyển phôi, IUI) |
| R05 | NHS Hành chánh | Nhân viên y tế hành chánh |
| R06 | Tiếp đón + Thu ngân | Tiếp nhận BN, thu tiền |
| R07 | Nam khoa | KTV nam khoa (TDĐ, lọc rửa) |
| R08 | Xét nghiệm | KTV xét nghiệm |
| R09 | LABO (Phôi học) | Kỹ thuật viên phôi học |
| R10 | Nhà thuốc | Dược sĩ |
| R11 | KTV Gây mê | Kỹ thuật viên gây mê |
| R12 | Khoa Dược | Quản lý dược, duyệt phiếu |
| R13 | Thư ký y khoa | Hỗ trợ hành chánh (đặc biệt cho người cho trứng) |

---

## 2. DATABASE SCHEMA

### 2.1 Core Entities

```sql
-- ============================================
-- BỆNH NHÂN & HỒ SƠ
-- ============================================

CREATE TABLE patients (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_code        VARCHAR(20) UNIQUE NOT NULL,      -- Mã BN (auto-gen)
    archive_code        VARCHAR(20) UNIQUE,               -- Số hồ sơ lưu trữ
    full_name           VARCHAR(200) NOT NULL,
    id_card_number      VARCHAR(20),                      -- CMND/CCCD
    date_of_birth       DATE,
    gender              VARCHAR(10) CHECK (gender IN ('MALE','FEMALE')),
    phone               VARCHAR(20),
    address             TEXT,                              -- Theo hộ khẩu
    photo_url           VARCHAR(500),                     -- Ảnh chụp camera
    fingerprint_data    BYTEA,                            -- Dấu vân tay (cho NCT/NH)
    patient_type        VARCHAR(20) DEFAULT 'INFERTILITY',-- INFERTILITY, EGG_DONOR, SPERM_DONOR
    is_active           BOOLEAN DEFAULT TRUE,
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    updated_at          TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE couples (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    wife_id         UUID NOT NULL REFERENCES patients(id),
    husband_id      UUID REFERENCES patients(id),
    couple_code     VARCHAR(20) UNIQUE NOT NULL,
    married_date    DATE,
    infertility_duration_months INT,
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- CHU KỲ ĐIỀU TRỊ (Treatment Cycle)
-- ============================================

CREATE TABLE treatment_cycles (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    couple_id       UUID NOT NULL REFERENCES couples(id),
    cycle_code      VARCHAR(20) UNIQUE NOT NULL,          -- Mã chu kỳ
    cycle_number    INT NOT NULL DEFAULT 1,               -- Lần thứ mấy
    treatment_type  VARCHAR(20) NOT NULL,                 -- QHTN, IUI, ICSI, IVM
    status          VARCHAR(30) NOT NULL DEFAULT 'CONSULTATION',
    -- Status flow: CONSULTATION → LAB_TESTING → STIMULATION → TRIGGER → 
    --              PROCEDURE → EMBRYO_CULTURE → TRANSFER → PREGNANCY_TEST →
    --              PREGNANT / NOT_PREGNANT / FROZEN_ALL / CANCELLED
    start_date      DATE NOT NULL,
    end_date        DATE,
    doctor_id       UUID REFERENCES users(id),
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- SỐ THỨ TỰ (Queue/Ticket)
-- ============================================

CREATE TABLE queue_tickets (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_number   VARCHAR(20) NOT NULL,
    ticket_type     VARCHAR(30) NOT NULL,
    -- Types: FIRST_VISIT, CONSULTATION, ULTRASOUND, LAB_TEST, 
    --        ADMIN, SAMPLE_COLLECTION, EMBRYO_REPORT
    patient_id      UUID NOT NULL REFERENCES patients(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    department      VARCHAR(50) NOT NULL,
    status          VARCHAR(20) DEFAULT 'WAITING',        -- WAITING, CALLED, COMPLETED, CANCELLED
    called_at       TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- LỊCH HẸN
-- ============================================

CREATE TABLE appointments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    couple_id       UUID REFERENCES couples(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    appointment_type VARCHAR(50) NOT NULL,
    -- Types: FOLLOW_UP, STIMULATION_CHECK, TRIGGER_DAY, OPU (chọc hút), 
    --        IUI, EMBRYO_TRANSFER, PREGNANCY_TEST, PRENATAL_7W,
    --        ENDOMETRIUM_PREP, SPERM_BANK_SAMPLE, EGG_DONOR_FOLLOW_UP
    scheduled_date  DATE NOT NULL,
    scheduled_time  TIME,
    doctor_id       UUID REFERENCES users(id),
    department      VARCHAR(50),
    status          VARCHAR(20) DEFAULT 'SCHEDULED',      -- SCHEDULED, CHECKED_IN, COMPLETED, NO_SHOW, CANCELLED
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- SIÊU ÂM
-- ============================================

CREATE TABLE ultrasound_exams (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    exam_type       VARCHAR(30) NOT NULL,
    -- Types: GYNECOLOGY (phụ khoa), FOLLICLE (nang noãn), 
    --        ENDOMETRIUM (niêm mạc), PRENATAL (thai), BREAST (nhũ)
    exam_date       TIMESTAMPTZ NOT NULL,
    doctor_id       UUID NOT NULL REFERENCES users(id),
    
    -- Kết quả SA phụ khoa
    uterus_size     VARCHAR(100),
    left_ovary      VARCHAR(200),
    right_ovary     VARCHAR(200),
    endometrium_thickness DECIMAL(5,2),                   -- mm
    
    -- Kết quả SA nang noãn (follicle tracking)
    follicle_data   JSONB,
    -- Cấu trúc: { "left": [{"size": 18, "count": 3}], "right": [...] }
    
    -- Kết quả SA thai
    gestational_age_weeks INT,
    gestational_age_days  INT,
    fetal_heart_rate      INT,
    fetal_count           INT,
    
    result_summary  TEXT,
    is_abnormal     BOOLEAN DEFAULT FALSE,
    abnormal_action VARCHAR(50),                          -- IVM, FREEZE_ALL, STOP_TREATMENT
    
    status          VARCHAR(20) DEFAULT 'COMPLETED',
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- XÉT NGHIỆM
-- ============================================

CREATE TABLE lab_orders (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    order_type      VARCHAR(30) NOT NULL,
    -- Types: ROUTINE (thường quy), HORMONAL (nội tiết), PRE_ANESTHESIA (tiền mê),
    --        BETA_HCG, HIV_SCREENING, BLOOD_TYPE, SPERM_ANALYSIS
    ordered_by      UUID NOT NULL REFERENCES users(id),
    ordered_at      TIMESTAMPTZ DEFAULT NOW(),
    status          VARCHAR(20) DEFAULT 'ORDERED',        -- ORDERED, SAMPLE_COLLECTED, IN_PROGRESS, COMPLETED, DELIVERED
    result_delivered_to VARCHAR(50),                       -- PATIENT, NHS, DOCTOR
    notes           TEXT
);

CREATE TABLE lab_tests (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id        UUID NOT NULL REFERENCES lab_orders(id),
    test_code       VARCHAR(50) NOT NULL,
    test_name       VARCHAR(200) NOT NULL,
    -- Tên XN: AMH, E2, P4, LH, FSH, BetaHCG, HIV, HBsAg, Anti_HCV, BW, 
    --         Blood_Group_RH, CBC, ECG, Semen_Analysis...
    result_value    VARCHAR(200),
    result_unit     VARCHAR(50),
    reference_range VARCHAR(100),
    is_abnormal     BOOLEAN DEFAULT FALSE,
    completed_at    TIMESTAMPTZ,
    performed_by    UUID REFERENCES users(id)
);

-- ============================================
-- TOA THUỐC & CHỈ ĐỊNH
-- ============================================

CREATE TABLE prescriptions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    prescribed_by   UUID NOT NULL REFERENCES users(id),
    prescription_type VARCHAR(30) NOT NULL,
    -- Types: STIMULATION (KTBT), TRIGGER (thuốc rụng trứng), 
    --        LUTEAL_SUPPORT (hỗ trợ hoàng thể), ANTIBIOTIC,
    --        ENDOMETRIUM_PREP (chuẩn bị NM), PREGNANCY_SUPPORT, GENERAL
    prescribed_date TIMESTAMPTZ DEFAULT NOW(),
    status          VARCHAR(20) DEFAULT 'CREATED',        -- CREATED, ENTERED_PM, PRINTED, DISPENSED
    entered_by      UUID REFERENCES users(id),            -- NHS nhập vào PM
    notes           TEXT
);

CREATE TABLE prescription_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    prescription_id UUID NOT NULL REFERENCES prescriptions(id),
    medication_name VARCHAR(200) NOT NULL,
    dosage          VARCHAR(100),
    frequency       VARCHAR(100),
    duration        VARCHAR(100),
    quantity        DECIMAL(10,2),
    unit            VARCHAR(30),
    route           VARCHAR(30),                          -- ORAL, INJECTION, VAGINAL
    instructions    TEXT
);

-- ============================================
-- THỦ THUẬT
-- ============================================

CREATE TABLE procedures (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    cycle_id        UUID NOT NULL REFERENCES treatment_cycles(id),
    procedure_type  VARCHAR(30) NOT NULL,
    -- Types: OPU (chọc hút), IUI, EMBRYO_TRANSFER_FRESH, EMBRYO_TRANSFER_FROZEN,
    --        IVM_OPU (chọc hút trứng non)
    procedure_date  TIMESTAMPTZ NOT NULL,
    doctor_id       UUID NOT NULL REFERENCES users(id),
    anesthetist_id  UUID REFERENCES users(id),
    
    -- Chọc hút
    eggs_retrieved  INT,
    mature_eggs     INT,
    immature_eggs   INT,
    
    -- IUI
    sperm_source    VARCHAR(20),                          -- HUSBAND, SPERM_BANK
    sperm_bank_code VARCHAR(50),
    
    -- Chuyển phôi
    embryos_transferred INT,
    embryo_quality  VARCHAR(100),
    transfer_day    VARCHAR(5),                           -- N3, N5, N6
    
    -- Theo dõi sau thủ thuật
    pre_bp          VARCHAR(20),                          -- Huyết áp trước
    post_bp         VARCHAR(20),                          -- Huyết áp sau
    complications   TEXT,
    
    status          VARCHAR(20) DEFAULT 'SCHEDULED',
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- PHÔI HỌC (LABO)
-- ============================================

CREATE TABLE embryos (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cycle_id        UUID NOT NULL REFERENCES treatment_cycles(id),
    couple_id       UUID NOT NULL REFERENCES couples(id),
    embryo_code     VARCHAR(50) UNIQUE NOT NULL,
    
    -- Thông tin phôi
    day             INT NOT NULL,                         -- N0, N1, N2, N3, N5, N6
    grade           VARCHAR(20),                          -- Grade A, B, C...
    cell_count      INT,
    fragmentation   VARCHAR(20),
    morphology      JSONB,                                -- Chi tiết hình thái
    
    -- Trạng thái
    status          VARCHAR(20) NOT NULL DEFAULT 'CULTURING',
    -- CULTURING, TRANSFERRED, FROZEN, THAWED, DISCARDED
    
    frozen_at       TIMESTAMPTZ,
    thawed_at       TIMESTAMPTZ,
    transferred_at  TIMESTAMPTZ,
    discarded_at    TIMESTAMPTZ,
    
    -- Trữ phôi
    storage_tank    VARCHAR(50),
    storage_position VARCHAR(50),
    straw_code      VARCHAR(50),                          -- Mã top/cọng rạ
    
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE embryo_freezing_contracts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    couple_id       UUID NOT NULL REFERENCES couples(id),
    cycle_id        UUID NOT NULL REFERENCES treatment_cycles(id),
    contract_code   VARCHAR(50) UNIQUE NOT NULL,
    
    total_embryos   INT NOT NULL,
    straw_count     INT NOT NULL,                         -- Số top
    -- Phôi N3: 1-3 phôi/top, Phôi N5&N6: 1-2 phôi/top, Đặc biệt: 1 phôi/top
    
    base_fee        DECIMAL(12,2) NOT NULL,               -- 8tr/top đầu
    additional_fee  DECIMAL(12,2) DEFAULT 0,              -- 2tr/top phát sinh
    total_fee       DECIMAL(12,2) NOT NULL,
    
    start_date      DATE NOT NULL,
    expiry_date     DATE NOT NULL,
    status          VARCHAR(20) DEFAULT 'ACTIVE',         -- ACTIVE, EXPIRED, TERMINATED
    
    signed_at       TIMESTAMPTZ,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- NAM KHOA
-- ============================================

CREATE TABLE semen_analyses (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),    -- Người chồng
    cycle_id        UUID REFERENCES treatment_cycles(id),
    analysis_type   VARCHAR(20) NOT NULL,                     -- DIAGNOSTIC (TDĐ), PRE_IUI, PRE_ICSI, PRE_IVM
    
    -- Trước lọc rửa
    volume          DECIMAL(5,2),                             -- ml
    concentration   DECIMAL(10,2),                            -- triệu/ml
    motility_a      DECIMAL(5,2),                             -- % di động nhanh
    motility_b      DECIMAL(5,2),                             -- % di động chậm
    morphology_normal DECIMAL(5,2),                           -- % hình thái bình thường
    
    -- Sau lọc rửa (nếu có)
    post_wash_volume        DECIMAL(5,2),
    post_wash_concentration DECIMAL(10,2),
    post_wash_motility      DECIMAL(5,2),
    
    sample_time     TIMESTAMPTZ,
    abstinence_days INT,
    performed_by    UUID REFERENCES users(id),
    result_status   VARCHAR(20) DEFAULT 'PENDING',
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- NGÂN HÀNG TINH TRÙNG (NHTT)
-- ============================================

CREATE TABLE sperm_donors (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),    -- Liên kết hồ sơ BN
    donor_code      VARCHAR(50) UNIQUE NOT NULL,
    photo_url       VARCHAR(500) NOT NULL,
    fingerprint_data BYTEA NOT NULL,
    
    -- XN sàng lọc
    hiv_status      VARCHAR(20),
    hbsag_status    VARCHAR(20),
    anti_hcv_status VARCHAR(20),
    bw_status       VARCHAR(20),
    blood_group     VARCHAR(10),
    rh_factor       VARCHAR(10),
    semen_quality   VARCHAR(20),                              -- PASS, FAIL
    
    -- Trạng thái
    screening_status VARCHAR(20) DEFAULT 'PENDING',
    -- PENDING, SCREENING, SAMPLE_1, SAMPLE_2, HIV_RETEST, APPROVED, REJECTED
    
    hiv_retest_date DATE,                                     -- Hẹn XN HIV lần 2 sau 3 tháng
    approved_at     TIMESTAMPTZ,
    
    referred_by     UUID REFERENCES users(id),                -- BS giới thiệu
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE sperm_bank_samples (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    donor_id        UUID NOT NULL REFERENCES sperm_donors(id),
    sample_number   INT NOT NULL,                             -- Lần lấy mẫu (1, 2)
    nhtt_code       VARCHAR(50) UNIQUE,                       -- Mã NHTT (cấp sau HIV lần 2)
    
    sample_date     TIMESTAMPTZ NOT NULL,
    quality_pass    BOOLEAN,
    
    -- Trữ lạnh
    is_frozen       BOOLEAN DEFAULT FALSE,
    frozen_at       TIMESTAMPTZ,
    storage_tank    VARCHAR(50),
    storage_position VARCHAR(50),
    straw_count     INT,
    straws_remaining INT,
    
    -- Sử dụng
    status          VARCHAR(20) DEFAULT 'COLLECTED',
    -- COLLECTED, FROZEN, IN_USE, DEPLETED, DESTROYED
    
    destroyed_at    TIMESTAMPTZ,
    destroy_reason  TEXT,
    
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE sperm_bank_usage (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sample_id       UUID NOT NULL REFERENCES sperm_bank_samples(id),
    recipient_couple_id UUID NOT NULL REFERENCES couples(id),
    cycle_id        UUID NOT NULL REFERENCES treatment_cycles(id),
    procedure_type  VARCHAR(20) NOT NULL,                     -- IUI, ICSI
    
    used_date       TIMESTAMPTZ NOT NULL,
    straws_used     INT NOT NULL DEFAULT 1,
    straws_remaining_after INT NOT NULL,
    
    performed_by    UUID REFERENCES users(id),
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Liên kết người cho trứng với cặp vợ chồng
CREATE TABLE sperm_donor_recipients (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    donor_id        UUID NOT NULL REFERENCES sperm_donors(id),
    couple_id       UUID NOT NULL REFERENCES couples(id),
    matched_by      UUID REFERENCES users(id),                -- NHS gắn
    matched_at      TIMESTAMPTZ DEFAULT NOW(),
    status          VARCHAR(20) DEFAULT 'ACTIVE'
);

-- ============================================
-- NGƯỜI CHO TRỨNG
-- ============================================

CREATE TABLE egg_donors (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    donor_code      VARCHAR(50) UNIQUE NOT NULL,
    photo_url       VARCHAR(500) NOT NULL,
    fingerprint_data BYTEA NOT NULL,
    
    screening_status VARCHAR(20) DEFAULT 'PENDING',
    -- PENDING, SA_DONE, LAB_TESTING, APPROVED, REJECTED, STIMULATING, COMPLETED
    previous_donations INT DEFAULT 0,
    
    breast_ultrasound_result TEXT,                             -- KQ SA nhũ
    pre_anesthesia_cleared BOOLEAN DEFAULT FALSE,
    
    approved_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE egg_donor_recipients (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    donor_id        UUID NOT NULL REFERENCES egg_donors(id),
    couple_id       UUID NOT NULL REFERENCES couples(id),     -- Cặp vợ chồng nhận
    matched_by      UUID REFERENCES users(id),                -- NHS gắn trên PM
    matched_at      TIMESTAMPTZ DEFAULT NOW(),
    status          VARCHAR(20) DEFAULT 'ACTIVE'
);

-- ============================================
-- TÀI CHÍNH
-- ============================================

CREATE TABLE invoices (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_number  VARCHAR(30) UNIQUE NOT NULL,
    patient_id      UUID NOT NULL REFERENCES patients(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    
    invoice_type    VARCHAR(30) NOT NULL,
    -- Types: CONSULTATION_FEE, ULTRASOUND_FEE, LAB_FEE, PROCEDURE_FEE,
    --        MEDICATION_FEE, INJECTION_FEE, FREEZING_FEE, 
    --        SPERM_BANK_FEE, SAMPLE_USAGE_FEE
    
    subtotal        DECIMAL(12,2) NOT NULL,
    discount        DECIMAL(12,2) DEFAULT 0,
    total           DECIMAL(12,2) NOT NULL,
    
    payment_method  VARCHAR(20),                              -- CASH, CARD, TRANSFER
    payment_status  VARCHAR(20) DEFAULT 'UNPAID',             -- UNPAID, PAID, REFUNDED, PARTIAL
    paid_at         TIMESTAMPTZ,
    
    -- Hoàn tiền (vd: hoàn tiền chuyển phôi khi TPTB)
    refund_amount   DECIMAL(12,2) DEFAULT 0,
    refund_reason   TEXT,
    refunded_at     TIMESTAMPTZ,
    
    cashier_id      UUID REFERENCES users(id),
    printed_at      TIMESTAMPTZ,
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE invoice_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id      UUID NOT NULL REFERENCES invoices(id),
    service_code    VARCHAR(50) NOT NULL,
    service_name    VARCHAR(200) NOT NULL,
    quantity        INT DEFAULT 1,
    unit_price      DECIMAL(12,2) NOT NULL,
    total_price     DECIMAL(12,2) NOT NULL,
    notes           TEXT
);

-- ============================================
-- KHO - THUỐC & VẬT TƯ
-- ============================================

CREATE TABLE inventory_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    item_code       VARCHAR(50) UNIQUE NOT NULL,
    item_name       VARCHAR(200) NOT NULL,
    category        VARCHAR(30) NOT NULL,                     -- MEDICATION, CONSUMABLE
    unit            VARCHAR(30) NOT NULL,
    current_stock   DECIMAL(10,2) DEFAULT 0,
    min_stock       DECIMAL(10,2) DEFAULT 0,
    location        VARCHAR(50),                              -- TU_TRUC, KHO_CHINH
    is_trackable    BOOLEAN DEFAULT TRUE,                     -- Có xuất kho hay không
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE inventory_requests (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    request_type    VARCHAR(30) NOT NULL,
    -- Types: RESTOCK_DUTY (bù tủ trực), CONSUMABLE_USAGE (hao phí VTTH), 
    --        PURCHASE_ORDER (đặt mua)
    requested_by    UUID NOT NULL REFERENCES users(id),
    approved_by     UUID REFERENCES users(id),
    status          VARCHAR(20) DEFAULT 'DRAFT',
    -- DRAFT, SUBMITTED, APPROVED, PRINTED, DISPENSED
    submitted_at    TIMESTAMPTZ,
    approved_at     TIMESTAMPTZ,
    dispensed_at    TIMESTAMPTZ,
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- USERS & AUTH
-- ============================================

CREATE TABLE users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username        VARCHAR(100) UNIQUE NOT NULL,
    password_hash   VARCHAR(255) NOT NULL,
    full_name       VARCHAR(200) NOT NULL,
    role            VARCHAR(30) NOT NULL,
    department      VARCHAR(50),
    is_active       BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- AUDIT LOG
-- ============================================

CREATE TABLE audit_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID REFERENCES users(id),
    action          VARCHAR(50) NOT NULL,
    entity_type     VARCHAR(50) NOT NULL,
    entity_id       UUID,
    old_data        JSONB,
    new_data        JSONB,
    ip_address      VARCHAR(50),
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- CHUẨN BỊ NIÊM MẠC CHUYỂN PHÔI TRỮ (FET)
-- ============================================

CREATE TABLE endometrium_prep_cycles (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    couple_id           UUID NOT NULL REFERENCES couples(id),
    original_cycle_id   UUID REFERENCES treatment_cycles(id),  -- Chu kỳ gốc đã trữ phôi
    cycle_code          VARCHAR(20) UNIQUE NOT NULL,
    start_date          DATE NOT NULL,                         -- Ngày 2/3 VK
    status              VARCHAR(30) DEFAULT 'INITIAL_EXAM',
    -- Flow: INITIAL_EXAM → MEDICATION → MONITORING → READY → TRANSFERRED → PREGNANCY_TEST
    medication_protocol TEXT,                                   -- CBNMTC protocol
    endometrium_target  DECIMAL(5,2),                          -- mm mục tiêu
    requires_two_doctors BOOLEAN DEFAULT TRUE,                 -- Lần đầu: 2 BS, tái khám: 1 BS
    doctor_id           UUID REFERENCES users(id),
    notes               TEXT,
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    updated_at          TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE endometrium_scans (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    prep_cycle_id       UUID NOT NULL REFERENCES endometrium_prep_cycles(id),
    scan_number         INT NOT NULL,                          -- Lần SA thứ mấy
    scan_date           TIMESTAMPTZ NOT NULL,
    thickness           DECIMAL(5,2) NOT NULL,                 -- mm
    pattern             VARCHAR(30),                           -- Triple-line, Hyperechoic...
    doctor_id           UUID NOT NULL REFERENCES users(id),
    is_ready            BOOLEAN DEFAULT FALSE,                 -- NM đạt chưa
    medication_adjustment TEXT,                                 -- Điều chỉnh thuốc
    next_scan_date      DATE,
    notes               TEXT,
    created_at          TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- CONSENT / CAM KẾT / HỢP ĐỒNG
-- ============================================

CREATE TABLE consent_forms (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id          UUID NOT NULL REFERENCES patients(id),
    couple_id           UUID REFERENCES couples(id),
    cycle_id            UUID REFERENCES treatment_cycles(id),
    form_type           VARCHAR(50) NOT NULL,
    -- Types: OPU_CONSENT, ANESTHESIA_CONSENT, EMBRYO_TRANSFER_CONSENT,
    --        IUI_CONSENT, FREEZING_CONTRACT, SPERM_BANK_PARTICIPATION,
    --        EGG_DONATION_CONSENT, TREATMENT_CONSENT
    template_version    VARCHAR(20),
    signed_at           TIMESTAMPTZ,
    signed_by_patient   BOOLEAN DEFAULT FALSE,
    signed_by_spouse    BOOLEAN DEFAULT FALSE,
    witness_id          UUID REFERENCES users(id),
    document_url        VARCHAR(500),                          -- Scan/PDF bản đã ký
    status              VARCHAR(20) DEFAULT 'PENDING',         -- PENDING, SIGNED, EXPIRED, REVOKED
    created_at          TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- LOG TIÊM THUỐC THỰC TẾ
-- ============================================

CREATE TABLE medication_administrations (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id              UUID NOT NULL REFERENCES patients(id),
    cycle_id                UUID REFERENCES treatment_cycles(id),
    prescription_item_id    UUID REFERENCES prescription_items(id),
    medication_name         VARCHAR(200) NOT NULL,
    dose                    VARCHAR(100),
    route                   VARCHAR(30),                       -- INJECTION, ORAL, VAGINAL
    administered_at         TIMESTAMPTZ NOT NULL,               -- Giờ tiêm chính xác
    administered_by         UUID REFERENCES users(id),         -- KTV phòng tiêm
    location                VARCHAR(50),                       -- IVFMD, BV_INJECTION_ROOM
    is_trigger_shot         BOOLEAN DEFAULT FALSE,             -- Mũi trigger rụng trứng (quan trọng cho tính 36h)
    notes                   TEXT,
    created_at              TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- PHÍ CHU KỲ (tracking phí thu 1 lần / chu kỳ)
-- ============================================

CREATE TABLE cycle_fees (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cycle_id        UUID NOT NULL,                             -- Có thể là treatment_cycle hoặc endometrium_prep
    cycle_type      VARCHAR(30) NOT NULL,                      -- TREATMENT, ENDOMETRIUM_PREP
    fee_type        VARCHAR(50) NOT NULL,
    -- Types: FOLLICLE_SCAN_CYCLE (SA nang noãn suốt CK),
    --        ENDOMETRIUM_SCAN_CYCLE (SA NMTC suốt CK),
    --        OPU_AND_TRANSFER (chọc hút + chuyển phôi)
    invoice_id      UUID REFERENCES invoices(id),
    paid            BOOLEAN DEFAULT FALSE,
    paid_at         TIMESTAMPTZ,
    refunded        BOOLEAN DEFAULT FALSE,
    refund_amount   DECIMAL(12,2) DEFAULT 0,
    refund_invoice_id UUID REFERENCES invoices(id),
    notes           TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(cycle_id, cycle_type, fee_type)                     -- 1 fee type / cycle
);

-- ============================================
-- TRACKING HỒ SƠ GIẤY (giai đoạn chuyển đổi)
-- ============================================

CREATE TABLE file_tracking (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id          UUID NOT NULL REFERENCES patients(id),
    file_code           VARCHAR(50) NOT NULL,                  -- Mã hồ sơ vật lý
    current_department  VARCHAR(50) NOT NULL,
    current_holder_id   UUID REFERENCES users(id),
    transferred_from    VARCHAR(50),
    transferred_at      TIMESTAMPTZ DEFAULT NOW(),
    notes               TEXT
);

-- ============================================
-- NHẮC LỊCH / THÔNG BÁO
-- ============================================

CREATE TABLE notifications (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    notification_type VARCHAR(30) NOT NULL,
    -- Types: APPOINTMENT_REMINDER, TRIGGER_TIME, OPU_36H, IUI_36H,
    --        MEDICATION_REMINDER, HIV_RETEST_3M, FREEZING_EXPIRY,
    --        LAB_RESULT_READY, PREGNANCY_TEST_DUE
    channel         VARCHAR(20) NOT NULL,                      -- SMS, ZALO, IN_APP, EMAIL
    message         TEXT NOT NULL,
    scheduled_at    TIMESTAMPTZ NOT NULL,
    sent_at         TIMESTAMPTZ,
    status          VARCHAR(20) DEFAULT 'PENDING',             -- PENDING, SENT, FAILED, CANCELLED
    related_entity_type VARCHAR(50),
    related_entity_id   UUID,
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- TEMPLATE TOA THUỐC
-- ============================================

CREATE TABLE prescription_templates (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_name   VARCHAR(200) NOT NULL,
    template_type   VARCHAR(30) NOT NULL,
    -- Types: STIMULATION_PROTOCOL, TRIGGER, LUTEAL_SUPPORT, 
    --        ANTIBIOTIC_POST_OPU, ENDOMETRIUM_PREP, PREGNANCY_SUPPORT
    items           JSONB NOT NULL,                            -- Array of {medication, dosage, frequency, duration, route}
    is_active       BOOLEAN DEFAULT TRUE,
    created_by      UUID REFERENCES users(id),
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- AUDIT LOG (đã có ở trên - bổ sung MEDICAL audit)
-- ============================================

CREATE TABLE medical_audit_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id),
    patient_id      UUID REFERENCES patients(id),
    action          VARCHAR(50) NOT NULL,                      -- CREATE, UPDATE, DELETE, VIEW, PRINT
    entity_type     VARCHAR(50) NOT NULL,                      -- LAB_RESULT, ULTRASOUND, PRESCRIPTION, EMBRYO...
    entity_id       UUID NOT NULL,
    field_changed   VARCHAR(100),                              -- Field cụ thể bị thay đổi
    old_value       TEXT,
    new_value       TEXT,
    reason          TEXT,                                       -- Lý do sửa (bắt buộc cho sửa KQ XN, SA)
    ip_address      VARCHAR(50),
    created_at      TIMESTAMPTZ DEFAULT NOW()
);
```

```
Phase 1  ██████░░░░░░░░░░░░░░░░░░░░░░░░░░░░ Nền tảng + SMS/QR/Alert  (4.5 tuần)
Phase 2  ░░░░░░░██████░░░░░░░░░░░░░░░░░░░░░░ Khám & Tư vấn + Template toa (3 tuần)
Phase 3  ░░░░░░░░░░░░░██████░░░░░░░░░░░░░░░░ KTBT & Theo dõi           (3 tuần)
Phase 4  ░░░░░░░░░░░░░░░░░░░█████░░░░░░░░░░░ Thủ thuật + Consent + IVM (2.5 tuần)
Phase 5  ░░░░░░░░░░░░░░░░░░░░░░░░████░░░░░░░ Phôi học                  (2 tuần)
Phase 5b ░░░░░░░░░░░░░░░░░░░░░░░░░░░░██░░░░░ FET / CBNMTC (MỚI)       (2 tuần)
Phase 6  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░██░░░ Thai kỳ                   (1.5 tuần)
Phase 7  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░██░ Cho trứng                 (2 tuần)
Phase 8  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░██ NHTT                     (2 tuần)
Phase 9  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░██ Kho & VT               (1.5 tuần)
Phase 10 ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░███ Tài chính + HIS     (3 tuần)
         Tuần 1────5──────10──────15──────20──────25───27
         Tuần 1───────5─────────10────────15────────20──────23
```

Tổng ước lượng: **~27 tuần (khoảng 7 tháng)** cho full-feature — tăng 4 tuần so với bản gốc do bổ sung Phase 5b (FET), consent module, IVM pathway, SMS/alert, tích hợp HIS.

---

## 4. PHASE 1 — Nền tảng & Quản lý bệnh nhân (4.5 tuần)

### Tuần 1-2: Setup hạ tầng + Auth + User Management

| Task | Chi tiết | Priority |
|------|----------|----------|
| P1.01 | **Setup Angular project**: `ng new ivfmd-frontend --standalone --style=scss --routing`, cài Tailwind CSS, Angular Material/PrimeNG, AG Grid, ngx-echarts, FullCalendar | CRITICAL |
| P1.01b | **Setup Backend**: NestJS (hoặc .NET Core), Docker, CI/CD, database migrations | CRITICAL |
| P1.02 | Database migration setup (Prisma migrate / Flyway) — bao gồm **tất cả bảng mới**: `endometrium_prep_cycles`, `consent_forms`, `medication_administrations`, `cycle_fees`, `notifications`, `prescription_templates`, `medical_audit_logs` | CRITICAL |
| P1.03 | **Auth module**: Angular `AuthService` + `authGuard` + `authInterceptor` (JWT, refresh token), login page | CRITICAL |
| P1.04 | **RBAC**: 13 role, `roleGuard`, `HasRoleDirective` (*hasRole), menu sidebar ẩn/hiện theo role | CRITICAL |
| P1.05 | User CRUD + quản lý role, department (Angular Reactive Forms) | HIGH |
| P1.06 | **Angular Core**: `ErrorInterceptor`, `LoadingInterceptor`, `NotificationService` (toast), `ApiService` (base HTTP) | HIGH |
| P1.07 | **Shared module**: `DataTableComponent` (AG Grid wrapper), `ConfirmDialogComponent`, `PatientSearchComponent`, `StatusBadgeComponent`, `TimelineComponent`, pipes (`VndCurrencyPipe`, `DateViPipe`) | HIGH |
| P1.07b | **Layout**: `MainLayoutComponent` (sidebar + header + breadcrumb + router-outlet), `AuthLayoutComponent` | HIGH |
| P1.07c | ★ **Medical Audit Log middleware** (backend) — ghi lại mọi thay đổi KQ XN, SA, toa thuốc (ai sửa, giá trị cũ/mới, lý do sửa) — bắt buộc cho pháp lý y khoa | HIGH |
| P1.07d | ★ **Barcode/QR module**: generate QR trên STT, hóa đơn, toa thuốc; scan component cho tiếp đón/thu ngân | HIGH |

### Tuần 3-4: Patient Management + Queue System

| Task | Chi tiết | Priority |
|------|----------|----------|
| P1.08 | **Feature: patients/**: `PatientService`, `PatientStore` (NgRx Signal Store), `PatientListComponent` (AG Grid + tìm kiếm), `PatientCreateComponent` (Reactive Form), `PatientDetailComponent` (tabs) | CRITICAL |
| P1.09 | **Feature: patients/couple**: `CoupleDetailComponent` — liên kết vợ-chồng, hiển thị thông tin cặp | CRITICAL |
| P1.10 | **Feature: queue/**: `QueueService` (HTTP + WebSocket), `ReceptionComponent` (cấp STT), STT phân loại: TV, SA, XN, HC, Lấy mẫu, Báo phôi. **STT multi-liên**: config số liên theo loại (TV=2, SA=2, XN=1), in QR code | CRITICAL |
| P1.11 | **queue/display**: `DisplayComponent` — fullscreen cho TV, auto-refresh via WebSocket; **queue/call**: `CallComponent` — BS/NHS bấm gọi số tiếp theo | HIGH |
| P1.12 | **Feature: appointments/**: `CalendarComponent` (FullCalendar Angular — kéo thả), `ListComponent` (AG Grid + filter ngày/BS/phòng). **Logic 2 BS vs 1 BS**: validate khi tạo hẹn — khám lần đầu / CBNM lần đầu cần 2 BS (SA + TV), tái khám chỉ cần 1 BS | HIGH |
| P1.13 | Upload ảnh BN (camera) + Lấy dấu vân tay — `FileUploadComponent`, tích hợp WebUSB/SDK thiết bị | MEDIUM |
| P1.14 | Tạo số hồ sơ lưu trữ (archive_code) — auto-generate | MEDIUM |
| P1.15 | In "Phiếu khám hiếm muộn" — `PrintService` + `ngx-print` + HTML template | MEDIUM |

### Tuần 4-4.5: Notification & Dashboard (★ CẢI TIẾN MỚI)

| Task | Chi tiết | Priority |
|------|----------|----------|
| P1.16 | ★ **SMS/Zalo nhắc lịch**: `NotificationService` (backend) — gửi nhắc trước 1 ngày cho lịch hẹn, nhắc giờ tiêm trigger, nhắc 36h chọc hút/IUI. Dùng bảng `notifications` + message queue | HIGH |
| P1.17 | ★ **Dashboard theo role**: mỗi role thấy KPI khác nhau — BS: BN chờ khám hôm nay, NHS: toa chờ nhập, LABO: phôi cần update, Thu ngân: hóa đơn chờ thanh toán, Admin: tổng quan | HIGH |
| P1.18 | ★ **File tracking hồ sơ giấy** (giai đoạn chuyển đổi): `FileTrackingComponent` — ghi nhận HS đang ở phòng nào, ai giữ, scan barcode khi chuyển giao | MEDIUM |

**Angular Components & Routes — Phase 1:**
```
features/
├── auth/
│   └── login/                                # LoginComponent
│       └── login.component.ts                # Route: /login
│
├── dashboard/
│   └── dashboard.component.ts                # Route: /dashboard (role-based widgets)
│
├── patients/
│   ├── patients.routes.ts                    # Lazy-loaded route config
│   ├── services/patient.service.ts
│   ├── store/patient.store.ts                # NgRx Signal Store
│   └── pages/
│       ├── patient-list/                     # Route: /patients
│       │   └── patient-list.component.ts     #   AG Grid + tìm kiếm + lọc theo loại BN
│       ├── patient-create/                   # Route: /patients/new
│       │   └── patient-create.component.ts   #   Reactive Form + upload ảnh
│       ├── patient-detail/                   # Route: /patients/:id
│       │   └── patient-detail.component.ts   #   Tab: Thông tin | Chu kỳ | Hóa đơn | Lịch sử
│       └── couple-detail/                    # Route: /patients/:id/couple
│           └── couple-detail.component.ts    #   Thông tin cặp vợ chồng
│
├── queue/
│   ├── queue.routes.ts
│   ├── services/queue.service.ts             # HTTP + WebSocket (realtime STT)
│   └── pages/
│       ├── reception/                        # Route: /queue/reception
│       │   └── reception.component.ts        #   Tiếp đón — cấp STT theo loại
│       ├── display/                          # Route: /queue/display
│       │   └── display.component.ts          #   Fullscreen TV — hiển thị STT đang gọi
│       └── call/                             # Route: /queue/call
│           └── call.component.ts             #   BS/NHS bấm gọi STT tiếp theo
│
├── appointments/
│   ├── appointments.routes.ts
│   └── pages/
│       ├── calendar/                         # Route: /appointments/calendar
│       │   └── calendar.component.ts         #   FullCalendar — kéo thả lịch hẹn
│       └── list/                             # Route: /appointments/list
│           └── list.component.ts             #   Bảng danh sách lịch hẹn + filter
│
└── admin/
    ├── admin.routes.ts
    └── pages/
        ├── user-management/                  # Route: /admin/users
        │   └── user-management.component.ts  #   CRUD users + gán role
        └── role-management/                  # Route: /admin/roles
            └── role-management.component.ts  #   Quản lý 13 roles + permissions
```

---

## 5. PHASE 2 — Quy trình Khám & Tư vấn (3 tuần)

> Covers: IVFMD.01, IVFMD.02

### Tuần 5: Module Siêu âm + Xét nghiệm

| Task | Chi tiết | Priority |
|------|----------|----------|
| P2.01 | **Module SA phụ khoa**: form nhập KQ SA, in KQ. **Medical audit**: mọi thay đổi KQ SA đều log lý do sửa | CRITICAL |
| P2.02 | **Module Chỉ định XN**: BS tick chỉ định trên PM (thay giấy!). Phân biệt **XN tại IVFMD vs XN tại BV** (BP XN BV, Phòng cấp cứu ECG) — ghi nhận XN ngoài hệ thống | CRITICAL |
| P2.03 | **Module XN**: tiếp nhận chỉ định, nhập KQ, trả KQ cho NHS. **Medical audit**: log mọi thay đổi KQ XN | CRITICAL |
| P2.04 | Phân biệt: XN thường quy (AMH, TDĐ) vs XN nội tiết (E2, P4, LH) vs XN tiền mê vs BetaHCG | HIGH |
| P2.05 | Workflow KQ XN: XN → hoàn tất → chuyển NHS (không trả BN trực tiếp cho một số loại) | HIGH |

### Tuần 6: Module Tư vấn + Toa thuốc

| Task | Chi tiết | Priority |
|------|----------|----------|
| P2.06 | **Khám tư vấn lần đầu**: form khám, ghi nhận bệnh sử, tiền căn | CRITICAL |
| P2.07 | **Tư vấn sau KQ XN**: BS xem KQ CLS, chọn hướng điều trị (QHTN/IUI/ICSI/IVM) | CRITICAL |
| P2.08 | **Tạo chu kỳ điều trị (Treatment Cycle)**: khi BS quyết định phương pháp | CRITICAL |
| P2.09 | **Module Toa thuốc**: BS chỉ định trên PM → NHS nhập toa (hoặc BS nhập trực tiếp sau cải tiến) → In toa | CRITICAL |
| P2.09b | ★ **Template toa thuốc**: BS chọn template có sẵn (KTBT protocol, Hỗ trợ hoàng thể, CBNMTC, Kháng sinh sau CH...) thay vì nhập từng thuốc. CRUD template trong admin | HIGH |
| P2.10 | Logic phân luồng: N2/N3 VK → hẹn tái khám; Thực hiện ngay → chỉ định tiêm KTBT. ★ **BR: BN quyết định không thực hiện ngay** → hệ thống ghi nhận, không tạo cycle | HIGH |
| P2.10b | ★ **BR4 — Miễn phí tư vấn**: NHS quyết định có thu phí tư vấn hay không dựa trên ghi chú BS. Thêm field `waive_consultation_fee` trên form tư vấn, khi tick → không tạo invoice tư vấn | HIGH |
| P2.11 | Chỉ định XN tiền mê + Đo ECG (cho IVF path). Hẹn ngày khám tiền mê sau khi có KQ XN | HIGH |

### Tuần 7: Module Nhà thuốc + Tiêm thuốc

| Task | Chi tiết | Priority |
|------|----------|----------|
| P2.12 | **Module Nhà thuốc**: nhận toa từ PM → tạo hóa đơn thuốc → thu tiền → phát thuốc. **Lưu ý**: nhà thuốc BV dùng PM chung BV hay PM IVFMD? → Cần config | HIGH |
| P2.13 | **Ghi nhận tiêm thuốc**: log vào bảng `medication_administrations` — giờ tiêm chính xác, ai tiêm, thuốc gì, liều. **Phân biệt**: tiêm tại IVFMD vs tiêm tại Phòng Tiêm BV (thu phí tiêm tại Thu Ngân BV) | HIGH |
| P2.14 | **Workflow hoàn chỉnh lần đầu**: BN vào → STT → HC → SA → TV → XN → Toa → Thuốc → Tiêm → Hẹn | HIGH |

**Angular Components & Routes — Phase 2:**
```
features/
├── consultation/
│   ├── consultation.routes.ts
│   ├── services/consultation.service.ts
│   └── pages/
│       ├── first-visit/                          # Route: /consultation/first-visit
│       │   └── first-visit.component.ts          #   Multi-step form: bệnh sử → khám → chỉ định
│       ├── follow-up/                            # Route: /consultation/follow-up
│       │   └── follow-up.component.ts            #   BS xem KQ CLS + quyết định hướng điều trị
│       └── treatment-decision/                   # Route: /consultation/:id/decision
│           └── treatment-decision.component.ts   #   Chọn QHTN/IUI/ICSI/IVM → tạo cycle
│
├── ultrasound/
│   ├── ultrasound.routes.ts
│   ├── services/ultrasound.service.ts
│   └── pages/
│       ├── gynecology/                           # Route: /ultrasound/gynecology
│       │   └── gynecology.component.ts           #   Queue STT + form nhập KQ SA phụ khoa
│       └── result-detail/                        # Route: /ultrasound/results/:id
│           └── result-detail.component.ts        #   Xem + in KQ SA
│
├── lab/
│   ├── lab.routes.ts
│   ├── services/lab.service.ts
│   └── pages/
│       ├── order-list/                           # Route: /lab/orders
│       │   └── order-list.component.ts           #   DS chỉ định XN theo trạng thái
│       ├── result-entry/                         # Route: /lab/entry
│       │   └── result-entry.component.ts         #   Nhập KQ XN (batch / từng mẫu)
│       └── result-view/                          # Route: /lab/results/:id
│           └── result-view.component.ts          #   Xem + in KQ XN
│
├── prescription/
│   ├── prescription.routes.ts
│   ├── services/prescription.service.ts
│   └── pages/
│       ├── create/                               # Route: /prescription/create
│       │   └── create.component.ts               #   BS tạo toa / NHS nhập từ giấy
│       └── print-preview/                        # Route: /prescription/:id/print
│           └── print-preview.component.ts        #   Preview + in toa thuốc
│
├── pharmacy/
│   ├── pharmacy.routes.ts
│   └── pages/
│       ├── pending/                              # Route: /pharmacy/pending
│       │   └── pending.component.ts              #   DS toa chờ phát thuốc
│       └── dispense/                             # Route: /pharmacy/dispense/:id
│           └── dispense.component.ts             #   Phát thuốc + tạo hóa đơn + thu tiền
│
└── treatment-cycles/
    ├── treatment-cycles.routes.ts
    ├── services/cycle.service.ts
    └── pages/
        ├── cycle-list/                           # Route: /treatment-cycles
        │   └── cycle-list.component.ts           #   DS chu kỳ (filter theo BN, trạng thái)
        └── cycle-detail/                         # Route: /treatment-cycles/:id
            └── cycle-detail.component.ts         #   Timeline + tabs: SA | XN | Toa | TT | Phôi
```

---

## 6. PHASE 3 — Kích thích buồng trứng & Theo dõi (3 tuần)

> Covers: IVFMD.10, IVFMD.11, IVFMD.20, IVFMD.21, IVFMD.60

### Tuần 8-9: Module SA Nang noãn + Theo dõi KTBT

| Task | Chi tiết | Priority |
|------|----------|----------|
| P3.01 | **SA Nang noãn**: form nhập kích thước nang, số lượng, 2 bên buồng trứng. **Medical audit** cho mọi thay đổi KQ | CRITICAL |
| P3.02 | **Phiếu theo dõi nang noãn**: hiển thị timeline các lần SA trong chu kỳ, biểu đồ tăng trưởng nang (ngx-echarts) | CRITICAL |
| P3.03 | **Logic thu phí SA nang noãn 1 lần/chu kỳ**: dùng bảng `cycle_fees` — check `fee_type='FOLLICLE_SCAN_CYCLE'`, nếu đã paid → không tạo invoice SA nang noãn nữa | CRITICAL |
| P3.04 | **Phân luồng sau SA nang noãn**: Bình thường → tiếp tục KTBT; Bất thường → **3 nhánh**: (a) Chuyển IVM (buồng trứng đa nang), (b) TPTB + SA bơm nước/Chụp HSG vào ngày báo phôi, (c) Ngưng điều trị (nang không phát triển) | HIGH |
| P3.05 | **Chỉ định thuốc KTBT liên tục**: BS ghi chỉ định, sử dụng **template toa** (từ P2.09b), NHS nhập toa, in toa | HIGH |
| P3.06 | **XN Nội tiết theo chu kỳ**: E2, P4, LH — liên kết với lần SA. KQ **không trả cho BN**, chuyển cho HC kẹp vào HS | HIGH |
| P3.07 | **Hẹn tái khám tự động**: dựa trên ngày tiêm thuốc (4-5 ngày sau) | MEDIUM |

### Tuần 10: Chỉ định tiêm thuốc rụng trứng

| Task | Chi tiết | Priority |
|------|----------|----------|
| P3.08 | **Logic đánh giá nang noãn đạt/chưa đạt** | CRITICAL |
| P3.09 | **Chỉ định tiêm thuốc rụng trứng**: ghi giờ tiêm chính xác vào `medication_administrations` với `is_trigger_shot=true` | CRITICAL |
| P3.10 | **Tính giờ chọc hút/IUI**: tự động tính 36 giờ sau giờ tiêm → tạo lịch hẹn. ★ **Auto-tạo alert/notification** nhắc BN giờ tiêm trigger + nhắc ngày chọc hút/IUI | CRITICAL |
| P3.11 | **Hướng dẫn quy trình trước chọc hút**: in phiếu hướng dẫn | HIGH |
| P3.12 | **Phân biệt luồng IVF vs IUI**: IUI cần thêm chỉ định thuốc hỗ trợ hoàng thể **trước ngày IUI** (để ngày TT không phải cho nữa — IVFMD.21.13) | HIGH |
| P3.13 | **SA nang noãn cho BN QHTN (CK tự nhiên)**: quy trình nhẹ, tư vấn ngày QH, hẹn thử thai 14 ngày sau, có thể có toa hỗ trợ hoàng thể sau QH | MEDIUM |

**Angular Components & Routes — Phase 3:**
```
features/
├── stimulation/
│   ├── stimulation.routes.ts
│   ├── services/stimulation.service.ts
│   ├── store/stimulation.store.ts
│   └── pages/
│       ├── tracking/                             # Route: /stimulation/tracking/:cycleId
│       │   └── tracking.component.ts             #   Bảng theo dõi KTBT — timeline + biểu đồ nang
│       ├── follicle-scan/                        # Route: /stimulation/follicle-scan
│       │   └── follicle-scan.component.ts        #   Form SA nang noãn (trái/phải, kích thước, số lượng)
│       ├── trigger-decision/                     # Route: /stimulation/trigger-decision
│       │   └── trigger-decision.component.ts     #   Quyết định tiêm rụng trứng + tính giờ 36h
│       └── medication-log/                       # Route: /stimulation/medication-log
│           └── medication-log.component.ts       #   Log thuốc tiêm trong chu kỳ
│
├── ultrasound/  (bổ sung)
│   └── pages/
│       ├── follicle/                             # Route: /ultrasound/follicle
│       │   └── follicle.component.ts             #   SA nang noãn — queue + form nhập
│       └── follicle-chart/                       # Route: /ultrasound/follicle-chart/:cycleId
│           └── follicle-chart.component.ts       #   ECharts biểu đồ nang noãn qua các lần SA
│
└── natural-cycle/
    ├── natural-cycle.routes.ts
    └── pages/
        └── tracking/                             # Route: /natural-cycle/tracking/:cycleId
            └── tracking.component.ts             #   Theo dõi CK tự nhiên (QHTN)
```

---

## 7. PHASE 4 — Thủ thuật: Chọc hút, IUI, IVM (2.5 tuần)

> Covers: IVFMD.30, IVFMD.40, IVM pathway, IVFMD.N1-N3

### Tuần 11: Chọc hút (OPU) + IVM

| Task | Chi tiết | Priority |
|------|----------|----------|
| P4.01 | **Checklist trước chọc hút**: kiểm tra HC, bệnh sử nội/ngoại/sản, XN tiền mê, KQ XN — **block nếu thiếu consent** | CRITICAL |
| P4.01b | ★ **Module Consent**: tạo consent form (OPU, gây mê, điều trị) từ template → BN ký (trên tablet hoặc in ký tay → scan upload) → tracking trạng thái. Dùng bảng `consent_forms` | CRITICAL |
| P4.02 | **Thu phí chọc hút + chuyển phôi cùng lúc**: dùng bảng `cycle_fees` với `fee_type='OPU_AND_TRANSFER'`. Logic: nếu TPTB → hoàn tiền CP, **cấn trừ vào phí trữ phôi** (tự động tính offset) | CRITICAL |
| P4.03 | **Quy trình lấy tinh trùng chồng**: cấp STT lấy mẫu, ghi nhận mẫu | CRITICAL |
| P4.04 | **Ghi nhận thủ thuật chọc hút**: BS ghi số trứng (mature, immature), gây mê, thời gian. **Đồng thời ghi nhận OPU cho IVM** khi `procedure_type='IVM_OPU'` (chọc hút trứng non — khác OPU thường) | CRITICAL |
| P4.05 | **Theo dõi sau chọc hút**: huyết áp trước/sau, nghỉ ngơi, hướng dẫn tái khám | HIGH |
| P4.06 | ★ **BR6 — Toa thuốc chuẩn bị trước 1 ngày**: workflow `pre-prepare prescription` — NHS nhập toa vào PM **trước ngày chọc hút**. Ngày CH chỉ cần lấy toa đã in, không phải chờ BS kê mới | HIGH |
| P4.07 | **Giao nhận mẫu NHS ↔ LABO**: form xác nhận giao/nhận TT + Trứng/Noãn — 2 bên ký xác nhận, timestamp | HIGH |

### Tuần 12: IUI + IVM LABO handoff

| Task | Chi tiết | Priority |
|------|----------|----------|
| P4.08 | **Checklist trước IUI**: HC, bệnh sử, XN, giờ tiêm mũi rụng trứng, ngày kiêng xuất tinh chồng, số lần quan hệ — **block nếu thiếu consent IUI** | CRITICAL |
| P4.09 | **Lấy mẫu TT chồng → Gửi Nam khoa lọc rửa → Nhận lại sau 2 giờ** | CRITICAL |
| P4.10 | **Ghi nhận thủ thuật IUI**: BS thực hiện, giờ thủ thuật. Toa thuốc **không cấp mới** (đã cấp trước vào ngày SA cuối/KTRT) | CRITICAL |
| P4.11 | **Hẹn thử thai**: tự động tạo lịch hẹn + ★ **gửi notification nhắc ngày thử thai** | HIGH |
| P4.12 | **Nhập KQ IUI vào PM**: giờ TT, BS TT | HIGH |
| P4.13 | ★ **IVM pathway — LABO nuôi trứng non**: sau chọc hút trứng non (IVM_OPU), LABO nhận trứng non → nuôi trưởng thành (IVF.00.13) → khi trưởng thành → ICSI → nuôi phôi. Cần thêm step `IVM_MATURATION` trong embryo tracking | HIGH |

**Angular Components & Routes — Phase 4:**
```
features/
├── procedure/
│   ├── procedure.routes.ts
│   ├── services/procedure.service.ts
│   └── pages/
│       ├── opu-checklist/                        # Route: /procedure/opu/checklist/:cycleId
│       │   └── opu-checklist.component.ts        #   Checklist trước chọc hút (HC, XN, bệnh sử)
│       ├── opu-perform/                          # Route: /procedure/opu/perform/:cycleId
│       │   └── opu-perform.component.ts          #   Ghi nhận: gây mê, chọc hút, số trứng
│       ├── opu-post-op/                          # Route: /procedure/opu/post-op/:cycleId
│       │   └── opu-post-op.component.ts          #   Theo dõi HA, nghỉ ngơi, phát toa
│       ├── iui-checklist/                        # Route: /procedure/iui/checklist/:cycleId
│       │   └── iui-checklist.component.ts        #   Checklist IUI (giờ tiêm, ngày kiêng...)
│       ├── iui-perform/                          # Route: /procedure/iui/perform/:cycleId
│       │   └── iui-perform.component.ts          #   Ghi nhận thủ thuật IUI
│       ├── iui-post-op/                          # Route: /procedure/iui/post-op/:cycleId
│       │   └── iui-post-op.component.ts          #   Sau IUI — hẹn thử thai
│       └── sample-handover/                      # Route: /procedure/sample-handover
│           └── sample-handover.component.ts      #   Form giao nhận mẫu NHS ↔ LABO
│
└── andrology/
    ├── andrology.routes.ts
    ├── services/andrology.service.ts
    └── pages/
        ├── semen-analysis/                       # Route: /andrology/semen-analysis
        │   └── semen-analysis.component.ts       #   XN Tinh dịch đồ (IVFMD.N1)
        ├── wash-iui/                             # Route: /andrology/wash-iui
        │   └── wash-iui.component.ts             #   Lọc rửa TT cho IUI (IVFMD.N2)
        └── wash-icsi/                            # Route: /andrology/wash-icsi
            └── wash-icsi.component.ts            #   Lọc rửa TT cho ICSI/IVM (IVFMD.N3)
```

---

## 8. PHASE 5 — Phôi học LABO (2 tuần)

> Covers: IVFMD.80 (Báo phôi + Chuyển phôi + Trữ phôi)

### Tuần 13-14

| Task | Chi tiết | Priority |
|------|----------|----------|
| P5.01 | **Theo dõi phôi realtime**: nhập thông tin phôi mỗi ngày (N0→N6), grade, cell count | CRITICAL |
| P5.02 | **Màn hình báo phôi cho BN**: LABO chuyển HS phôi → NHS ghi → BS/NV báo phôi | CRITICAL |
| P5.03 | **Quyết định: Chuyển phôi tươi / Trữ phôi toàn bộ** | CRITICAL |
| P5.04 | **Ghi nhận chuyển phôi**: số phôi, chất lượng, ngày chuyển (N3/N5/N6), BS thực hiện | CRITICAL |
| P5.05 | **Hợp đồng trữ phôi**: tạo HĐ, tính phí (8tr/top đầu, 2tr/top phát sinh) | CRITICAL |
| P5.06 | **Logic tính top**: Phôi N3 (1-3/top), N5&N6 (1-2/top), Đặc biệt (1/top) | HIGH |
| P5.07 | **Quản lý kho trữ phôi**: tank, vị trí, mã cọng rạ | HIGH |
| P5.08 | **Phiếu trữ phôi**: in cho BN | HIGH |
| P5.09 | **SA niêm mạc trước chuyển phôi tươi**: SA tại phòng báo phôi | MEDIUM |
| P5.10 | **Logic hoàn tiền**: hoàn phí CP nếu TPTB, cấn trừ vào phí trữ | MEDIUM |

**Angular Components & Routes — Phase 5:**
```
features/
└── embryology/
    ├── embryology.routes.ts
    ├── services/embryology.service.ts
    ├── store/embryology.store.ts              # Realtime phôi state
    └── pages/
        ├── culture/                           # Route: /embryology/culture/:cycleId
        │   └── culture.component.ts           #   Bảng theo dõi phôi daily (N0→N6), drag-drop grade
        ├── report/                            # Route: /embryology/report/:cycleId
        │   └── report.component.ts            #   Báo phôi cho BN — hiển thị ảnh phôi + grade
        ├── transfer/                          # Route: /embryology/transfer/:cycleId
        │   └── transfer.component.ts          #   Ghi nhận chuyển phôi (N3/N5/N6, số phôi, BS)
        ├── freeze-contract/                   # Route: /embryology/freeze-contract/:cycleId
        │   └── freeze-contract.component.ts   #   Tạo HĐ trữ phôi + tính phí auto
        ├── storage/                           # Route: /embryology/storage
        │   └── storage.component.ts           #   Quản lý kho trữ (tank, vị trí, mã cọng rạ)
        └── embryo-detail/                     # Route: /embryology/embryo/:id
            └── embryo-detail.component.ts     #   Chi tiết 1 phôi — lịch sử nuôi cấy
```

---

## 8b. PHASE 5b — Chuyển phôi trữ / FET (CBNMTC) (2 tuần) ← MỚI

> Covers: **IVFMD.50** (Khám chuẩn bị NMTC N2 VK), **IVFMD.51** (SA theo dõi NMTC)
> 
> ⚠️ **Đây là quy trình sử dụng rất thường xuyên** — nhiều chu kỳ kết thúc bằng TPTB (trữ phôi toàn bộ), nên FET có thể chiếm 40-60% tổng số ca chuyển phôi.

### Tuần 15: Khám chuẩn bị NMTC lần đầu (IVFMD.50)

| Task | Chi tiết | Priority |
|------|----------|----------|
| P5b.01 | **Tạo chu kỳ FET** (`endometrium_prep_cycles`): liên kết với chu kỳ gốc đã trữ phôi, ghi nhận phôi có sẵn trong kho | CRITICAL |
| P5b.02 | **Flow tiếp nhận N2 VK**: BN đến ngày 2-3 chu kỳ kinh → xin phiếu hẹn/phiếu trữ phôi → thu phí **SA phụ khoa + phí tư vấn** (thu lại vì KQ SA cũ hết ý nghĩa) | CRITICAL |
| P5b.03 | **SA phụ khoa cho FET**: cần **2 BS** (BS Siêu âm + BS Tư vấn) — giống lần đầu. Cấp STT SA trước, **STT tư vấn cấp sau khi SA xong** (khác với lần đầu cấp cùng lúc) | CRITICAL |
| P5b.04 | **BS Tư vấn**: khám, chỉ định CLS bổ sung nếu hết hạn, ghi thuốc CBNMTC (chuẩn bị niêm mạc tử cung) vào HS → NHS nhập toa PM → in toa → hẹn tái khám | CRITICAL |
| P5b.05 | **Logic phí `cycle_fees`**: tạo record `fee_type='ENDOMETRIUM_SCAN_CYCLE'` — phí SA NMTC thu 1 lần suốt chu kỳ chuẩn bị | HIGH |

### Tuần 16: SA theo dõi NMTC + Chuyển phôi trữ (IVFMD.51)

| Task | Chi tiết | Priority |
|------|----------|----------|
| P5b.06 | **SA theo dõi NMTC**: **chỉ cần 1 BS** tư vấn (vừa SA vừa tư vấn — khác lần đầu!). Ghi KQ NM vào bảng `endometrium_scans` (độ dày, pattern, đạt/chưa đạt) | CRITICAL |
| P5b.07 | **Biểu đồ độ dày NMTC**: ngx-echarts line chart hiển thị progression qua các lần SA (giống biểu đồ nang noãn) | HIGH |
| P5b.08 | **Điều chỉnh thuốc CBNMTC**: BS ghi thuốc mới/điều chỉnh → NHS nhập PM → in toa → BS kiểm tra ký tên → hẹn tái khám. Chu kỳ 10-12 ngày uống thuốc → tái khám | HIGH |
| P5b.09 | **NM đạt → Lên lịch chuyển phôi trữ**: khi `is_ready=true`, tạo lịch hẹn chuyển phôi + thông báo LABO chuẩn bị rã đông phôi. Liên kết với phôi cụ thể trong kho trữ | CRITICAL |
| P5b.10 | **Thủ thuật chuyển phôi trữ**: tạo `procedure` với `procedure_type='EMBRYO_TRANSFER_FROZEN'`, ghi nhận phôi nào được rã đông + chuyển, cập nhật trạng thái phôi trong `embryos` table | CRITICAL |
| P5b.11 | **Sau chuyển phôi trữ → Hẹn thử thai**: tự động tạo lịch hẹn thử thai + gửi notification. Flow sau đó vào **Phase 6** (thử thai, theo dõi thai 7 tuần) | HIGH |
| P5b.12 | ★ **Alert hết hạn trữ phôi**: notification tự động khi hợp đồng trữ phôi sắp hết hạn (30/60/90 ngày trước) | MEDIUM |

**Angular Components & Routes — Phase 5b:**
```
features/
└── endometrium-prep/                              # MODULE MỚI — Lazy loaded
    ├── endometrium-prep.routes.ts
    ├── services/endometrium-prep.service.ts
    ├── store/endometrium-prep.store.ts
    └── pages/
        ├── create/                                # Route: /endometrium-prep/create
        │   └── create.component.ts                #   Tạo chu kỳ FET — chọn chu kỳ gốc + phôi
        ├── initial-exam/                          # Route: /endometrium-prep/initial/:cycleId
        │   └── initial-exam.component.ts          #   Khám lần đầu N2 VK — SA PK + tư vấn (2 BS)
        ├── monitoring/                            # Route: /endometrium-prep/monitoring/:cycleId
        │   └── monitoring.component.ts            #   SA theo dõi NM — form + biểu đồ (1 BS)
        ├── medication/                            # Route: /endometrium-prep/medication/:cycleId
        │   └── medication.component.ts            #   Thuốc CBNMTC — template toa + điều chỉnh
        ├── ready-for-transfer/                    # Route: /endometrium-prep/transfer/:cycleId
        │   └── ready-for-transfer.component.ts    #   NM đạt → chọn phôi rã đông → lên lịch CP
        └── transfer-perform/                      # Route: /endometrium-prep/perform/:cycleId
            └── transfer-perform.component.ts      #   Ghi nhận chuyển phôi trữ → hẹn thử thai
```

---

## 10. PHASE 6 — Thai kỳ sớm (1.5 tuần)

> Covers: IVFMD.90 (Thử thai), IVFMD.91 (Theo dõi thai 7 tuần)

### Tuần 15-16 (nửa đầu)

| Task | Chi tiết | Priority |
|------|----------|----------|
| P6.01 | ★ **BR7 — Flow riêng cho thử thai**: BN đến thẳng **Thu Ngân BV** (không qua tiếp đón IVFMD, không lấy STT đầu tiên). Hệ thống cần route riêng `/pregnancy/beta-hcg` mà không yêu cầu STT check-in. Nếu tích hợp HIS BV → tự động nhận phí từ BV; nếu không → ghi nhận manual "BN đã đóng tiền tại BV" | CRITICAL |
| P6.02 | ★ **BR5 — Chỉ BS thông báo KQ BetaHCG**: Workflow XN → chuyển NHS (không trả BN) → NHS nhập PM → **Permission check: chỉ role BS_TU_VAN mới có nút "Thông báo KQ cho BN"**, NHS chỉ thấy KQ nhưng không có quyền gọi BN thông báo. UI hiển thị warning nếu NHS cố truy cập | CRITICAL |
| P6.03 | **Phân luồng kết quả**: Dương tính → toa thuốc 3 tuần + hẹn thai 7 tuần + ★ gửi notification chúc mừng; Âm tính → hẹn N2 VK cho chu kỳ tiếp (CPT lần sau, hoặc QHTN/IUI/ICSI/IVM lần khác) | CRITICAL |
| P6.04 | **Khám thai 7 tuần**: SA thai → In KQ (**2 bản** — 1 cho BN, 1 kẹp HS) → Cấp STT khám thai (sau SA) → BS khám tư vấn → Toa thuốc 4 tuần | HIGH |
| P6.05 | **Hẹn tái khám thai**: **2 tuần** (1 thai) / **1 tuần** (2 thai). Tái khám lần 2: tiếp đón phát **sổ khám thai** | HIGH |
| P6.06 | **Phát sổ khám thai**: tại tái khám lần 2, ghi nhận trong hệ thống | MEDIUM |
| P6.07 | **Chuyển sang QT thai thường quy BV**: sau tái khám lần 2, **đóng chu kỳ IVF** → status = 'PREGNANT_DISCHARGED'. IVF không theo dõi thai nữa, chuyển qua QT thai bình thường của BV | MEDIUM |

**Angular Components & Routes — Phase 6:**
```
features/
└── pregnancy/
    ├── pregnancy.routes.ts
    ├── services/pregnancy.service.ts
    └── pages/
        ├── beta-hcg/                          # Route: /pregnancy/beta-hcg/:cycleId
        │   └── beta-hcg.component.ts          #   Nhập/xem KQ BetaHCG (chỉ BS thông báo)
        ├── result/                            # Route: /pregnancy/result/:cycleId
        │   └── result.component.ts            #   BS thông báo KQ + chọn hướng xử trí
        ├── prenatal-7w/                       # Route: /pregnancy/prenatal-7w/:cycleId
        │   └── prenatal-7w.component.ts       #   SA thai 7 tuần + toa thuốc 4 tuần
        └── discharge/                         # Route: /pregnancy/discharge/:cycleId
            └── discharge.component.ts         #   Kết thúc theo dõi IVF → chuyển sản BV
```

---

## 10. PHASE 7 — Người cho trứng (2 tuần)

> Covers: IVFMD.T1, IVFMD.T2

### Tuần 16 (nửa sau) - 17

| Task | Chi tiết | Priority |
|------|----------|----------|
| P7.01 | **Đăng ký người cho trứng**: kiểm tra đã cho lần nào chưa, tạo ID, chụp ảnh, vân tay | CRITICAL |
| P7.02 | **SA phụ khoa cho NCT**: quy trình tương tự BN nhưng thêm kiểm tra đặc thù | CRITICAL |
| P7.03 | **XN sàng lọc NCT**: thường quy + bổ sung | HIGH |
| P7.04 | **Tư vấn NCT sau KQ XN**: đánh giá đủ tiêu chuẩn, chỉ định thêm SA nhũ | HIGH |
| P7.05 | **Gắn NCT với 2 cặp vợ chồng hiếm muộn trên PM** (IVFMD.T2.24) | CRITICAL |
| P7.06 | **XN tiền mê + ECG + SA nhũ cho NCT** | HIGH |
| P7.07 | **KTBT cho NCT**: quy trình KTBT giống BN IVF | HIGH |

**Angular Components & Routes — Phase 7:**
```
features/
└── egg-donor/
    ├── egg-donor.routes.ts
    ├── services/egg-donor.service.ts
    └── pages/
        ├── register/                          # Route: /egg-donor/register
        │   └── register.component.ts          #   Đăng ký NCT — form + ảnh + vân tay
        ├── list/                              # Route: /egg-donor/list
        │   └── list.component.ts              #   DS NCT + filter trạng thái sàng lọc
        ├── detail/                            # Route: /egg-donor/:id
        │   └── detail.component.ts            #   Chi tiết NCT (tabs: info | XN | SA nhũ | KTBT)
        ├── screening/                         # Route: /egg-donor/:id/screening
        │   └── screening.component.ts         #   Sàng lọc XN + kết quả + đủ tiêu chuẩn
        ├── match/                             # Route: /egg-donor/:id/match
        │   └── match.component.ts             #   Gắn NCT với 2 cặp vợ chồng trên PM
        └── stimulation/                       # Route: /egg-donor/:id/stimulation
            └── stimulation.component.ts       #   Theo dõi KTBT cho NCT (reuse stimulation components)
```

---

## 11. PHASE 8 — Ngân hàng tinh trùng NHTT (2 tuần)

> Covers: IVFMD.N4, IVFMD.N5, IVFMD.N6, IVFMD.N7

### Tuần 18-19

| Task | Chi tiết | Priority |
|------|----------|----------|
| **GĐ1 — Sàng lọc (IVFMD.N4)** | | |
| P8.01 | BS tư vấn xin TT, viết giấy giới thiệu NHTT | CRITICAL |
| P8.02 | XN sàng lọc: HIV, HBsAg, BW, Gs+RH, Anti HCV, CBC, TDĐ | CRITICAL |
| P8.03 | Kiểm tra người hiến: ảnh camera + vân tay (so khớp sinh trắc) | CRITICAL |
| P8.04 | Báo KQ HIV nhanh (15 phút), KQ còn lại (2 giờ) | HIGH |
| P8.05 | Quyết định đủ/không đủ tiêu chuẩn NHTT. Nếu không đủ: tư vấn tìm NH khác, trả KQ TDĐ + XN máu cho NVĐ/NH | CRITICAL |
| **GĐ2 — Lấy mẫu (IVFMD.N5, N6)** | | |
| P8.06 | Lấy mẫu lần 1: xác minh sinh trắc → ★ **Tư vấn QT NHTT + ký cam kết tham gia** (consent_forms `form_type='SPERM_BANK_PARTICIPATION'`) → lấy mẫu → LABO lọc rửa + đánh giá → trữ lạnh | CRITICAL |
| P8.07 | Lấy mẫu lần 2: tương tự lần 1, sau đó hẹn XN HIV lần 2 sau 3 tháng | CRITICAL |
| P8.08 | XN HIV lần 2: xác minh sinh trắc → lấy máu tại IVFMD → XN | CRITICAL |
| P8.09 | HIV âm tính → cấp mã NHTT, cấp phiếu NHTT cho NVĐ; HIV dương tính → hủy mẫu | CRITICAL |
| **GĐ3 — Sử dụng (IVFMD.N7)** | | |
| P8.10 | BN xuất trình phiếu NHTT → thu tiền sử dụng → chuyển LABO | CRITICAL |
| P8.11 | LABO hoán đổi mẫu theo mã NHTT → lấy mẫu → ghi nhận sử dụng | CRITICAL |
| P8.12 | Theo dõi số mẫu còn lại, điều kiện hủy mẫu | HIGH |
| P8.13 | Logic hủy mẫu: (a) vợ có thai + bé 1 tuổi bình thường; (b) 1 năm kể từ lần dùng cuối | HIGH |

**Angular Components & Routes — Phase 8:**
```
features/
└── sperm-bank/
    ├── sperm-bank.routes.ts
    ├── services/sperm-bank.service.ts
    ├── store/sperm-bank.store.ts
    └── pages/
        ├── screening/                         # Route: /sperm-bank/screening/:donorId
        │   └── screening.component.ts         #   GĐ1: XN sàng lọc + quyết định đủ chuẩn
        ├── biometric-verify/                  # Route: /sperm-bank/biometric-verify/:donorId
        │   └── biometric-verify.component.ts  #   So khớp ảnh + vân tay (tích hợp thiết bị)
        ├── sample-1/                          # Route: /sperm-bank/sample-1/:donorId
        │   └── sample-1.component.ts          #   GĐ2: Lấy mẫu lần 1 → LABO đánh giá → trữ
        ├── sample-2/                          # Route: /sperm-bank/sample-2/:donorId
        │   └── sample-2.component.ts          #   GĐ2: Lấy mẫu lần 2 → hẹn HIV lần 2
        ├── hiv-retest/                        # Route: /sperm-bank/hiv-retest/:donorId
        │   └── hiv-retest.component.ts        #   XN HIV lần 2 (sau 3 tháng)
        ├── approve/                           # Route: /sperm-bank/approve/:donorId
        │   └── approve.component.ts           #   Cấp mã NHTT + phiếu NHTT
        ├── inventory/                         # Route: /sperm-bank/inventory
        │   └── inventory.component.ts         #   Kho mẫu NHTT (tank, straw, trạng thái)
        ├── usage/                             # Route: /sperm-bank/usage/:sampleId
        │   └── usage.component.ts             #   GĐ3: Sử dụng mẫu (hoán đổi, ghi nhận)
        └── destroy/                           # Route: /sperm-bank/destroy/:sampleId
            └── destroy.component.ts           #   Hủy mẫu + ghi lý do
```

---

## 12. PHASE 9 — Quản lý Thuốc & Vật tư (1.5 tuần)

> Covers: IVFMD.D1, IVFMD.D2, IVFMD.D3

### Tuần 20

| Task | Chi tiết | Priority |
|------|----------|----------|
| **Thuốc bù tủ trực (D1)** | | |
| P9.01 | KTV gây mê ghi sổ bàn giao thuốc | HIGH |
| P9.02 | NHS kiểm tra tồn kho 2 lần/ngày | HIGH |
| P9.03 | Tạo phiếu bù tủ trực → Khoa Dược duyệt → In phiếu, trình ký → Lĩnh thuốc | HIGH |
| **VTTH không xuất kho (D2)** | | |
| P9.04 | NHS kiểm kho mỗi 3-5 ngày | MEDIUM |
| P9.05 | Lập phiếu hao phí → Khoa Dược duyệt → In → Lĩnh VT | MEDIUM |
| **VTTH có xuất kho (D3)** | | |
| P9.06 | NHS đặt hàng qua email/PM → BP Mua hàng nhận → Mua từ NCC → Phát VT | MEDIUM |
| P9.07 | NHS nhập kho vào PM → Sử dụng → Xuất kho khi hết lô | MEDIUM |

**Angular Components & Routes — Phase 9:**
```
features/
└── inventory/
    ├── inventory.routes.ts
    ├── services/inventory.service.ts
    └── pages/
        ├── duty-restock/                      # Route: /inventory/duty-restock
        │   └── duty-restock.component.ts      #   Phiếu bù tủ trực — tạo + duyệt workflow
        ├── consumable-usage/                  # Route: /inventory/consumable-usage
        │   └── consumable-usage.component.ts  #   Phiếu hao phí VTTH (không xuất kho)
        ├── purchase-order/                    # Route: /inventory/purchase-order
        │   └── purchase-order.component.ts    #   Đặt mua VTTH (có xuất kho)
        ├── stock/                             # Route: /inventory/stock
        │   └── stock.component.ts             #   Tồn kho hiện tại + cảnh báo min_stock
        └── approval/                          # Route: /inventory/approval
            └── approval.component.ts          #   Khoa Dược duyệt phiếu (bù tủ + hao phí)
```

---

## 13. PHASE 10 — Tài chính & Báo cáo (2 tuần)

### Tuần 21: Module Billing hoàn chỉnh

| Task | Chi tiết | Priority |
|------|----------|----------|
| P10.01 | **Tạo hóa đơn tổng hợp**: gộp nhiều dịch vụ (SA + TV + XN + Thuốc) | CRITICAL |
| P10.02 | **Thu tiền**: tiền mặt, cà thẻ, chuyển khoản. ★ **BR8 — Chuyển khoản cần kế toán approve**: kế toán kiểm tra tài khoản → email/notification thông báo Thu Ngân IVF → Thu Ngân xác nhận đã nhận → cập nhật invoice status | CRITICAL |
| P10.03 | **In hóa đơn**: 2 liên (hồng cho BN, trắng lưu) | CRITICAL |
| P10.04 | **Hoàn tiền / Cấn trừ**: hoàn tiền CP khi TPTB → cấn trừ vào trữ phôi | HIGH |
| P10.05 | **Bảng giá dịch vụ**: cấu hình giá SA, TV, XN, thủ thuật, trữ phôi... Bao gồm rule: phí SA nang noãn suốt CK, phí SA NMTC suốt CK, phí CH+CP bundle | HIGH |
| P10.06 | **Thu phí đặc biệt**: phí tiêm thuốc (thu tại thu ngân BV), phí NHTT, phí sử dụng mẫu. **Phân biệt rõ**: phí IVFMD vs phí BV (XN BetaHCG, ECG, tiêm thuốc) | HIGH |
| P10.06b | ★ **Tích hợp HIS BV Mỹ Đức** (nếu có API): đồng bộ phí tiêm thuốc, phí ECG, XN BetaHCG, XN tại BP XN BV. Nếu không có API → màn hình ghi nhận manual "BN đã thanh toán tại BV" | MEDIUM |

### Tuần 22-23: Báo cáo & Dashboard

| Task | Chi tiết | Priority |
|------|----------|----------|
| P10.07 | **Dashboard tổng quan**: số BN, chu kỳ đang điều trị, tỷ lệ thành công | HIGH |
| P10.08 | **Báo cáo tài chính**: doanh thu theo ngày/tháng, theo dịch vụ, theo BS | HIGH |
| P10.09 | **Báo cáo y khoa**: tỷ lệ thai lâm sàng, tỷ lệ chọc hút thành công, số phôi trung bình, **tỷ lệ FET thành công** (mới) | HIGH |
| P10.10 | **Báo cáo NHTT**: số mẫu, số lần sử dụng, mẫu hết hạn, mẫu cần hủy | MEDIUM |
| P10.11 | **Báo cáo kho**: tồn kho, xuất nhập, cảnh báo hết hàng | MEDIUM |
| P10.12 | **Export**: PDF (jsPDF), Excel (SheetJS). **Báo cáo consent**: danh sách consent chưa ký, sắp hết hạn | MEDIUM |

**Angular Components & Routes — Phase 10:**
```
features/
├── billing/
│   ├── billing.routes.ts
│   ├── services/billing.service.ts
│   └── pages/
│       ├── create/                            # Route: /billing/create
│       │   └── create.component.ts            #   Tạo hóa đơn tổng hợp (nhiều dịch vụ)
│       ├── payment/                           # Route: /billing/payment/:invoiceId
│       │   └── payment.component.ts           #   Thu tiền: mặt / cà thẻ / chuyển khoản
│       ├── refund/                            # Route: /billing/refund/:invoiceId
│       │   └── refund.component.ts            #   Hoàn tiền / cấn trừ
│       ├── history/                           # Route: /billing/history
│       │   └── history.component.ts           #   Lịch sử hóa đơn + filter
│       └── price-list/                        # Route: /billing/price-list
│           └── price-list.component.ts        #   Bảng giá dịch vụ (CRUD)
│
└── reports/
    ├── reports.routes.ts
    ├── services/report.service.ts
    └── pages/
        ├── dashboard/                         # Route: /reports/dashboard
        │   └── dashboard.component.ts         #   Tổng quan: ngx-echarts charts + KPI cards
        ├── financial/                         # Route: /reports/financial
        │   └── financial.component.ts         #   BC tài chính: doanh thu, theo BS, theo DV
        ├── clinical/                          # Route: /reports/clinical
        │   └── clinical.component.ts          #   BC y khoa: tỷ lệ thai, chọc hút, phôi TB
        ├── sperm-bank-report/                 # Route: /reports/sperm-bank
        │   └── sperm-bank-report.component.ts #   BC NHTT: mẫu, sử dụng, hết hạn
        └── inventory-report/                  # Route: /reports/inventory
            └── inventory-report.component.ts  #   BC kho: tồn, xuất nhập, cảnh báo
```

---

## 14. API ENDPOINTS TỔNG HỢP

### Auth & Users
```
POST   /api/auth/login
POST   /api/auth/refresh
GET    /api/users
POST   /api/users
PUT    /api/users/:id
```

### Patients & Couples
```
GET    /api/patients?search=&type=
POST   /api/patients
GET    /api/patients/:id
PUT    /api/patients/:id
POST   /api/patients/:id/photo          # Upload ảnh
POST   /api/patients/:id/fingerprint    # Lấy vân tay

GET    /api/couples
POST   /api/couples
GET    /api/couples/:id
```

### Queue & Appointments
```
POST   /api/queue/ticket                 # Cấp STT
GET    /api/queue/current?dept=          # STT hiện tại theo phòng
PUT    /api/queue/:id/call               # Gọi STT
PUT    /api/queue/:id/complete           # Hoàn thành

GET    /api/appointments?date=&doctor=
POST   /api/appointments
PUT    /api/appointments/:id
```

### Treatment Cycles
```
GET    /api/cycles?coupleId=&status=
POST   /api/cycles
GET    /api/cycles/:id
PUT    /api/cycles/:id
GET    /api/cycles/:id/timeline          # Timeline toàn bộ chu kỳ
```

### Ultrasound
```
POST   /api/ultrasound                   # Tạo KQ SA
GET    /api/ultrasound/:id
GET    /api/ultrasound/cycle/:cycleId    # Tất cả SA trong chu kỳ
PUT    /api/ultrasound/:id
```

### Lab
```
POST   /api/lab/orders                   # Tạo chỉ định XN
GET    /api/lab/orders?status=
POST   /api/lab/orders/:id/results       # Nhập KQ
PUT    /api/lab/orders/:id/deliver       # Trả KQ cho NHS
GET    /api/lab/results/patient/:patientId
```

### Prescriptions & Pharmacy
```
POST   /api/prescriptions
GET    /api/prescriptions/:id
PUT    /api/prescriptions/:id/enter      # NHS nhập PM
PUT    /api/prescriptions/:id/print      # In toa

GET    /api/pharmacy/pending              # Toa chờ phát
PUT    /api/pharmacy/:prescriptionId/dispense
```

### Stimulation
```
GET    /api/stimulation/cycle/:cycleId   # Toàn bộ theo dõi KTBT
POST   /api/stimulation/follicle-scan    # Nhập SA nang noãn
POST   /api/stimulation/trigger          # Chỉ định tiêm rụng trứng
POST   /api/stimulation/medication-log   # Ghi nhận tiêm thuốc
```

### Procedures
```
POST   /api/procedures                   # Tạo thủ thuật
GET    /api/procedures/:id
PUT    /api/procedures/:id/checklist     # Checklist trước TT
PUT    /api/procedures/:id/perform       # Ghi nhận thực hiện
PUT    /api/procedures/:id/post-op       # Ghi nhận sau TT
POST   /api/procedures/sample-handover   # Giao nhận mẫu
```

### Embryology
```
GET    /api/embryos/cycle/:cycleId       # Tất cả phôi trong chu kỳ
POST   /api/embryos                      # Tạo phôi mới
PUT    /api/embryos/:id                  # Cập nhật hàng ngày
PUT    /api/embryos/:id/transfer         # Ghi nhận chuyển phôi
PUT    /api/embryos/:id/freeze           # Ghi nhận trữ phôi

POST   /api/embryo-contracts             # Tạo HĐ trữ phôi
GET    /api/embryo-contracts/:id
GET    /api/embryo-storage               # Kho trữ phôi
```

### Andrology
```
POST   /api/andrology/semen-analysis     # XN TDĐ
POST   /api/andrology/sperm-wash         # Lọc rửa TT
GET    /api/andrology/results/:id
```

### Sperm Bank
```
POST   /api/sperm-bank/donors            # Đăng ký người hiến
PUT    /api/sperm-bank/donors/:id/screen # Sàng lọc
POST   /api/sperm-bank/donors/:id/biometric-verify  # Xác minh sinh trắc
POST   /api/sperm-bank/samples           # Lấy mẫu
PUT    /api/sperm-bank/samples/:id/freeze
PUT    /api/sperm-bank/samples/:id/approve  # Cấp mã NHTT
POST   /api/sperm-bank/usage             # Sử dụng mẫu
PUT    /api/sperm-bank/samples/:id/destroy
```

### Egg Donors
```
POST   /api/egg-donors
GET    /api/egg-donors/:id
PUT    /api/egg-donors/:id/match         # Gắn NCT với cặp VC
GET    /api/egg-donors/:id/screening
```

### Pregnancy
```
POST   /api/pregnancy/beta-hcg           # Nhập KQ BetaHCG
GET    /api/pregnancy/cycle/:cycleId
POST   /api/pregnancy/prenatal           # Khám thai
PUT    /api/pregnancy/cycle/:cycleId/discharge  # Kết thúc theo dõi
```

### Billing
```
POST   /api/invoices
GET    /api/invoices?patientId=&date=
PUT    /api/invoices/:id/pay
PUT    /api/invoices/:id/refund
GET    /api/price-list
```

### Inventory
```
GET    /api/inventory/stock
POST   /api/inventory/restock-request     # Phiếu bù tủ trực
POST   /api/inventory/usage-report        # Phiếu hao phí
PUT    /api/inventory/requests/:id/approve
```

### Reports
```
GET    /api/reports/dashboard
GET    /api/reports/financial?from=&to=
GET    /api/reports/clinical?from=&to=
GET    /api/reports/sperm-bank
GET    /api/reports/inventory
GET    /api/reports/consent-status          # ★ Consent chưa ký / sắp hết hạn
```

### ★ Endometrium Prep / FET (MỚI)
```
POST   /api/endometrium-prep               # Tạo chu kỳ FET
GET    /api/endometrium-prep/:id
GET    /api/endometrium-prep?coupleId=&status=
PUT    /api/endometrium-prep/:id
POST   /api/endometrium-prep/:id/scan      # Nhập KQ SA NMTC
GET    /api/endometrium-prep/:id/scans     # Tất cả SA NMTC trong chu kỳ
PUT    /api/endometrium-prep/:id/ready     # NM đạt → lên lịch CP
POST   /api/endometrium-prep/:id/transfer  # Ghi nhận chuyển phôi trữ
```

### ★ Consent Forms (MỚI)
```
POST   /api/consents                       # Tạo consent form
GET    /api/consents?patientId=&status=
GET    /api/consents/:id
PUT    /api/consents/:id/sign              # Ký consent (upload scan)
PUT    /api/consents/:id/revoke            # Thu hồi consent
GET    /api/consents/pending/:cycleId      # Consent chưa ký cho 1 chu kỳ
```

### ★ Medication Administrations (MỚI)
```
POST   /api/medication-admin               # Ghi nhận tiêm/uống thuốc
GET    /api/medication-admin/cycle/:cycleId # Tất cả thuốc đã tiêm trong CK
GET    /api/medication-admin/trigger/:cycleId # Lấy giờ tiêm trigger → tính 36h
```

### ★ Notifications (MỚI)
```
GET    /api/notifications?patientId=&status=
POST   /api/notifications/schedule         # Lên lịch gửi notification
PUT    /api/notifications/:id/cancel       # Hủy notification
POST   /api/notifications/send-now         # Gửi ngay (manual)
GET    /api/notifications/upcoming         # Notification sắp gửi (24h tới)
```

### ★ Prescription Templates (MỚI)
```
GET    /api/prescription-templates
POST   /api/prescription-templates
PUT    /api/prescription-templates/:id
DELETE /api/prescription-templates/:id
POST   /api/prescriptions/from-template/:templateId  # Tạo toa từ template
```

### ★ Cycle Fees (MỚI)
```
GET    /api/cycle-fees/:cycleId            # Phí đã thu cho chu kỳ
POST   /api/cycle-fees/check               # Check phí đã thu chưa (trước khi tạo invoice)
PUT    /api/cycle-fees/:id/refund          # Hoàn phí (vd: hoàn phí CP khi TPTB)
```

### ★ File Tracking (MỚI — giai đoạn chuyển đổi)
```
POST   /api/file-tracking/transfer         # Ghi nhận chuyển HS giữa phòng
GET    /api/file-tracking/:patientId       # Xem HS đang ở đâu
GET    /api/file-tracking/department/:dept  # Tất cả HS đang ở 1 phòng
```

### ★ Medical Audit (MỚI)
```
GET    /api/medical-audit?entityType=&patientId=&from=&to=
GET    /api/medical-audit/entity/:entityType/:entityId   # Lịch sử thay đổi 1 record
```

---

## 16. CHECKLIST THEO DÕI TIẾN ĐỘ

### Phase 1 — Nền tảng (Tuần 1-4.5)
- [ ] P1.01 Angular project setup (standalone, Tailwind, Angular Material, AG Grid)
- [ ] P1.01b Backend setup (NestJS / .NET), Docker, CI/CD
- [ ] P1.02 Database migration — **tất cả bảng** (bao gồm bảng mới: endometrium_prep, consent, medication_admin, cycle_fees, notifications, prescription_templates, medical_audit)
- [ ] P1.03 Auth: `AuthService`, `authGuard`, `authInterceptor`, login page
- [ ] P1.04 RBAC: `roleGuard`, `HasRoleDirective`, sidebar menu theo role
- [ ] P1.05 User CRUD (Reactive Forms)
- [ ] P1.06 Core: `ErrorInterceptor`, `LoadingInterceptor`, `NotificationService`, `ApiService`
- [ ] P1.07 Shared: `DataTableComponent`, `ConfirmDialogComponent`, `PatientSearchComponent`, pipes
- [ ] P1.07b Layout: `MainLayoutComponent` (sidebar + header + breadcrumb), `AuthLayoutComponent`
- [ ] P1.07c ★ Medical Audit Log middleware (backend)
- [ ] P1.07d ★ Barcode/QR module (generate + scan)
- [ ] P1.08 Feature patients: `PatientStore`, list, create, detail (tabs)
- [ ] P1.09 Feature patients/couple: `CoupleDetailComponent`
- [ ] P1.10 Feature queue: WebSocket + STT multi-liên + QR code
- [ ] P1.11 Feature queue: display (fullscreen TV) + call (BS/NHS)
- [ ] P1.12 Feature appointments: FullCalendar + logic 2 BS vs 1 BS
- [ ] P1.13 Upload ảnh (camera) + vân tay (WebUSB/SDK)
- [ ] P1.14 Auto-generate số hồ sơ lưu trữ
- [ ] P1.15 In phiếu khám HM (`PrintService` + `ngx-print`)
- [ ] P1.16 ★ SMS/Zalo nhắc lịch (NotificationService backend + message queue)
- [ ] P1.17 ★ Dashboard theo role (KPI widgets khác nhau cho mỗi role)
- [ ] P1.18 ★ File tracking hồ sơ giấy (giai đoạn chuyển đổi)

### Phase 2 — Khám & Tư vấn (Tuần 5-7)
- [ ] P2.01 SA phụ khoa + medical audit
- [ ] P2.02 Chỉ định XN trên PM (phân biệt XN tại IVFMD vs XN tại BV)
- [ ] P2.03 Module XN + medical audit
- [ ] P2.04 Phân loại XN (thường quy / nội tiết / tiền mê / BetaHCG)
- [ ] P2.05 Workflow KQ XN (không trả BN cho một số loại)
- [ ] P2.06 Khám tư vấn lần đầu
- [ ] P2.07 Tư vấn sau KQ XN — chọn hướng điều trị
- [ ] P2.08 Tạo chu kỳ điều trị
- [ ] P2.09 Module toa thuốc
- [ ] P2.09b ★ Template toa thuốc (CRUD template + tạo toa từ template)
- [ ] P2.10 Logic phân luồng (N2 VK / thực hiện ngay / không thực hiện)
- [ ] P2.10b ★ BR4 — Miễn phí tư vấn (flag `waive_consultation_fee`)
- [ ] P2.11 XN tiền mê + ECG + hẹn khám tiền mê
- [ ] P2.12 Module nhà thuốc (PM IVFMD vs PM BV?)
- [ ] P2.13 Ghi nhận tiêm thuốc (`medication_administrations` — tiêm tại IVFMD vs BV)
- [ ] P2.14 Workflow hoàn chỉnh lần đầu (end-to-end test)

### Phase 3 — KTBT & Theo dõi (Tuần 8-10)
- [ ] P3.01 SA nang noãn + medical audit
- [ ] P3.02 Phiếu theo dõi nang noãn (timeline + biểu đồ ngx-echarts)
- [ ] P3.03 Logic thu phí 1 lần/chu kỳ (`cycle_fees` table)
- [ ] P3.04 Phân luồng bất thường (IVM / TPTB+HSG / Ngưng)
- [ ] P3.05 Chỉ định thuốc KTBT (dùng template toa)
- [ ] P3.06 XN nội tiết theo chu kỳ (KQ không trả BN)
- [ ] P3.07 Hẹn tái khám tự động (4-5 ngày sau tiêm)
- [ ] P3.08 Logic đánh giá nang noãn đạt/chưa đạt
- [ ] P3.09 Chỉ định tiêm rụng trứng (`is_trigger_shot=true`)
- [ ] P3.10 Tính giờ chọc hút/IUI (36h) + ★ auto alert/notification
- [ ] P3.11 In phiếu hướng dẫn trước chọc hút
- [ ] P3.12 Phân biệt IVF vs IUI (thuốc hoàng thể trước cho IUI)
- [ ] P3.13 SA nang noãn CK tự nhiên (QHTN)

### Phase 4 — Thủ thuật (Tuần 11-12.5)
- [ ] P4.01 Checklist trước chọc hút (block nếu thiếu consent)
- [ ] P4.01b ★ Module Consent (tạo, ký, upload scan, tracking)
- [ ] P4.02 Thu phí CH + CP cùng lúc (`cycle_fees` + logic hoàn/cấn trừ)
- [ ] P4.03 Lấy TT chồng (STT lấy mẫu)
- [ ] P4.04 Ghi nhận chọc hút + IVM_OPU (chọc hút trứng non)
- [ ] P4.05 Theo dõi sau chọc hút (HA trước/sau)
- [ ] P4.06 ★ BR6 — Toa thuốc chuẩn bị trước 1 ngày
- [ ] P4.07 Giao nhận mẫu NHS↔LABO (xác nhận 2 bên + timestamp)
- [ ] P4.08 Checklist trước IUI (block nếu thiếu consent IUI)
- [ ] P4.09 Lấy mẫu + lọc rửa IUI (2 giờ)
- [ ] P4.10 Ghi nhận IUI (không cấp toa mới)
- [ ] P4.11 Hẹn thử thai + ★ gửi notification
- [ ] P4.12 Nhập KQ IUI vào PM
- [ ] P4.13 ★ IVM pathway LABO (nuôi trưởng thành trứng non → ICSI)

### Phase 5 — Phôi học (Tuần 13-14)
- [ ] P5.01 Theo dõi phôi realtime (WebSocket)
- [ ] P5.02 Màn hình báo phôi
- [ ] P5.03 Quyết định CP tươi/TPTB
- [ ] P5.04 Ghi nhận chuyển phôi (tươi)
- [ ] P5.05 Hợp đồng trữ phôi (consent form)
- [ ] P5.06 Logic tính top (N3: 1-3, N5&N6: 1-2, đặc biệt: 1)
- [ ] P5.07 Quản lý kho trữ phôi (tank, vị trí, cọng rạ)
- [ ] P5.08 Phiếu trữ phôi (in)
- [ ] P5.09 SA niêm mạc trước CP tươi
- [ ] P5.10 Logic hoàn tiền CP → cấn trừ vào trữ phôi

### ★ Phase 5b — FET / CBNMTC (Tuần 15-16) ← MỚI
- [ ] P5b.01 Tạo chu kỳ FET (liên kết chu kỳ gốc + phôi trong kho)
- [ ] P5b.02 Flow tiếp nhận N2 VK (thu phí SA PK + tư vấn)
- [ ] P5b.03 SA phụ khoa cho FET (2 BS, STT tư vấn cấp sau SA)
- [ ] P5b.04 BS tư vấn + thuốc CBNMTC + hẹn tái khám
- [ ] P5b.05 Logic phí `cycle_fees` SA NMTC 1 lần/chu kỳ
- [ ] P5b.06 SA theo dõi NMTC (1 BS, form + bảng endometrium_scans)
- [ ] P5b.07 Biểu đồ độ dày NMTC (ngx-echarts)
- [ ] P5b.08 Điều chỉnh thuốc CBNMTC (10-12 ngày → tái khám)
- [ ] P5b.09 NM đạt → lên lịch chuyển phôi trữ + thông báo LABO rã đông
- [ ] P5b.10 Ghi nhận chuyển phôi trữ (EMBRYO_TRANSFER_FROZEN)
- [ ] P5b.11 Hẹn thử thai + notification
- [ ] P5b.12 ★ Alert hết hạn trữ phôi (30/60/90 ngày trước)

### Phase 6 — Thai kỳ (Tuần 17-18)
- [ ] P6.01 ★ BR7 — Flow riêng thử thai (skip tiếp đón IVFMD)
- [ ] P6.02 ★ BR5 — Chỉ BS thông báo KQ BetaHCG (permission check)
- [ ] P6.03 Phân luồng Dương/Âm tính + notification
- [ ] P6.04 Khám thai 7 tuần (SA thai 2 bản + toa 4 tuần)
- [ ] P6.05 Hẹn tái khám thai (2w/1 thai, 1w/2 thai)
- [ ] P6.06 Phát sổ khám thai (tái khám lần 2)
- [ ] P6.07 Đóng chu kỳ IVF → chuyển QT thai BV

### Phase 7 — Cho trứng (Tuần 18-19)
- [ ] P7.01 Đăng ký NCT (ảnh + vân tay + check đã cho chưa)
- [ ] P7.02 SA phụ khoa NCT
- [ ] P7.03 XN sàng lọc NCT
- [ ] P7.04 Tư vấn NCT + consent cho trứng
- [ ] P7.05 Gắn NCT với 2 cặp VC trên PM
- [ ] P7.06 XN tiền mê + ECG + SA nhũ
- [ ] P7.07 KTBT cho NCT

### Phase 8 — NHTT (Tuần 20-21)
- [ ] P8.01 Tư vấn xin TT + giấy giới thiệu NHTT
- [ ] P8.02 XN sàng lọc NH (HIV, HBsAg, BW, Gs+RH, Anti HCV, CBC)
- [ ] P8.03 Sinh trắc NH (ảnh + vân tay + so khớp)
- [ ] P8.04 Báo KQ HIV nhanh (15 phút)
- [ ] P8.05 Quyết định đủ chuẩn / tư vấn tìm NH khác
- [ ] P8.06 Lấy mẫu lần 1 + ★ consent cam kết tham gia NHTT
- [ ] P8.07 Lấy mẫu lần 2 + hẹn HIV lần 2 sau 3 tháng
- [ ] P8.08 XN HIV lần 2 + xác minh sinh trắc lại
- [ ] P8.09 Cấp mã NHTT + phiếu NHTT
- [ ] P8.10 Thu tiền sử dụng mẫu
- [ ] P8.11 Hoán đổi & lấy mẫu NHTT (LABO)
- [ ] P8.12 Theo dõi mẫu còn lại + ★ alert 3 tháng HIV retest
- [ ] P8.13 Logic hủy mẫu (bé 1 tuổi bình thường / 1 năm không dùng)

### Phase 9 — Kho & VT (Tuần 22)
- [ ] P9.01 Sổ bàn giao thuốc (KTV gây mê)
- [ ] P9.02 Kiểm tồn kho 2 lần/ngày
- [ ] P9.03 Phiếu bù tủ trực → Khoa Dược duyệt → in → lĩnh
- [ ] P9.04 Kiểm kho VTTH 3-5 ngày
- [ ] P9.05 Phiếu hao phí → Khoa Dược duyệt → in → lĩnh
- [ ] P9.06 Đặt mua VTTH (email/PM → NCC)
- [ ] P9.07 Nhập/xuất kho PM

### Phase 10 — Tài chính & BC (Tuần 23-27)
- [ ] P10.01 Hóa đơn tổng hợp (nhiều dịch vụ)
- [ ] P10.02 Thu tiền đa hình thức + ★ BR8 chuyển khoản cần kế toán approve
- [ ] P10.03 In hóa đơn 2 liên (hồng cho BN, trắng lưu) + QR code
- [ ] P10.04 Hoàn tiền / cấn trừ (logic offset CP→trữ phôi)
- [ ] P10.05 Bảng giá dịch vụ (bao gồm rule phí suốt CK)
- [ ] P10.06 Thu phí đặc biệt (phân biệt phí IVFMD vs phí BV)
- [ ] P10.06b ★ Tích hợp HIS BV Mỹ Đức (nếu có API)
- [ ] P10.07 Dashboard tổng quan (ngx-echarts + KPI cards)
- [ ] P10.08 BC tài chính (doanh thu theo ngày/tháng/BS/DV)
- [ ] P10.09 BC y khoa (tỷ lệ thai, chọc hút, phôi, ★ FET)
- [ ] P10.10 BC NHTT (mẫu, sử dụng, hết hạn, cần hủy)
- [ ] P10.11 BC kho (tồn, xuất nhập, cảnh báo hết)
- [ ] P10.12 Export PDF/Excel + ★ BC consent chưa ký/sắp hết hạn

---

## PHỤ LỤC A: CÁC ĐỀ XUẤT CẢI TIẾN (từ tài liệu gốc + gap analysis)

### Cải tiến từ tài liệu gốc (tích hợp ngay từ đầu):

| # | Cải tiến | Phase áp dụng | Trạng thái |
|---|----------|---------------|------------|
| 1 | BS chỉ định trên PM thay cho HS giấy | Phase 2 (P2.02) | ✅ Đã tích hợp |
| 2 | BS ra toa thuốc trên PM thay cho HS giấy | Phase 2 (P2.09) | ✅ Đã tích hợp |
| 3 | BS ghi nhận KQ SA nang noãn trên PM | Phase 3 (P3.01) | ✅ Đã tích hợp |
| 4 | BS ghi nhận KQ SA NMTC trên PM | Phase 5b (P5b.06) | ✅ Đã tích hợp |
| 5 | LABO theo dõi phôi realtime trên PM | Phase 5 (P5.01) | ✅ Đã tích hợp |

### Cải tiến mới từ gap analysis (★):

| # | Cải tiến | Phase | Mức |
|---|----------|-------|-----|
| C1 | SMS/Zalo nhắc lịch hẹn | Phase 1 (P1.16) | HIGH |
| C2 | Dashboard theo role | Phase 1 (P1.17) | HIGH |
| C3 | Barcode/QR trên STT, hóa đơn, toa thuốc | Phase 1 (P1.07d) | HIGH |
| C4 | Alert mốc thời gian (trigger 36h, hết hạn trữ, HIV retest 3 tháng) | Phase 3/5b/8 | HIGH |
| C5 | Tích hợp HIS BV Mỹ Đức | Phase 10 (P10.06b) | MEDIUM |
| C6 | Template toa thuốc | Phase 2 (P2.09b) | HIGH |
| C7 | Module Consent forms | Phase 4 (P4.01b) | HIGH |
| C8 | Medical Audit Log (pháp lý y khoa) | Phase 1 (P1.07c) | HIGH |
| C9 | File tracking hồ sơ giấy | Phase 1 (P1.18) | MEDIUM |
| C10 | Patient portal (app BN) | Sau go-live | MEDIUM |
| C11 | Ký điện tử consent | Sau go-live | MEDIUM |
| C12 | LABO image capture (ảnh phôi) | Sau go-live | MEDIUM |

### Business Rules bổ sung:

| # | Rule | Phase | Trạng thái |
|---|------|-------|------------|
| BR4 | NHS quyết định miễn phí tư vấn dựa ghi chú BS | Phase 2 (P2.10b) | ✅ Đã tích hợp |
| BR5 | Chỉ BS được thông báo KQ BetaHCG cho BN | Phase 6 (P6.02) | ✅ Đã tích hợp |
| BR6 | Toa thuốc sau chọc hút chuẩn bị trước 1 ngày | Phase 4 (P4.06) | ✅ Đã tích hợp |
| BR7 | Thử thai: BN đến thẳng Thu Ngân BV, skip tiếp đón IVFMD | Phase 6 (P6.01) | ✅ Đã tích hợp |
| BR8 | Chuyển khoản cần kế toán kiểm tra + email Thu Ngân | Phase 10 (P10.02) | ✅ Đã tích hợp |

---

*Tài liệu này được tạo dựa trên phân tích đầy đủ "Tài liệu Quy trình IVFMD v1.0" + Gap Analysis v1.*
*Tổng cộng: 11 phase (bao gồm Phase 5b FET), ~130 task, ~70 standalone Angular components, ~110 API endpoints.*
*Bao gồm: 8 database tables mới, 5 business rules bổ sung, 12 cải tiến từ gap analysis.*

---

## PHỤ LỤC B: ANGULAR — CẤU HÌNH & RECIPES

### B.1 Khởi tạo dự án Angular

```bash
# 1. Tạo Angular project (standalone, SCSS, routing)
ng new ivfmd-frontend --standalone --style=scss --routing --skip-tests=false
cd ivfmd-frontend

# 2. Tailwind CSS
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init

# 3. UI Library (chọn 1)
ng add @angular/material                    # Angular Material
# hoặc
npm install primeng primeicons              # PrimeNG

# 4. State Management
npm install @ngrx/signals                   # NgRx Signal Store (lightweight)
# hoặc
npm install @ngrx/store @ngrx/effects @ngrx/entity  # NgRx full

# 5. AG Grid (data table)
npm install ag-grid-community ag-grid-angular

# 6. Charts
npm install echarts ngx-echarts             # ECharts
# hoặc
npm install chart.js ng2-charts             # Chart.js

# 7. Calendar
npm install @fullcalendar/core @fullcalendar/angular \
            @fullcalendar/daygrid @fullcalendar/timegrid @fullcalendar/interaction

# 8. Print & PDF
npm install ngx-print jspdf html2canvas

# 9. JWT
npm install @auth0/angular-jwt

# 10. i18n
npm install @ngx-translate/core @ngx-translate/http-loader

# 11. Utilities
npm install date-fns lodash-es             # date & utility
npm install -D @types/lodash-es

# 12. WebSocket (cho STT realtime, phôi tracking)
npm install socket.io-client
npm install -D @types/socket.io-client
```

### B.2 tailwind.config.js

```javascript
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      colors: {
        primary: {
          50:  '#eff6ff',
          100: '#dbeafe',
          200: '#bfdbfe',
          300: '#93c5fd',
          400: '#60a5fa',
          500: '#3b82f6',    // Main brand color
          600: '#2563eb',
          700: '#1d4ed8',
          800: '#1e40af',
          900: '#1e3a8a',
        },
        ivf: {
          teal:    '#0d9488',  // Stimulation / KTBT
          rose:    '#e11d48',  // Procedure / Thủ thuật
          amber:   '#d97706',  // Lab / Xét nghiệm  
          violet:  '#7c3aed',  // Embryology / Phôi
          emerald: '#059669',  // Pregnancy / Thai
          sky:     '#0284c7',  // Queue / STT
        }
      },
      fontFamily: {
        sans: ['Be Vietnam Pro', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
    require('@tailwindcss/typography'),
  ],
};
```

### B.3 app.config.ts (Angular 17+ Standalone Bootstrap)

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { APP_ROUTES } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(
      APP_ROUTES,
      withComponentInputBinding(),     // auto-bind route params to @Input()
      withViewTransitions(),           // smooth page transitions
    ),
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        errorInterceptor,
        loadingInterceptor,
      ])
    ),
    provideAnimationsAsync(),
  ],
};
```

### B.4 Environment Configuration

```typescript
// src/environments/environment.ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:3000/api',
  wsUrl: 'ws://localhost:3000',
  
  // Feature flags
  features: {
    biometricVerification: true,   // Vân tay + camera
    realtimeQueue: true,           // WebSocket cho STT
    realtimeEmbryo: true,          // WebSocket cho LABO
  },
  
  // Tính phí
  pricing: {
    freezingBasePerStraw: 8_000_000,        // 8 triệu/top đầu
    freezingAdditionalPerStraw: 2_000_000,  // 2 triệu/top phát sinh
  },
};
```

### B.5 Angular Service Pattern (với Error Handling)

```typescript
// core/services/api.service.ts
@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private notification = inject(NotificationService);

  get<T>(url: string, params?: HttpParams): Observable<T> {
    return this.http.get<T>(url, { params }).pipe(
      catchError(this.handleError.bind(this))
    );
  }

  post<T>(url: string, body: unknown): Observable<T> {
    return this.http.post<T>(url, body).pipe(
      catchError(this.handleError.bind(this))
    );
  }

  put<T>(url: string, body: unknown): Observable<T> {
    return this.http.put<T>(url, body).pipe(
      catchError(this.handleError.bind(this))
    );
  }

  delete<T>(url: string): Observable<T> {
    return this.http.delete<T>(url).pipe(
      catchError(this.handleError.bind(this))
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let message = 'Đã xảy ra lỗi. Vui lòng thử lại.';
    if (error.error?.message) {
      message = error.error.message;
    } else if (error.status === 0) {
      message = 'Không thể kết nối đến server.';
    } else if (error.status === 403) {
      message = 'Bạn không có quyền thực hiện thao tác này.';
    } else if (error.status === 404) {
      message = 'Không tìm thấy dữ liệu.';
    }
    this.notification.error(message);
    return throwError(() => error);
  }
}
```

### B.6 WebSocket Service (Realtime STT + Phôi)

```typescript
// core/services/websocket.service.ts
@Injectable({ providedIn: 'root' })
export class WebSocketService {
  private socket: Socket | null = null;

  connect(): void {
    if (this.socket?.connected) return;
    this.socket = io(environment.wsUrl, {
      auth: { token: inject(TokenService).getAccessToken() },
    });
  }

  // Lắng nghe STT mới được gọi
  onQueueCalled(): Observable<QueueTicket> {
    return new Observable(observer => {
      this.socket?.on('queue:called', (data: QueueTicket) => observer.next(data));
      return () => this.socket?.off('queue:called');
    });
  }

  // Lắng nghe cập nhật phôi realtime
  onEmbryoUpdated(): Observable<Embryo> {
    return new Observable(observer => {
      this.socket?.on('embryo:updated', (data: Embryo) => observer.next(data));
      return () => this.socket?.off('embryo:updated');
    });
  }

  disconnect(): void {
    this.socket?.disconnect();
    this.socket = null;
  }
}
```

### B.7 Print Service

```typescript
// core/services/print.service.ts
@Injectable({ providedIn: 'root' })
export class PrintService {
  
  /** In nội dung HTML (STT, hóa đơn, KQ XN, toa thuốc) */
  printHtml(content: string, title: string = 'IVFMD'): void {
    const printWindow = window.open('', '_blank');
    if (!printWindow) return;
    
    printWindow.document.write(`
      <!DOCTYPE html>
      <html>
      <head>
        <title>${title}</title>
        <link rel="stylesheet" href="/assets/print-templates/print.css">
      </head>
      <body onload="window.print(); window.close();">
        ${content}
      </body>
      </html>
    `);
    printWindow.document.close();
  }

  /** Export sang PDF */
  async exportPdf(elementId: string, filename: string): Promise<void> {
    const element = document.getElementById(elementId);
    if (!element) return;
    
    const canvas = await html2canvas(element);
    const pdf = new jsPDF('p', 'mm', 'a4');
    const imgData = canvas.toDataURL('image/png');
    const imgWidth = 210; // A4 width mm
    const imgHeight = (canvas.height * imgWidth) / canvas.width;
    
    pdf.addImage(imgData, 'PNG', 0, 0, imgWidth, imgHeight);
    pdf.save(`${filename}.pdf`);
  }
}
```

### B.8 Tổng hợp Angular Packages

| Package | Mục đích | Dùng ở đâu |
|---------|----------|-------------|
| `@angular/material` hoặc `primeng` | UI components (dialog, snackbar, datepicker, menu) | Toàn bộ |
| `tailwindcss` + `@tailwindcss/forms` | Styling, layout, responsive | Toàn bộ |
| `@ngrx/signals` | State management (Signal Store) | patients, queue, embryology, sperm-bank |
| `ag-grid-angular` | Data table nâng cao (sort, filter, pagination) | Danh sách BN, hóa đơn, XN, phôi, kho |
| `ngx-echarts` | Charts (bar, line, pie, gauge) | Dashboard, reports, follicle chart |
| `@fullcalendar/angular` | Lịch hẹn kéo thả | appointments/calendar |
| `socket.io-client` | Realtime WebSocket | queue/display, embryology/culture |
| `ngx-print` | In ấn trực tiếp | STT, hóa đơn, KQ XN, toa thuốc, phiếu |
| `jspdf` + `html2canvas` | Export PDF | Reports, hồ sơ BN |
| `@auth0/angular-jwt` | JWT decode, auto-attach | Auth |
| `@ngx-translate/core` | Đa ngôn ngữ (vi/en) | Toàn bộ |
| `date-fns` | Format ngày tiếng Việt, tính 36h trigger | Toàn bộ |
