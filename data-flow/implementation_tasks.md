# IVF System - Implementation Tasks

## Sprint 1: Project Foundation (Week 1-2)

### Task 1.1: Solution Setup
**Priority:** P0 | **Estimate:** 4h | **Assignee:** Backend Lead

**Steps:**
1. Create solution structure:
```bash
mkdir -p src tests docs
dotnet new sln -n IVF
dotnet new classlib -n IVF.Domain -o src/IVF.Domain
dotnet new classlib -n IVF.Application -o src/IVF.Application
dotnet new classlib -n IVF.Infrastructure -o src/IVF.Infrastructure
dotnet new webapi -n IVF.API -o src/IVF.API
dotnet new xunit -n IVF.Tests -o tests/IVF.Tests
```

2. Add project references:
```
IVF.API → IVF.Application, IVF.Infrastructure
IVF.Application → IVF.Domain
IVF.Infrastructure → IVF.Domain, IVF.Application
```

3. Install core packages:
```bash
# IVF.Infrastructure
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design

# IVF.API
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Swashbuckle.AspNetCore
```

**Acceptance Criteria:**
- [ ] Solution compiles without errors
- [ ] All projects have correct references
- [ ] Swagger UI accessible at /swagger

---

### Task 1.2: PostgreSQL Database Setup
**Priority:** P0 | **Estimate:** 8h | **Assignee:** Backend

**Steps:**
1. Create `IVF.Infrastructure/Persistence/IvfDbContext.cs`
2. Configure connection string in `appsettings.json`
3. Create initial migration with core tables
4. Seed lookup data (departments, queue types, etc.)

**Files to Create:**
```
src/IVF.Infrastructure/
├── Persistence/
│   ├── IvfDbContext.cs
│   ├── Configurations/
│   │   ├── PatientConfiguration.cs
│   │   ├── CoupleConfiguration.cs
│   │   └── ...
│   └── Migrations/
└── DependencyInjection.cs
```

**Entity Configurations:**
```csharp
// PatientConfiguration.cs
public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PatientCode)
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(p => p.PatientCode).IsUnique();
        builder.Property(p => p.FullName)
            .HasMaxLength(200)
            .IsRequired();
    }
}
```

**Acceptance Criteria:**
- [ ] Database created successfully
- [ ] All 17 tables exist with correct schema
- [ ] Foreign keys properly configured
- [ ] Seed data populated

---

### Task 1.3: Angular Project Setup
**Priority:** P0 | **Estimate:** 8h | **Assignee:** Frontend Lead

**Steps:**
1. Create Angular 17 project:
```bash
cd src
npx -y @angular/cli@17 new IVF.Web --routing --style=scss --ssr=false
cd IVF.Web
npm install primeng primeicons @primeng/themes
npm install @angular/cdk
```

2. Configure PrimeNG:
```typescript
// app.config.ts
import { provideAnimations } from '@angular/platform-browser/animations';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeng/themes/aura';

export const appConfig: ApplicationConfig = {
  providers: [
    provideAnimations(),
    providePrimeNG({ theme: { preset: Aura } })
  ]
};
```

3. Create module structure:
```
src/app/
├── core/
│   ├── services/
│   ├── guards/
│   └── interceptors/
├── shared/
│   ├── components/
│   └── pipes/
├── modules/
│   ├── auth/
│   ├── reception/
│   ├── consultation/
│   ├── ultrasound/
│   ├── lab/
│   ├── andrology/
│   ├── sperm-bank/
│   ├── billing/
│   └── admin/
└── app.routes.ts
```

**Acceptance Criteria:**
- [ ] Angular app runs at localhost:4200
- [ ] PrimeNG components working
- [ ] Routing configured
- [ ] API proxy configured

---

### Task 1.4: JWT Authentication
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/Entities/User.cs
src/IVF.Application/Features/Auth/
├── Commands/
│   ├── LoginCommand.cs
│   └── RefreshTokenCommand.cs
├── DTOs/
│   └── AuthResponse.cs
└── Services/
    └── ITokenService.cs
