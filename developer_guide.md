# IVF System - Developer Planning Guide

## Overview

Detailed technical specifications for developers implementing the IVF Information System.

---

## Database Schema

### Core Tables

```sql
-- Patients
CREATE TABLE patients (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_code VARCHAR(20) UNIQUE NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    date_of_birth DATE NOT NULL,
    gender VARCHAR(10) CHECK (gender IN ('Male', 'Female')),
    identity_number VARCHAR(20),
    phone VARCHAR(20),
    address TEXT,
    photo BYTEA,
    fingerprint BYTEA,
    patient_type VARCHAR(20) CHECK (patient_type IN ('Infertility', 'EggDonor', 'SpermDonor')),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Couples
CREATE TABLE couples (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    wife_id UUID REFERENCES patients(id) NOT NULL,
    husband_id UUID REFERENCES patients(id) NOT NULL,
    sperm_donor_id UUID REFERENCES patients(id),
    marriage_date DATE,
    infertility_years INT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Treatment Cycles
CREATE TABLE treatment_cycles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    couple_id UUID REFERENCES couples(id) NOT NULL,
    cycle_code VARCHAR(20) UNIQUE NOT NULL,
    method VARCHAR(10) CHECK (method IN ('QHTN', 'IUI', 'ICSI', 'IVM')),
    current_phase VARCHAR(30),
    start_date DATE NOT NULL,
    end_date DATE,
    outcome VARCHAR(30),
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Queue Tickets
CREATE TABLE queue_tickets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_number VARCHAR(20) NOT NULL,
    queue_type VARCHAR(20) NOT NULL,
    patient_id UUID REFERENCES patients(id) NOT NULL,
    cycle_id UUID REFERENCES treatment_cycles(id),
    department_code VARCHAR(20) NOT NULL,
    status VARCHAR(20) DEFAULT 'Waiting',
    issued_at TIMESTAMPTZ DEFAULT NOW(),
    called_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    called_by_user_id UUID
);

-- Ultrasound Records
CREATE TABLE ultrasounds (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cycle_id UUID REFERENCES treatment_cycles(id) NOT NULL,
    exam_date TIMESTAMPTZ NOT NULL,
    ultrasound_type VARCHAR(30), -- PhụKhoa, NangNoãn, NMTC, Thai
    left_ovary_count INT,
    right_ovary_count INT,
    endometrium_thickness DECIMAL(5,2),
    left_follicles JSONB,  -- [{size: 15, position: 1}, ...]
    right_follicles JSONB,
    findings TEXT,
    doctor_id UUID,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Embryos
CREATE TABLE embryos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cycle_id UUID REFERENCES treatment_cycles(id) NOT NULL,
    embryo_number INT NOT NULL,
    fertilization_date DATE,
    grade VARCHAR(5),
    day VARCHAR(5) CHECK (day IN ('D1', 'D2', 'D3', 'D4', 'D5', 'D6')),
    status VARCHAR(20) CHECK (status IN ('Developing', 'Transferred', 'Frozen', 'Discarded')),
    cryo_location_id UUID,
    freeze_date DATE,
    thaw_date DATE,
    notes TEXT
);

-- Cryo Storage Locations
CREATE TABLE cryo_locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tank VARCHAR(20),
    canister VARCHAR(20),
    cane VARCHAR(20),
    goblet VARCHAR(20),
    straw VARCHAR(20),
    specimen_type VARCHAR(20), -- Embryo, Sperm, Oocyte
    is_occupied BOOLEAN DEFAULT FALSE
);

-- Semen Analysis
CREATE TABLE semen_analyses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id UUID REFERENCES patients(id) NOT NULL,
    cycle_id UUID REFERENCES treatment_cycles(id),
    analysis_date TIMESTAMPTZ NOT NULL,
    volume DECIMAL(5,2),
    concentration DECIMAL(10,2),
    motility_a DECIMAL(5,2),
    motility_b DECIMAL(5,2),
    motility_c DECIMAL(5,2),
    motility_d DECIMAL(5,2),
    morphology DECIMAL(5,2),
    analysis_type VARCHAR(20), -- PreWash, PostWash
    notes TEXT
);

-- Sperm Bank Donors
CREATE TABLE sperm_donors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id UUID REFERENCES patients(id) NOT NULL,
    donor_code VARCHAR(20) UNIQUE NOT NULL,
    status VARCHAR(20) CHECK (status IN ('Screening', 'Active', 'Inactive', 'Rejected')),
    screening_date DATE,
    hiv_retest_date DATE,
    hiv_retest_result VARCHAR(20),
    max_couples INT DEFAULT 2,
    current_couples INT DEFAULT 0
);

-- Sperm Samples
CREATE TABLE sperm_samples (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    donor_id UUID REFERENCES sperm_donors(id) NOT NULL,
    sample_number INT NOT NULL,
    collection_date DATE NOT NULL,
    cryo_location_id UUID REFERENCES cryo_locations(id),
    status VARCHAR(20) CHECK (status IN ('Available', 'Reserved', 'Used', 'Discarded')),
    used_by_couple_id UUID REFERENCES couples(id),
    used_date DATE
);

-- Invoices
CREATE TABLE invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_number VARCHAR(30) UNIQUE NOT NULL,
    patient_id UUID REFERENCES patients(id) NOT NULL,
    cycle_id UUID REFERENCES treatment_cycles(id),
    total_amount DECIMAL(15,2) NOT NULL,
    paid_amount DECIMAL(15,2) DEFAULT 0,
    status VARCHAR(20) CHECK (status IN ('Pending', 'Partial', 'Paid', 'Refunded')),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    paid_at TIMESTAMPTZ
);

-- Invoice Items
CREATE TABLE invoice_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id UUID REFERENCES invoices(id) NOT NULL,
    service_code VARCHAR(30) NOT NULL,
    service_name VARCHAR(200) NOT NULL,
    quantity INT DEFAULT 1,
    unit_price DECIMAL(15,2) NOT NULL,
    discount DECIMAL(15,2) DEFAULT 0,
    total DECIMAL(15,2) NOT NULL
);

-- Prescriptions
CREATE TABLE prescriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id UUID REFERENCES patients(id) NOT NULL,
    cycle_id UUID REFERENCES treatment_cycles(id),
    doctor_id UUID NOT NULL,
    prescription_date DATE NOT NULL,
    status VARCHAR(20) DEFAULT 'Pending',
    dispensed_at TIMESTAMPTZ,
    notes TEXT
);

-- Prescription Items
CREATE TABLE prescription_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    prescription_id UUID REFERENCES prescriptions(id) NOT NULL,
    drug_code VARCHAR(30),
    drug_name VARCHAR(200) NOT NULL,
    dosage VARCHAR(100),
    frequency VARCHAR(100),
    duration VARCHAR(50),
    quantity INT NOT NULL
);

-- Users (Staff)
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    role VARCHAR(30) NOT NULL,
    department VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

---

## Workflow State Machines

### IVF Cycle States

```
┌──────────────┐
│ Registration │
└──────┬───────┘
       │
       ▼