src/IVF.Infrastructure/Services/TokenService.cs
src/IVF.API/Controllers/AuthController.cs
```

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/auth/login | User login |
| POST | /api/auth/refresh | Refresh token |
| POST | /api/auth/logout | Logout |

**JWT Configuration:**
```json
{
  "JwtSettings": {
    "Secret": "your-256-bit-secret-key",
    "Issuer": "IVF-System",
    "Audience": "IVF-Users",
    "ExpiryMinutes": 60
  }
}
```

**Acceptance Criteria:**
- [ ] Login returns JWT token
- [ ] Protected endpoints require valid token
- [ ] Refresh token works
- [ ] Roles extracted from claims

---

### Task 1.5: Login UI
**Priority:** P0 | **Estimate:** 8h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/auth/
├── login/
│   ├── login.component.ts
│   ├── login.component.html
│   └── login.component.scss
├── services/
│   └── auth.service.ts
└── auth.routes.ts
```

**UI Components:**
- Username input
- Password input
- Remember me checkbox
- Login button
- Error message display

**Acceptance Criteria:**
- [ ] Login form validates inputs
- [ ] Successful login redirects to dashboard
- [ ] Token stored in localStorage
- [ ] Auth guard protects routes

---

## Sprint 2: Patient & Queue (Week 3-4)

### Task 2.1: Patient Domain Entities
**Priority:** P0 | **Estimate:** 8h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/
├── Entities/
│   ├── Patient.cs
│   ├── Couple.cs
│   └── MedicalRecord.cs
├── Enums/
│   ├── Gender.cs
│   └── PatientType.cs
└── Events/
    └── PatientCreatedEvent.cs
```

**Patient Entity:**
```csharp
public class Patient : BaseEntity
{
    public string PatientCode { get; private set; }
    public string FullName { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string IdentityNumber { get; private set; }
    public string Phone { get; private set; }
    public string Address { get; private set; }
    public byte[]? Photo { get; private set; }
    public byte[]? Fingerprint { get; private set; }
    public PatientType Type { get; private set; }
    
    // Navigation
    public virtual ICollection<Couple> AsWife { get; private set; }
    public virtual ICollection<Couple> AsHusband { get; private set; }
    public virtual ICollection<QueueTicket> QueueTickets { get; private set; }
}
```

---

### Task 2.2: Patient CRUD API
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Backend

**CQRS Structure:**
```
src/IVF.Application/Features/Patients/
├── Commands/
│   ├── CreatePatient/
│   │   ├── CreatePatientCommand.cs
│   │   ├── CreatePatientCommandHandler.cs
│   │   └── CreatePatientCommandValidator.cs
│   ├── UpdatePatient/
│   └── DeletePatient/
├── Queries/
│   ├── GetPatientById/
│   ├── SearchPatients/
│   └── GetPatientHistory/
└── DTOs/
    ├── PatientDto.cs
    └── PatientListDto.cs
```

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/patients | Create patient |
| GET | /api/patients/{id} | Get by ID |
| PUT | /api/patients/{id} | Update |
| DELETE | /api/patients/{id} | Soft delete |
| GET | /api/patients/search | Search |
| GET | /api/patients/{id}/history | Treatment history |

**Search Parameters:**
- `q` - Search term (name, code, phone)
- `type` - Patient type filter
- `page`, `pageSize` - Pagination

---

### Task 2.3: Patient Registration UI
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/reception/
├── patient-registration/
│   ├── patient-registration.component.ts
│   ├── patient-registration.component.html
│   └── patient-registration.component.scss
├── patient-search/
│   ├── patient-search.component.ts
│   └── ...
└── services/
    └── patient.service.ts
```

**Form Fields:**
| Field | Type | Validation |
|-------|------|------------|
| FullName | Text | Required, max 200 |
| DateOfBirth | Date | Required, past date |
| Gender | Dropdown | Required |
| IdentityNumber | Text | Required, 12 chars |
| Phone | Text | Required, pattern |
| Address | Textarea | Required |
| PatientType | Dropdown | Required |
| Photo | File upload | Optional |

**Features:**
- Photo capture from webcam
- Duplicate check on ID number
- Auto-generate patient code
- Print registration form

---

### Task 2.4: Queue Ticket API
**Priority:** P0 | **Estimate:** 12h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/Entities/QueueTicket.cs
src/IVF.Domain/Enums/QueueType.cs
src/IVF.Domain/Enums/TicketStatus.cs
src/IVF.Application/Features/Queue/
├── Commands/
│   ├── IssueTicket/
│   ├── CallTicket/
│   └── CompleteTicket/
├── Queries/
│   ├── GetDepartmentQueue/
│   └── GetTicketStatus/
└── Services/
    └── ITicketNumberGenerator.cs
```

**Ticket Number Format:**
- SA-001, SA-002 (Siêu âm)
- TV-001, TV-002 (Tư vấn)
- XN-001, XN-002 (Xét nghiệm)

**Reset daily at midnight**

---

### Task 2.5: SignalR Queue Hub
**Priority:** P1 | **Estimate:** 8h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.API/Hubs/QueueHub.cs
src/IVF.Infrastructure/Services/QueueNotificationService.cs
```

**Hub Methods:**
```csharp
public class QueueHub : Hub
{
    public async Task JoinDepartment(string departmentCode)
    public async Task LeaveDepartment(string departmentCode)
}

// Server broadcasts
await Clients.Group(dept).SendAsync("TicketCalled", ticket);
await Clients.Group(dept).SendAsync("QueueUpdated", queue);
```

---

### Task 2.6: Queue Display UI
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/reception/
├── queue-display/
│   ├── queue-display.component.ts
│   └── ...
├── queue-caller/
│   └── ...
└── services/
    └── queue.service.ts
```

**Display Features:**
- Large ticket numbers
- "Now Serving" highlight
- Waiting list
- Audio announcement
- Department filter

**Caller Features:**
- Next patient button
- Skip button
- Complete button
- Current patient info

---

## Sprint 3: Consultation (Week 5-6)

### Task 3.1: Couple Management
**Priority:** P0 | **Estimate:** 12h | **Assignee:** Backend

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/couples | Link couple |
| GET | /api/couples/{id} | Get couple |
| PUT | /api/couples/{id} | Update |
| GET | /api/couples/{id}/cycles | Get all cycles |

---

### Task 3.2: Treatment Cycle API
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Backend

**Cycle Operations:**
- Create new cycle
- Update current phase
- Record events (ultrasound, procedure, etc.)
- Close cycle with outcome

**State Transitions:**
```
Consultation → OvarianStimulation → TriggerShot → 
EggRetrieval → EmbryoCulture → EmbryoTransfer → 
LutealSupport → PregnancyTest → [Pregnant/NotPregnant]
```

---

### Task 3.3: Consultation Screen
**Priority:** P0 | **Estimate:** 24h | **Assignee:** Frontend

**Sections:**
1. Patient Info Header
2. Medical History Tab
3. Current Cycle Tab
4. Ultrasound Results Tab
5. Lab Results Tab
6. Prescription Panel
7. Appointment Scheduler

---

### Task 3.4: Prescription API
**Priority:** P1 | **Estimate:** 12h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/prescriptions | Create |
| GET | /api/prescriptions/{id} | Get details |
| PUT | /api/prescriptions/{id}/dispense | Mark dispensed |

---

### Task 3.5: Prescription UI
**Priority:** P1 | **Estimate:** 16h | **Assignee:** Frontend

**Features:**
- Drug search autocomplete
- Dosage templates
- Duration calculator
- Print prescription
- Pharmacy queue integration

---

## Sprint 4: Ultrasound (Week 7-8)

### Task 4.1: Ultrasound Recording API
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/cycles/{id}/ultrasounds | Record US |
| GET | /api/cycles/{id}/ultrasounds | Get history |
| PUT | /api/ultrasounds/{id} | Update |

**Data Structure:**
```json
{
  "examDate": "2026-02-03T10:30:00Z",
  "type": "NangNoan",
  "leftOvaryCount": 8,
  "rightOvaryCount": 6,
  "leftFollicles": [
    {"size": 18, "position": 1},
    {"size": 16, "position": 2}
  ],
  "rightFollicles": [
    {"size": 17, "position": 1}
  ],
  "endometriumThickness": 9.5,
  "findings": "Nang noãn phát triển tốt"
}
```

---

### Task 4.2: Follicle Monitoring Form
**Priority:** P0 | **Estimate:** 24h | **Assignee:** Frontend

**UI Components:**
- Ovary diagrams (left/right)
- Follicle size inputs (grid)
- Auto-count totals
- Endometrium thickness
- Previous visit comparison
- Growth chart

---

## Sprint 5: Lab (LABO) Module (Week 9-10)

### Task 5.1: Embryo Domain Entities
**Priority:** P0 | **Estimate:** 8h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/Entities/
├── Embryo.cs
├── CryoLocation.cs
├── EmbryoTransfer.cs
└── EmbryoFreeze.cs
src/IVF.Domain/Enums/
├── EmbryoGrade.cs
├── EmbryoDay.cs
└── EmbryoStatus.cs
```

**Embryo Entity:**
```csharp
public class Embryo : BaseEntity
{
    public Guid CycleId { get; private set; }
    public int EmbryoNumber { get; private set; }
    public DateTime FertilizationDate { get; private set; }
    public EmbryoGrade Grade { get; private set; }
    public EmbryoDay Day { get; private set; }
    public EmbryoStatus Status { get; private set; }
    public Guid? CryoLocationId { get; private set; }
    public DateTime? FreezeDate { get; private set; }
    public DateTime? ThawDate { get; private set; }
    public string Notes { get; private set; }
    
    // Methods
    public void Transfer() => Status = EmbryoStatus.Transferred;
    public void Freeze(Guid locationId) { ... }
    public void Thaw() { ... }
    public void Discard(string reason) { ... }
}
```

**Acceptance Criteria:**
- [ ] Embryo state transitions validated
- [ ] Cryo location tracking works
- [ ] Audit trail for all status changes

---

### Task 5.2: Embryo Tracking API
**Priority:** P0 | **Estimate:** 20h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Application/Features/Lab/
├── Commands/
│   ├── RecordEmbryos/
│   ├── UpdateEmbryoGrade/
│   ├── FreezeEmbryo/
│   ├── ThawEmbryo/
│   └── DiscardEmbryo/
├── Queries/
│   ├── GetCycleEmbryos/
│   ├── GetEmbryoById/
│   └── GetCryoInventory/
└── DTOs/
    ├── EmbryoDto.cs
    └── EmbryoReportDto.cs
```

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/cycles/{id}/embryos | Record embryos from retrieval |
| PUT | /api/embryos/{id}/grade | Update grade |
| POST | /api/embryos/{id}/freeze | Freeze embryo |
| POST | /api/embryos/{id}/thaw | Thaw for transfer |
| DELETE | /api/embryos/{id} | Discard with reason |
| GET | /api/lab/cryo-inventory | Get all frozen specimens |

**Embryo Report Structure:**
```json
{
  "cycleId": "uuid",
  "retrievalDate": "2026-02-05",
  "eggsRetrieved": 12,
  "matureEggs": 10,
  "fertilized": 8,
  "embryos": [
    {"number": 1, "day": "D5", "grade": "AA", "status": "Transferred"},
    {"number": 2, "day": "D5", "grade": "AB", "status": "Frozen", "location": "Tank1-C2-S3"}
  ]
}
```

---

### Task 5.3: Embryo Dashboard UI
**Priority:** P0 | **Estimate:** 24h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/lab/
├── embryo-board/
│   ├── embryo-board.component.ts
│   ├── embryo-board.component.html
│   └── embryo-board.component.scss
├── embryo-card/
├── cryo-map/
└── services/
    └── lab.service.ts
```

**UI Features:**
- Kanban board: D1 → D2 → D3 → D5 → D6
- Drag-drop embryo cards
- Grade selection dropdown
- Freeze/Transfer/Discard actions
- Daily embryo report generation
- Print embryo report

**Acceptance Criteria:**
- [ ] Real-time updates across lab stations
- [ ] Embryo grade history visible
- [ ] Printable daily report

---

### Task 5.4: Cryo Storage Management
**Priority:** P1 | **Estimate:** 16h | **Assignee:** Backend

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/cryo/tanks | List all tanks |
| GET | /api/cryo/tanks/{id}/map | Get tank layout |
| POST | /api/cryo/locations | Add location |
| PUT | /api/cryo/locations/{id}/assign | Assign specimen |

**Tank Location Hierarchy:**
```
Tank → Canister → Cane → Goblet → Straw
   1      1-6      1-4     1-5     1-4
```

---

### Task 5.5: Cryo Map UI
**Priority:** P1 | **Estimate:** 16h | **Assignee:** Frontend

**Features:**
- Visual tank diagram
- Click to view location details
- Occupied/Empty color coding
- Search specimen by patient
- Expiration alerts (embryos > 5 years)

---

## Sprint 6: Andrology (Week 11-12)

### Task 6.1: Semen Analysis Domain
**Priority:** P0 | **Estimate:** 8h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/Entities/SemenAnalysis.cs
src/IVF.Domain/Entities/SpermWashing.cs
```

**Semen Analysis Entity:**
```csharp
public class SemenAnalysis : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public DateTime AnalysisDate { get; private set; }
    public decimal Volume { get; private set; }           // ml
    public decimal Concentration { get; private set; }    // million/ml
    public decimal MotilityA { get; private set; }        // % rapid progressive
    public decimal MotilityB { get; private set; }        // % slow progressive
    public decimal MotilityC { get; private set; }        // % non-progressive
    public decimal MotilityD { get; private set; }        // % immotile
    public decimal Morphology { get; private set; }       // % normal forms
    public AnalysisType Type { get; private set; }        // Pre/Post wash
    public string Notes { get; private set; }
    