┌──────────────┐     ┌─────────────┐
│ Consultation │────►│ KTBT Start  │
└──────────────┘     └──────┬──────┘
                            │
                     ┌──────▼──────┐
                     │  KTRT Tiêm  │
                     └──────┬──────┘
                            │ 36h
                     ┌──────▼──────┐
                     │  Chọc Hút   │
                     └──────┬──────┘
                            │
              ┌─────────────┼─────────────┐
              │             │             │
       ┌──────▼──────┐     │      ┌──────▼──────┐
       │ Nuôi Phôi   │     │      │  Trữ Toàn   │
       └──────┬──────┘     │      │    Bộ       │
              │            │      └─────────────┘
       ┌──────▼──────┐     │
       │Chuyển Phôi  │◄────┘
       └──────┬──────┘
              │ 14 days
       ┌──────▼──────┐
       │  Thử Thai   │
       └──────┬──────┘
              │
     ┌────────┼────────┐
     │ (+)    │    (-) │
┌────▼────┐   │   ┌────▼────┐
│Thai 7w  │   │   │End/Retry│
└────┬────┘   │   └─────────┘
     │        │
┌────▼────┐   │
│Thai 11w │   │
└────┬────┘   │
     │        │
     ▼        │
 Transfer     │
 to OB/GYN────┘