    public decimal TotalMotility => MotilityA + MotilityB + MotilityC;
    public decimal ProgressiveMotility => MotilityA + MotilityB;
}
```

---

### Task 6.2: Semen Analysis API
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/semen-analysis | Record analysis |
| GET | /api/semen-analysis/{id} | Get details |
| GET | /api/patients/{id}/semen-analyses | Patient history |
| POST | /api/semen-analysis/{id}/wash | Record post-wash |

**WHO Reference Values (2021):**
| Parameter | Lower Reference |
|-----------|-----------------|
| Volume | 1.4 ml |
| Concentration | 16 million/ml |
| Total motility | 42% |
| Progressive | 30% |
| Morphology | 4% |

---

### Task 6.3: Semen Analysis Form UI
**Priority:** P0 | **Estimate:** 20h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/andrology/
├── semen-analysis/
│   ├── semen-analysis-form.component.ts
│   └── ...
├── sperm-wash/
└── services/
    └── andrology.service.ts
```

**Form Features:**
- Auto-calculate totals
- WHO reference comparison
- Pre/Post wash toggle
- Visual motility chart
- Print result

---

### Task 6.4: Sperm Washing Module
**Priority:** P1 | **Estimate:** 12h | **Assignee:** Full-stack

**Workflow:**
1. Receive sample from NHS
2. Record pre-wash analysis
3. Perform washing (gradient/swim-up)
4. Record post-wash analysis
5. Send to LABO (ICSI) or NHS (IUI)

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/sperm-wash | Start wash process |
| PUT | /api/sperm-wash/{id}/complete | Record result |

---

## Sprint 7: Sperm Bank - NHTT (Week 13-14)

### Task 7.1: Sperm Donor Domain
**Priority:** P0 | **Estimate:** 12h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/Entities/
├── SpermDonor.cs
└── SpermSample.cs
src/IVF.Domain/Enums/
└── DonorStatus.cs
```

**SpermDonor Entity:**
```csharp
public class SpermDonor : BaseEntity
{
    public Guid PatientId { get; private set; }
    public string DonorCode { get; private set; }
    public DonorStatus Status { get; private set; }
    public DateTime ScreeningDate { get; private set; }
    public DateTime? HivRetestDate { get; private set; }
    public string HivRetestResult { get; private set; }
    public int MaxCouples { get; private set; } = 2;
    public int CurrentCouples { get; private set; }
    
    public virtual ICollection<SpermSample> Samples { get; private set; }
    public virtual ICollection<Couple> LinkedCouples { get; private set; }
    
    public bool CanAcceptNewCouple => CurrentCouples < MaxCouples;
}
```

---

### Task 7.2: Sperm Bank API
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/sperm-bank/donors | Register donor |
| GET | /api/sperm-bank/donors/{id} | Get donor details |
| PUT | /api/sperm-bank/donors/{id}/status | Update status |
| POST | /api/sperm-bank/donors/{id}/samples | Add sample |
| GET | /api/sperm-bank/inventory | Available samples |
| POST | /api/sperm-bank/samples/{id}/use | Use sample |
| POST | /api/sperm-bank/match | Match donor to couple |

**Donor Screening Workflow:**
```
Registration → TDĐ Check → Blood Tests (HIV, HBV, HCV, etc.) 
→ If pass: Sample 1 → Sample 2 → HIV Retest (3 months) 
→ If HIV negative: Active
```

---

### Task 7.3: Donor Registration UI
**Priority:** P0 | **Estimate:** 16h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/sperm-bank/
├── donor-registration/
├── donor-screening/
├── sample-collection/
└── inventory/
```