```

### Queue Ticket States

```
                ┌─────────┐
                │ Issued  │
                └────┬────┘
                     │
                ┌────▼────┐
           ┌────│ Waiting │
           │    └────┬────┘
           │         │ (called)
      (skip)    ┌────▼────┐
           │    │ Called  │
           │    └────┬────┘
           │         │
           │    ┌────▼─────┐
           │    │In Service│
           │    └────┬─────┘
           │         │
           │    ┌────▼────┐
           └───►│Completed│
                └─────────┘
```

---

## API Specifications

### Patient Module

#### POST /api/patients
```json
Request:
{
  "fullName": "Nguyễn Thị A",
  "dateOfBirth": "1990-05-15",
  "gender": "Female",
  "identityNumber": "012345678901",
  "phone": "0901234567",
  "address": "123 Đường ABC, Q1, HCM",
  "patientType": "Infertility"
}

Response:
{
  "id": "uuid",
  "patientCode": "BN-2026-001234",
  "fullName": "Nguyễn Thị A",
  ...
}
```

#### POST /api/couples
```json
Request:
{
  "wifeId": "uuid",
  "husbandId": "uuid",
  "marriageDate": "2018-06-20",
  "infertilityYears": 5
}
```

### Queue Module

#### POST /api/queue/tickets
```json
Request:
{
  "patientId": "uuid",
  "queueType": "SieuAm",
  "departmentCode": "SA-PK"
}

Response:
{
  "id": "uuid",
  "ticketNumber": "SA-042",
  "status": "Waiting",
  "estimatedWait": 15
}
```

#### SignalR Hub: /hubs/queue
```javascript
// Client subscribes to department
connection.invoke("JoinDepartment", "SA-PK");

// Server broadcasts
connection.on("TicketCalled", (ticketNumber, patientName) => {
  // Update display
});