**Features:**
- Photo + fingerprint capture
- Screening checklist
- Lab result entry
- Sample tracking
- HIV retest scheduling

---

### Task 7.4: Donor-Couple Matching
**Priority:** P1 | **Estimate:** 12h | **Assignee:** Backend

**Business Rules:**
- Max 2 couples per donor
- Anonymous matching only
- Track usage history
- Auto-discard after:
  - Successful pregnancy + 1 year
  - 1 year since last use

**API:**
```json
POST /api/sperm-bank/match
{
  "donorId": "uuid",
  "coupleId": "uuid"
}
```

---

### Task 7.5: HIV Retest Tracking
**Priority:** P1 | **Estimate:** 8h | **Assignee:** Full-stack

**Features:**
- Dashboard showing donors due for retest
- Auto-schedule 3 months after sample 2
- Result recording
- Status update (Active/Rejected)

---

## Sprint 8: Billing & Pharmacy (Week 15-16)

### Task 8.1: Invoice Domain
**Priority:** P0 | **Estimate:** 12h | **Assignee:** Backend

**Files to Create:**
```
src/IVF.Domain/Entities/
├── Invoice.cs
├── InvoiceItem.cs
├── Payment.cs
└── Refund.cs
src/IVF.Domain/Enums/
├── InvoiceStatus.cs
└── PaymentMethod.cs
```

**Invoice Entity:**
```csharp
public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? CycleId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public InvoiceStatus Status { get; private set; }
    
    public virtual ICollection<InvoiceItem> Items { get; private set; }
    public virtual ICollection<Payment> Payments { get; private set; }
    
    public decimal Balance => TotalAmount - PaidAmount - RefundedAmount;
}
```

---

### Task 8.2: Invoice API
**Priority:** P0 | **Estimate:** 20h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/invoices | Create invoice |
| GET | /api/invoices/{id} | Get details |
| POST | /api/invoices/{id}/items | Add item |
| DELETE | /api/invoices/{id}/items/{itemId} | Remove item |
| POST | /api/invoices/{id}/pay | Record payment |
| POST | /api/invoices/{id}/refund | Process refund |
| GET | /api/invoices/daily-report | Daily summary |

**Payment Recording:**
```json
POST /api/invoices/{id}/pay
{
  "amount": 5000000,
  "method": "Cash",  // Cash, Card, Transfer
  "reference": "TX123456"
}
```

---

### Task 8.3: Billing UI
**Priority:** P0 | **Estimate:** 24h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/billing/
├── invoice-create/
├── invoice-detail/
├── payment-form/
├── daily-report/
└── services/
    └── billing.service.ts
```

**Features:**
- Service selector with prices
- Auto-calculate totals
- Multiple payment methods
- Split payment support
- Print receipt (thermal/A4)
- Daily closing report

---

### Task 8.4: Service Price Management
**Priority:** P1 | **Estimate:** 12h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/services | List all services |
| POST | /api/services | Add service |
| PUT | /api/services/{id} | Update price |

**Seed Data:**
```sql
INSERT INTO services (code, name_vn, price) VALUES
('TV-001', 'Phí tư vấn', 300000),
('SA-PK', 'Siêu âm phụ khoa', 200000),
('SA-NN', 'Siêu âm nang noãn (cả chu kỳ)', 1500000),
('XN-AMH', 'Xét nghiệm AMH', 800000),
('TT-CH', 'Chọc hút + Chuyển phôi', 25000000),
('TT-IUI', 'Thủ thuật IUI', 5000000),
('TR-TOP1', 'Trữ phôi (top đầu)', 8000000),
('TR-TOP+', 'Trữ phôi (top thêm)', 2000000);
```

---

### Task 8.5: Pharmacy Module
**Priority:** P1 | **Estimate:** 16h | **Assignee:** Full-stack

**API Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/pharmacy/queue | Prescriptions pending |
| PUT | /api/prescriptions/{id}/dispense | Mark dispensed |

**UI Features:**
- Prescription queue
- Drug lookup
- Dispense confirmation
- Print medication labels

---

## Sprint 9: Reports & Polish (Week 17-18)

### Task 9.1: Reports API
**Priority:** P0 | **Estimate:** 24h | **Assignee:** Backend

**Endpoints:**
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/reports/cycles | Cycle statistics |
| GET | /api/reports/success-rates | IVF/IUI success rates |
| GET | /api/reports/revenue | Financial summary |
| GET | /api/reports/workload | Staff workload |
| GET | /api/reports/cryo-inventory | Frozen specimens |

**Success Rate Calculation:**
```csharp
public class SuccessRateReport
{
    public int TotalCycles { get; set; }
    public int TransferCycles { get; set; }
    public int PregnancyCycles { get; set; }
    public int LiveBirthCycles { get; set; }
    
    public decimal ClinicalPregnancyRate => 
        TransferCycles > 0 ? (decimal)PregnancyCycles / TransferCycles * 100 : 0;
    public decimal LiveBirthRate => 
        TransferCycles > 0 ? (decimal)LiveBirthCycles / TransferCycles * 100 : 0;
}
```

---

### Task 9.2: Analytics Dashboard UI
**Priority:** P0 | **Estimate:** 24h | **Assignee:** Frontend

**Files to Create:**
```
src/app/modules/admin/
├── dashboard/
├── reports/
│   ├── cycle-report/
│   ├── success-report/
│   ├── revenue-report/
│   └── workload-report/
└── services/
    └── report.service.ts
```

**Dashboard Widgets:**
- Today's appointments count
- Cycles in progress
- Pending lab tasks
- Revenue this month
- Success rate trend chart

---

### Task 9.3: Performance Optimization
**Priority:** P1 | **Estimate:** 16h | **Assignee:** Backend

**Tasks:**
- Add database indexes
- Implement caching (Redis)
- Optimize N+1 queries
- Add response compression
- Configure connection pooling

**Key Indexes:**
```sql
CREATE INDEX idx_patients_code ON patients(patient_code);
CREATE INDEX idx_cycles_couple ON treatment_cycles(couple_id);
CREATE INDEX idx_queue_dept_date ON queue_tickets(department_code, issued_at);
CREATE INDEX idx_embryos_cycle ON embryos(cycle_id);
```

---

### Task 9.4: UI/UX Polish
**Priority:** P1 | **Estimate:** 24h | **Assignee:** Frontend

**Tasks:**
- Loading states & skeletons
- Error handling & toasts
- Form validation messages (Vietnamese)
- Responsive layout fixes
- Keyboard navigation
- Print stylesheet optimization

---

### Task 9.5: Testing & Bug Fixes
**Priority:** P0 | **Estimate:** 40h | **Assignee:** All

**Testing Checklist:**
- [ ] All API endpoints tested
- [ ] Form validations work
- [ ] Queue real-time updates
- [ ] Billing calculations correct
- [ ] Print layouts verified
- [ ] Cross-browser testing
- [ ] Load testing (100 concurrent users)

---

### Task 9.6: Documentation
**Priority:** P1 | **Estimate:** 16h | **Assignee:** All

**Deliverables:**
- API documentation (Swagger)
- User manual (Vietnamese) - PDF
- Admin guide
- Deployment guide
- Database ERD

---

## Definition of Done

Each task must meet:
- [ ] Code compiles without warnings
- [ ] Unit tests pass (>80% coverage for critical paths)
- [ ] API documentation updated (Swagger)
- [ ] Code reviewed by peer
- [ ] UI matches design specs
- [ ] Works on Chrome, Firefox, Edge
- [ ] Vietnamese labels/messages correct