connection.on("QueueUpdated", (queue) => {
  // Refresh queue list
});
```

### Cycle Module

#### POST /api/cycles/{id}/ultrasound
```json
Request:
{
  "examDate": "2026-02-03T10:30:00Z",
  "ultrasoundType": "NangNoãn",
  "leftOvaryCount": 8,
  "rightOvaryCount": 6,
  "endometriumThickness": 9.5,
  "leftFollicles": [
    {"size": 18, "position": 1},
    {"size": 16, "position": 2}
  ],
  "rightFollicles": [
    {"size": 17, "position": 1}
  ],
  "findings": "Nang noãn phát triển tốt"
}
```

---

## Feature Breakdown by Sprint

### Sprint 1 (Week 1-2): Project Setup
| Task | Hours | Assignee |
|------|-------|----------|
| .NET 10 solution setup | 4 | Backend |
| PostgreSQL + EF Core setup | 8 | Backend |
| Angular 17 project init | 4 | Frontend |
| PrimeNG integration | 4 | Frontend |
| Auth module (JWT) | 16 | Backend |
| Login/Logout UI | 8 | Frontend |

### Sprint 2 (Week 3-4): Patient & Queue
| Task | Hours | Assignee |
|------|-------|----------|
| Patient CRUD API | 16 | Backend |
| Patient registration UI | 16 | Frontend |
| Patient search | 8 | Full-stack |
| Queue ticket API | 12 | Backend |
| SignalR hub setup | 8 | Backend |
| Queue display UI | 16 | Frontend |
| Queue caller UI | 12 | Frontend |

### Sprint 3 (Week 5-6): Consultation
| Task | Hours | Assignee |
|------|-------|----------|
| Couple management | 12 | Backend |
| Treatment cycle creation | 16 | Backend |
| Consultation screen | 24 | Frontend |
| Prescription API | 12 | Backend |
| Prescription UI | 16 | Frontend |

### Sprint 4 (Week 7-8): Ultrasound
| Task | Hours | Assignee |
|------|-------|----------|
| Ultrasound recording API | 16 | Backend |
| Follicle monitoring form | 24 | Frontend |
| Ultrasound history view | 12 | Frontend |
| SA Phụ Khoa form | 16 | Frontend |

### Sprint 5 (Week 9-10): Lab (LABO)
| Task | Hours | Assignee |
|------|-------|----------|
| Embryo tracking API | 20 | Backend |
| Embryo dashboard | 24 | Frontend |
| Cryo storage management | 16 | Backend |
| Cryo location UI | 16 | Frontend |

### Sprint 6 (Week 11-12): Andrology
| Task | Hours | Assignee |
|------|-------|----------|
| Semen analysis API | 16 | Backend |
| Semen analysis form | 20 | Frontend |
| Sperm washing module | 12 | Backend |
| Andrology dashboard | 16 | Frontend |

### Sprint 7 (Week 13-14): Sperm Bank
| Task | Hours | Assignee |
|------|-------|----------|
| Donor registration | 16 | Backend |
| Sample tracking | 16 | Backend |
| Donor-couple matching | 12 | Backend |
| NHTT workflows UI | 24 | Frontend |
| HIV retest tracking | 8 | Full-stack |

### Sprint 8 (Week 15-16): Billing
| Task | Hours | Assignee |
|------|-------|----------|
| Invoice API | 20 | Backend |
| Service pricing | 12 | Backend |
| Payment recording | 16 | Backend |
| Billing UI | 24 | Frontend |
| Receipt printing | 8 | Frontend |

### Sprint 9 (Week 17-18): Polish
| Task | Hours | Assignee |
|------|-------|----------|
| Reports & analytics | 24 | Full-stack |
| Testing & bug fixes | 40 | All |
| Performance optimization | 16 | Backend |
| UI/UX improvements | 24 | Frontend |

---

## Service Pricing Reference

| Service Code | Service Name (VN) | Price (VND) |
|--------------|-------------------|-------------|
| TV-001 | Phí tư vấn | 300,000 |
| SA-PK | Siêu âm phụ khoa | 200,000 |
| SA-NN | Siêu âm nang noãn (cả chu kỳ) | 1,500,000 |
| SA-NM | Siêu âm niêm mạc (cả chu kỳ) | 1,000,000 |
| XN-AMH | Xét nghiệm AMH | 800,000 |
| XN-TDD | Tinh dịch đồ | 500,000 |
| TT-CH | Chọc hút + Chuyển phôi | 25,000,000 |
| TT-IUI | Thủ thuật IUI | 5,000,000 |
| TR-TOP1 | Trữ phôi (top đầu) | 8,000,000 |
| TR-TOP+ | Trữ phôi (top thêm) | 2,000,000 |
| NHTT-SD | Sử dụng mẫu NHTT | 15,000,000 |

---

## Role-Based Access

| Role | Modules Access |
|------|----------------|
| Admin | All |
| Receptionist | Patient, Queue, Billing |
| Doctor | Consultation, Ultrasound, Prescription |
| Nurse | Queue, Patient view |
| Lab Tech | Lab, Embryo, Cryo |
| Andrologist | Andrology, Sperm Bank |
| Pharmacist | Pharmacy, Prescription view |
| Accountant | Billing, Reports |
