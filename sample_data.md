# IVF System - Sample Data & Flows

## Sample Data for All Modules

---

## Sprint 1: Foundation

### Users (Staff)
```sql
INSERT INTO users (id, username, password_hash, full_name, role, department, is_active) VALUES
('11111111-1111-1111-1111-111111111111', 'admin', '$2a$11$...', 'System Admin', 'Admin', NULL, true),
('22222222-2222-2222-2222-222222222222', 'letan01', '$2a$11$...', 'Nguyễn Thị Hoa', 'Receptionist', 'TiepDon', true),
('33333333-3333-3333-3333-333333333333', 'bstuvan01', '$2a$11$...', 'BS. Trần Văn Minh', 'Doctor', 'TuVan', true),
('44444444-4444-4444-4444-444444444444', 'bssa01', '$2a$11$...', 'BS. Lê Thị Lan', 'Doctor', 'SieuAm', true),
('55555555-5555-5555-5555-555555555555', 'ktv01', '$2a$11$...', 'Phạm Văn Đức', 'LabTech', 'LABO', true),
('66666666-6666-6666-6666-666666666666', 'namkhoa01', '$2a$11$...', 'Hoàng Văn Nam', 'Andrologist', 'NamKhoa', true),
('77777777-7777-7777-7777-777777777777', 'duoc01', '$2a$11$...', 'Vũ Thị Mai', 'Pharmacist', 'NhaThuoc', true);
```

### Departments
```sql
INSERT INTO departments (code, name_vn, name_en, queue_prefix) VALUES
('TD', 'Tiếp đón + Thu ngân', 'Reception', 'TD'),
('TV', 'Phòng Tư vấn', 'Consultation', 'TV'),
('SA-PK', 'Siêu âm Phụ khoa', 'Gynecological US', 'SA'),
('SA-NN', 'Siêu âm Nang noãn', 'Follicle US', 'NN'),
('XN', 'Xét nghiệm', 'Lab Testing', 'XN'),
('NK', 'Nam khoa', 'Andrology', 'NK'),
('LABO', 'Phòng Lab', 'Laboratory', 'LB'),
('GM', 'Gây mê', 'Anesthesia', NULL),
('NT', 'Nhà thuốc', 'Pharmacy', 'NT');
```

---

## Sprint 2: Patient & Queue

### Sample Patients
```sql
INSERT INTO patients (id, patient_code, full_name, date_of_birth, gender, identity_number, phone, address, patient_type, created_at) VALUES
-- Wife
('aaaa1111-1111-1111-1111-111111111111', 'BN-2026-000001', 'Nguyễn Thị Mai', '1992-05-15', 'Female', '012345678901', '0901234567', '123 Nguyễn Huệ, Q1, HCM', 'Infertility', NOW()),
-- Husband
('aaaa2222-2222-2222-2222-222222222222', 'BN-2026-000002', 'Trần Văn Hùng', '1990-08-20', 'Male', '012345678902', '0901234568', '123 Nguyễn Huệ, Q1, HCM', 'Infertility', NOW()),
-- Another couple
('aaaa3333-3333-3333-3333-333333333333', 'BN-2026-000003', 'Lê Thị Hồng', '1988-03-10', 'Female', '012345678903', '0907654321', '456 Lê Lợi, Q3, HCM', 'Infertility', NOW()),
('aaaa4444-4444-4444-4444-444444444444', 'BN-2026-000004', 'Phạm Văn Long', '1985-11-25', 'Male', '012345678904', '0907654322', '456 Lê Lợi, Q3, HCM', 'Infertility', NOW()),
-- Egg Donor
('aaaa5555-5555-5555-5555-555555555555', 'BN-2026-000005', 'Hoàng Thị Linh', '1995-07-08', 'Female', '012345678905', '0912345678', '789 CMT8, Q10, HCM', 'EggDonor', NOW()),
-- Sperm Donor
('aaaa6666-6666-6666-6666-666666666666', 'BN-2026-000006', 'Võ Văn Tuấn', '1993-01-30', 'Male', '012345678906', '0923456789', '321 Hai Bà Trưng, Q1, HCM', 'SpermDonor', NOW());
```

### Sample Couples
```sql
INSERT INTO couples (id, wife_id, husband_id, sperm_donor_id, marriage_date, infertility_years, created_at) VALUES
('bbbb1111-1111-1111-1111-111111111111', 'aaaa1111-1111-1111-1111-111111111111', 'aaaa2222-2222-2222-2222-222222222222', NULL, '2018-06-20', 5, NOW()),
('bbbb2222-2222-2222-2222-222222222222', 'aaaa3333-3333-3333-3333-333333333333', 'aaaa4444-4444-4444-4444-444444444444', NULL, '2015-12-10', 8, NOW());
```

### Queue Tickets Sample (Daily Flow)
```sql
-- Morning queue
INSERT INTO queue_tickets (id, ticket_number, queue_type, patient_id, department_code, status, issued_at, called_at, completed_at) VALUES
('cccc1111-1111-1111-1111-111111111111', 'SA-001', 'SieuAm', 'aaaa1111-1111-1111-1111-111111111111', 'SA-PK', 'Completed', '2026-02-03 08:00:00', '2026-02-03 08:15:00', '2026-02-03 08:30:00'),
('cccc2222-2222-2222-2222-222222222222', 'TV-001', 'TuVan', 'aaaa1111-1111-1111-1111-111111111111', 'TV', 'Completed', '2026-02-03 08:30:00', '2026-02-03 08:45:00', '2026-02-03 09:15:00'),
('cccc3333-3333-3333-3333-333333333333', 'XN-001', 'XetNghiem', 'aaaa1111-1111-1111-1111-111111111111', 'XN', 'Completed', '2026-02-03 09:15:00', '2026-02-03 09:20:00', '2026-02-03 09:30:00'),
('cccc4444-4444-4444-4444-444444444444', 'NK-001', 'NamKhoa', 'aaaa2222-2222-2222-2222-222222222222', 'NK', 'Called', '2026-02-03 09:15:00', '2026-02-03 09:25:00', NULL),
('cccc5555-5555-5555-5555-555555555555', 'SA-002', 'SieuAm', 'aaaa3333-3333-3333-3333-333333333333', 'SA-PK', 'Waiting', '2026-02-03 09:30:00', NULL, NULL);
```

### Queue Flow Diagram
```
┌─────────────────────────────────────────────────────────────────┐
│                    PATIENT JOURNEY - Day 1                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  08:00  ┌──────────┐                                            │
│    ───▶ │ Get STT  │ SA-001                                     │
│         └────┬─────┘                                            │
│              │                                                   │
│  08:15  ┌────▼──────────┐                                       │
│    ───▶ │  Siêu Âm PK   │ 15 min                                │
│         └────┬──────────┘                                       │
│              │                                                   │
│  08:30  ┌────▼──────────┐                                       │
│    ───▶ │ Get STT TV-001│                                       │
│         └────┬──────────┘                                       │
│              │                                                   │
│  08:45  ┌────▼──────────┐                                       │
│    ───▶ │  BS Tư Vấn    │ 30 min                                │
│         └────┬──────────┘                                       │
│              │                                                   │
│  09:15  ┌────▼──────────┐  ┌──────────────┐                     │
│    ───▶ │   Thu Ngân    │─▶│ XN + TDĐ     │                     │
│         └───────────────┘  └──────────────┘                     │
│                                                                  │
│  ════════════════════════════════════════════════════════════   │
│                    Return next day for results                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Sprint 3: Consultation

### Treatment Cycles
```sql
INSERT INTO treatment_cycles (id, couple_id, cycle_code, method, current_phase, start_date, end_date, outcome, notes) VALUES
-- Active ICSI cycle
('dddd1111-1111-1111-1111-111111111111', 'bbbb1111-1111-1111-1111-111111111111', 'CK-2026-0001', 'ICSI', 'OvarianStimulation', '2026-01-25', NULL, NULL, 'First IVF attempt'),
-- Completed IUI cycle
('dddd2222-2222-2222-2222-222222222222', 'bbbb2222-2222-2222-2222-222222222222', 'CK-2026-0002', 'IUI', 'Completed', '2026-01-10', '2026-02-01', 'NotPregnant', 'Third IUI attempt, proceeding to IVF'),
-- New cycle starting
('dddd3333-3333-3333-3333-333333333333', 'bbbb2222-2222-2222-2222-222222222222', 'CK-2026-0003', 'ICSI', 'Consultation', '2026-02-03', NULL, NULL, 'First IVF after 3 failed IUI');
```

### Prescriptions
```sql
INSERT INTO prescriptions (id, patient_id, cycle_id, doctor_id, prescription_date, status, notes) VALUES
('eeee1111-1111-1111-1111-111111111111', 'aaaa1111-1111-1111-1111-111111111111', 'dddd1111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333', '2026-01-25', 'Dispensed', 'KTBT medication');

INSERT INTO prescription_items (id, prescription_id, drug_code, drug_name, dosage, frequency, duration, quantity) VALUES
('ffff1111-1111-1111-1111-111111111111', 'eeee1111-1111-1111-1111-111111111111', 'GON-001', 'Gonal-F 300IU', '150IU', 'Mỗi ngày', '10 ngày', 2),
('ffff2222-2222-2222-2222-222222222222', 'eeee1111-1111-1111-1111-111111111111', 'MER-001', 'Menopur 75IU', '75IU', 'Mỗi ngày', '10 ngày', 10),
('ffff3333-3333-3333-3333-333333333333', 'eeee1111-1111-1111-1111-111111111111', 'CET-001', 'Cetrotide 0.25mg', '0.25mg', 'Mỗi ngày', '5 ngày', 5);
```

### KTBT Flow
```
┌─────────────────────────────────────────────────────────────────┐
│                 OVARIAN STIMULATION (KTBT) FLOW                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Day 1 (N2 VK)                                                  │
│  ┌────────────────┐                                             │
│  │ Start Gonal-F  │                                             │
│  │ + Menopur      │                                             │
│  └───────┬────────┘                                             │
│          │ 4-5 days                                             │
│          ▼                                                       │
│  Day 5   ┌────────────────┐                                     │
│  ┌───────│  US + Hormone  │ E2, P4, LH                          │
│  │       └───────┬────────┘                                     │
│  │               │ Follicles < 14mm                             │
│  │               ▼ Continue KTBT                                │
│  │       ┌────────────────┐                                     │
│  │       │ Adjust dosage  │                                     │
│  │       └───────┬────────┘                                     │
│  │               │ 2-3 days                                     │
│  │               ▼                                               │
│  Day 8   │ ┌────────────────┐                                   │
│  ────────┼─│  US + Hormone  │ Check again                       │
│          │ └───────┬────────┘                                   │
│          │         │ Follicles 14-16mm                          │
│          │         ▼ Add Cetrotide                              │
│          │ ┌────────────────┐                                   │
│          │ │ Continue 2 days│                                   │
│          │ └───────┬────────┘                                   │
│          │         │                                             │
│          │         ▼                                             │
│  Day 10  │ ┌────────────────┐                                   │
│  ────────┴─│  US + Hormone  │ Follicles ≥ 18mm                  │
│            └───────┬────────┘                                   │
│                    │ READY!                                      │
│                    ▼                                             │
│            ┌────────────────┐                                   │
│            │ Trigger Shot   │ 22:00                             │
│            │ (Ovitrelle)    │                                   │
│            └───────┬────────┘                                   │
│                    │ 36 hours                                    │
│                    ▼                                             │
│  Day 12   ┌────────────────┐                                    │
│            │   Chọc Hút     │ 10:00                             │
│            └────────────────┘                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Sprint 4: Ultrasound

### Ultrasound Records
```sql
INSERT INTO ultrasounds (id, cycle_id, exam_date, ultrasound_type, left_ovary_count, right_ovary_count, endometrium_thickness, left_follicles, right_follicles, findings, doctor_id) VALUES
-- Day 1 baseline
('gggg1111-1111-1111-1111-111111111111', 'dddd1111-1111-1111-1111-111111111111', '2026-01-25 09:00:00', 'NangNoan', 10, 8, 4.5, 
 '[{"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}]',
 '[{"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}, {"size": 5}, {"size": 4}]',
 'AFC 18, bắt đầu KTBT', '44444444-4444-4444-4444-444444444444'),
-- Day 5
('gggg2222-2222-2222-2222-222222222222', 'dddd1111-1111-1111-1111-111111111111', '2026-01-30 09:00:00', 'NangNoan', 10, 7, 6.8,
 '[{"size": 10}, {"size": 9}, {"size": 10}, {"size": 8}, {"size": 9}, {"size": 8}, {"size": 7}, {"size": 6}, {"size": 5}, {"size": 4}]',
 '[{"size": 11}, {"size": 10}, {"size": 9}, {"size": 8}, {"size": 7}, {"size": 6}, {"size": 5}]',
 'Nang noãn phát triển đều, tiếp tục KTBT', '44444444-4444-4444-4444-444444444444'),
-- Day 8 (add antagonist)
('gggg3333-3333-3333-3333-333333333333', 'dddd1111-1111-1111-1111-111111111111', '2026-02-02 09:00:00', 'NangNoan', 9, 6, 9.2,
 '[{"size": 15}, {"size": 14}, {"size": 14}, {"size": 13}, {"size": 12}, {"size": 11}, {"size": 10}, {"size": 8}, {"size": 6}]',
 '[{"size": 16}, {"size": 15}, {"size": 14}, {"size": 13}, {"size": 11}, {"size": 9}]',
 'Thêm Cetrotide, hẹn 2 ngày', '44444444-4444-4444-4444-444444444444'),
-- Day 10 (trigger)
('gggg4444-4444-4444-4444-444444444444', 'dddd1111-1111-1111-1111-111111111111', '2026-02-04 09:00:00', 'NangNoan', 8, 5, 10.5,
 '[{"size": 20}, {"size": 19}, {"size": 18}, {"size": 18}, {"size": 17}, {"size": 16}, {"size": 14}, {"size": 12}]',
 '[{"size": 21}, {"size": 20}, {"size": 19}, {"size": 17}, {"size": 14}]',
 'Nang noãn đạt. Tiêm Ovitrelle 22:00. Chọc hút 2026-02-06 10:00', '44444444-4444-4444-4444-444444444444');
```

### Follicle Growth Chart Data
```json
{
  "cycleId": "dddd1111-1111-1111-1111-111111111111",
  "visits": [
    {"day": 1, "date": "2026-01-25", "avgSize": 4.7, "count": 18, "endo": 4.5},
    {"day": 5, "date": "2026-01-30", "avgSize": 8.1, "count": 17, "endo": 6.8},
    {"day": 8, "date": "2026-02-02", "avgSize": 12.4, "count": 15, "endo": 9.2},
    {"day": 10, "date": "2026-02-04", "avgSize": 17.3, "count": 13, "endo": 10.5}
  ]
}
```

---

## Sprint 5: Lab (LABO)

### Egg Retrieval & Embryos
```sql
-- Egg Retrieval Record
INSERT INTO egg_retrievals (id, cycle_id, retrieval_date, eggs_retrieved, mature_eggs, immature_eggs, atretic_eggs, doctor_id, notes) VALUES
('hhhh1111-1111-1111-1111-111111111111', 'dddd1111-1111-1111-1111-111111111111', '2026-02-06 10:30:00', 12, 10, 1, 1, '33333333-3333-3333-3333-333333333333', 'No complications');

-- Embryos
INSERT INTO embryos (id, cycle_id, embryo_number, fertilization_date, grade, day, status, cryo_location_id, freeze_date, notes) VALUES
('iiii1111-1111-1111-1111-111111111111', 'dddd1111-1111-1111-1111-111111111111', 1, '2026-02-07', 'AA', 'D5', 'Transferred', NULL, NULL, 'Best quality, transferred'),
('iiii2222-2222-2222-2222-222222222222', 'dddd1111-1111-1111-1111-111111111111', 2, '2026-02-07', 'AB', 'D5', 'Frozen', 'jjjj1111-1111-1111-1111-111111111111', '2026-02-11', 'Good quality'),
('iiii3333-3333-3333-3333-333333333333', 'dddd1111-1111-1111-1111-111111111111', 3, '2026-02-07', 'BB', 'D5', 'Frozen', 'jjjj2222-2222-2222-2222-222222222222', '2026-02-11', 'Average quality'),
('iiii4444-4444-4444-4444-444444444444', 'dddd1111-1111-1111-1111-111111111111', 4, '2026-02-07', 'BC', 'D6', 'Frozen', 'jjjj3333-3333-3333-3333-333333333333', '2026-02-12', 'Slow development'),
('iiii5555-5555-5555-5555-555555555555', 'dddd1111-1111-1111-1111-111111111111', 5, '2026-02-07', 'CC', 'D5', 'Discarded', NULL, NULL, 'Poor quality'),
('iiii6666-6666-6666-6666-666666666666', 'dddd1111-1111-1111-1111-111111111111', 6, '2026-02-07', 'CD', 'D5', 'Discarded', NULL, NULL, 'Fragmented'),
('iiii7777-7777-7777-7777-777777777777', 'dddd1111-1111-1111-1111-111111111111', 7, '2026-02-07', NULL, 'D3', 'Arrested', NULL, NULL, 'Arrested at D3'),
('iiii8888-8888-8888-8888-888888888888', 'dddd1111-1111-1111-1111-111111111111', 8, '2026-02-07', NULL, 'D3', 'Arrested', NULL, NULL, 'Arrested at D3');
```

### Cryo Locations
```sql
INSERT INTO cryo_locations (id, tank, canister, cane, goblet, straw, specimen_type, is_occupied) VALUES
('jjjj1111-1111-1111-1111-111111111111', 'T1', 'C1', 'N1', 'G1', 'S1', 'Embryo', true),
('jjjj2222-2222-2222-2222-222222222222', 'T1', 'C1', 'N1', 'G1', 'S2', 'Embryo', true),
('jjjj3333-3333-3333-3333-333333333333', 'T1', 'C1', 'N1', 'G2', 'S1', 'Embryo', true),
('jjjj4444-4444-4444-4444-444444444444', 'T1', 'C1', 'N1', 'G2', 'S2', 'Embryo', false),
('jjjj5555-5555-5555-5555-555555555555', 'T2', 'C1', 'N1', 'G1', 'S1', 'Sperm', true),
('jjjj6666-6666-6666-6666-666666666666', 'T2', 'C1', 'N1', 'G1', 'S2', 'Sperm', false);
```

### Embryo Development Flow
```
┌─────────────────────────────────────────────────────────────────────────┐
│                        EMBRYO DEVELOPMENT TRACKING                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Day 0 (Retrieval)      Day 1 (Fertilization)    Day 3                  │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐      │
│  │  12 Eggs        │───▶│  10 Fertilized  │───▶│ 8 Cleaving      │      │
│  │  Retrieved      │    │  (2PN)          │    │ (6-8 cells)     │      │
│  └─────────────────┘    └─────────────────┘    └────────┬────────┘      │
│                                                          │               │
│                                                          │               │
│                          Day 5 (Blastocyst)              ▼               │
│                         ┌─────────────────────────────────────┐         │
│                         │                                     │         │
│    ┌────────────────────┤      6 Blastocysts                 │         │
│    │                    │                                     │         │
│    │                    └──────┬──────────────────────────────┘         │
│    │                           │                                         │
│    │     ┌─────────────────────┼─────────────────────┐                  │
│    │     │                     │                     │                  │
│    │     ▼                     ▼                     ▼                  │
│    │  ┌──────┐             ┌──────┐             ┌──────┐               │
│    │  │ #1   │             │ #2,3 │             │ #5,6 │               │
│    │  │ AA   │             │ AB,BB│             │ CC,CD│               │
│    │  │      │             │      │             │      │               │
│    │  │TRANS │             │FROZEN│             │DISCAR│               │
│    │  └──────┘             └──────┘             └──────┘               │
│    │                                                                    │
│    │  Day 6                                                             │
│    │  ┌──────┐                                                          │
│    │  │ #4   │                                                          │
│    │  │ BC   │                                                          │
│    │  │FROZEN│                                                          │
│    │  └──────┘                                                          │
│    │                                                                    │
│    │  Arrested at D3: #7, #8                                            │
│    │                                                                    │
└────┴────────────────────────────────────────────────────────────────────┘

SUMMARY:
═══════════════════════════════════════
  Eggs Retrieved:    12
  Mature (MII):      10
  Fertilized (2PN):  10
  Cleaving D3:        8
  Blastocysts:        6
  Transferred:        1 (AA)
  Frozen:             3 (AB, BB, BC)
  Discarded:          2 (CC, CD)
  Arrested:           2
═══════════════════════════════════════
```

---

## Sprint 6: Andrology

### Semen Analysis
```sql
INSERT INTO semen_analyses (id, patient_id, cycle_id, analysis_date, volume, concentration, motility_a, motility_b, motility_c, motility_d, morphology, analysis_type, notes) VALUES
-- Pre-wash
('kkkk1111-1111-1111-1111-111111111111', 'aaaa2222-2222-2222-2222-222222222222', 'dddd1111-1111-1111-1111-111111111111', '2026-02-06 09:00:00', 2.5, 45.0, 25.0, 15.0, 10.0, 50.0, 5.0, 'PreWash', 'Sample from husband'),
-- Post-wash
('kkkk2222-2222-2222-2222-222222222222', 'aaaa2222-2222-2222-2222-222222222222', 'dddd1111-1111-1111-1111-111111111111', '2026-02-06 10:00:00', 0.5, 80.0, 60.0, 25.0, 5.0, 10.0, NULL, 'PostWash', 'After gradient washing');
```

### Sperm Washing Flow
```
┌─────────────────────────────────────────────────────────────────┐
│                     SPERM WASHING PROCESS                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐                                             │
│  │ Sample Receipt │ 09:00                                       │
│  │ Vol: 2.5ml     │                                             │
│  └───────┬────────┘                                             │
│          │                                                       │
│          ▼                                                       │
│  ┌────────────────┐                                             │
│  │ Pre-Wash TDĐ   │                                             │
│  │ Conc: 45M/ml   │                                             │
│  │ Motility: 50%  │ (A+B+C)                                     │
│  │ Normal: 5%     │                                             │
│  └───────┬────────┘                                             │
│          │                                                       │
│          ▼                                                       │
│  ┌────────────────┐                                             │
│  │ Gradient Wash  │ 45 min                                      │
│  │ (80%/40%)      │                                             │
│  └───────┬────────┘                                             │
│          │                                                       │
│          ▼                                                       │
│  ┌────────────────┐                                             │
│  │ Post-Wash TDĐ  │                                             │
│  │ Vol: 0.5ml     │                                             │
│  │ Conc: 80M/ml   │                                             │
│  │ Motility: 90%  │ (A+B+C)                                     │
│  └───────┬────────┘                                             │
│          │                                                       │
│          ▼                                                       │
│  ┌────────────────┐                                             │
│  │ Ready for ICSI │ 10:00                                       │
│  └────────────────┘                                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Sprint 7: Sperm Bank

### Sperm Donors
```sql
INSERT INTO sperm_donors (id, patient_id, donor_code, status, screening_date, hiv_retest_date, hiv_retest_result, max_couples, current_couples) VALUES
('llll1111-1111-1111-1111-111111111111', 'aaaa6666-6666-6666-6666-666666666666', 'NH-2026-001', 'Active', '2025-10-15', '2026-01-20', 'Negative', 2, 1);
```

### Sperm Samples
```sql
INSERT INTO sperm_samples (id, donor_id, sample_number, collection_date, cryo_location_id, status, used_by_couple_id, used_date) VALUES
('mmmm1111-1111-1111-1111-111111111111', 'llll1111-1111-1111-1111-111111111111', 1, '2025-10-20', 'jjjj5555-5555-5555-5555-555555555555', 'Used', 'bbbb2222-2222-2222-2222-222222222222', '2026-01-10'),
('mmmm2222-2222-2222-2222-222222222222', 'llll1111-1111-1111-1111-111111111111', 2, '2025-10-25', 'jjjj6666-6666-6666-6666-666666666666', 'Available', NULL, NULL);
```

### NHTT Flow
```
┌─────────────────────────────────────────────────────────────────────────┐
│                      SPERM BANK (NHTT) WORKFLOW                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Phase 1: SCREENING                                                      │
│  ═══════════════════                                                     │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐               │
│  │ Registration │───▶│ TDĐ Check    │───▶│ Blood Tests  │               │
│  │ + Photo/FP   │    │ Normal?      │    │ HIV, HBV...  │               │
│  └──────────────┘    └──────┬───────┘    └──────┬───────┘               │
│                             │                    │                       │
│                     ┌───────┴───────┐    ┌──────┴───────┐               │
│                     │ FAIL          │    │ PASS         │               │
│                     │ → Reject      │    │ → Continue   │               │
│                     └───────────────┘    └──────┬───────┘               │
│                                                  │                       │
│  Phase 2: SAMPLE COLLECTION                      │                       │
│  ══════════════════════════                      ▼                       │
│                             ┌──────────────────────────────┐            │
│                             │      Sample 1 Collection     │            │
│                             │      + Freeze + Store        │            │
│                             └─────────────┬────────────────┘            │
│                                           │ 7 days                       │
│                                           ▼                              │
│                             ┌──────────────────────────────┐            │
│                             │      Sample 2 Collection     │            │
│                             │      + Freeze + Store        │            │
│                             └─────────────┬────────────────┘            │
│                                           │ Schedule HIV                 │
│                                           │ retest +3 months             │
│  Phase 3: HIV RETEST                      ▼                              │
│  ═══════════════════        ┌──────────────────────────────┐            │
│                             │     HIV Retest (3 months)    │            │
│                             └─────────────┬────────────────┘            │
│                                           │                              │
│                             ┌─────────────┴─────────────┐               │
│                             │ Negative     │ Positive   │               │
│                             ▼              ▼            │               │
│                     ┌───────────────┐ ┌───────────────┐                 │
│                     │ STATUS: ACTIVE│ │ DESTROY ALL   │                 │
│                     │ Donor Code    │ │ SAMPLES       │                 │
│                     │ issued        │ │ + Reject      │                 │
│                     └───────────────┘ └───────────────┘                 │
│                                                                          │
│  Phase 4: USAGE                                                          │
│  ══════════════                                                          │
│                     ┌───────────────────────────────────┐               │
│                     │ Match Donor → Couple (max 2)      │               │
│                     │ Issue NHTT Card                   │               │
│                     └───────────────┬───────────────────┘               │
│                                     │                                    │
│                                     ▼                                    │
│                     ┌───────────────────────────────────┐               │
│                     │ On procedure day:                 │               │
│                     │ - Verify identity (photo/FP)      │               │
│                     │ - Thaw sample                     │               │
│                     │ - Use for IUI/ICSI                │               │
│                     └───────────────────────────────────┘               │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Sprint 8: Billing

### Invoices
```sql
INSERT INTO invoices (id, invoice_number, patient_id, cycle_id, total_amount, paid_amount, status, created_at, paid_at) VALUES
('nnnn1111-1111-1111-1111-111111111111', 'HD-2026-000001', 'aaaa1111-1111-1111-1111-111111111111', 'dddd1111-1111-1111-1111-111111111111', 35000000, 35000000, 'Paid', '2026-02-06 08:00:00', '2026-02-06 08:15:00'),
('nnnn2222-2222-2222-2222-222222222222', 'HD-2026-000002', 'aaaa1111-1111-1111-1111-111111111111', 'dddd1111-1111-1111-1111-111111111111', 12000000, 12000000, 'Paid', '2026-02-11 10:00:00', '2026-02-11 10:10:00');
```

### Invoice Items
```sql
INSERT INTO invoice_items (id, invoice_id, service_code, service_name, quantity, unit_price, discount, total) VALUES
-- Invoice 1: Chọc hút + Chuyển phôi
('oooo1111-1111-1111-1111-111111111111', 'nnnn1111-1111-1111-1111-111111111111', 'TT-CH', 'Chọc hút + Chuyển phôi', 1, 25000000, 0, 25000000),
('oooo2222-2222-2222-2222-222222222222', 'nnnn1111-1111-1111-1111-111111111111', 'XN-TM', 'XN Tiền mê', 1, 2000000, 0, 2000000),
('oooo3333-3333-3333-3333-333333333333', 'nnnn1111-1111-1111-1111-111111111111', 'GM-001', 'Phí gây mê', 1, 3000000, 0, 3000000),
('oooo4444-4444-4444-4444-444444444444', 'nnnn1111-1111-1111-1111-111111111111', 'LABO', 'Phí nuôi phôi', 1, 5000000, 0, 5000000),
-- Invoice 2: Trữ phôi
('oooo5555-5555-5555-5555-555555555555', 'nnnn2222-2222-2222-2222-222222222222', 'TR-TOP1', 'Trữ phôi (top đầu - 2 phôi)', 1, 8000000, 0, 8000000),
('oooo6666-6666-6666-6666-666666666666', 'nnnn2222-2222-2222-2222-222222222222', 'TR-TOP+', 'Trữ phôi (top thêm - 1 phôi)', 2, 2000000, 0, 4000000);
```

### Payments
```sql
INSERT INTO payments (id, invoice_id, amount, method, reference, paid_at, received_by) VALUES
('pppp1111-1111-1111-1111-111111111111', 'nnnn1111-1111-1111-1111-111111111111', 20000000, 'Cash', NULL, '2026-02-06 08:10:00', '22222222-2222-2222-2222-222222222222'),
('pppp2222-2222-2222-2222-222222222222', 'nnnn1111-1111-1111-1111-111111111111', 15000000, 'Card', 'VISA-9876', '2026-02-06 08:15:00', '22222222-2222-2222-2222-222222222222'),
('pppp3333-3333-3333-3333-333333333333', 'nnnn2222-2222-2222-2222-222222222222', 12000000, 'Transfer', 'VCB-123456', '2026-02-11 10:10:00', '22222222-2222-2222-2222-222222222222');
```

### Service Pricing
```sql
INSERT INTO services (code, name_vn, name_en, price, category, is_active) VALUES
('TV-001', 'Phí tư vấn', 'Consultation fee', 300000, 'Consultation', true),
('SA-PK', 'Siêu âm phụ khoa', 'Gynecological US', 200000, 'Ultrasound', true),
('SA-NN', 'Siêu âm nang noãn (cả chu kỳ)', 'Follicle monitoring (cycle)', 1500000, 'Ultrasound', true),
('SA-NM', 'Siêu âm niêm mạc (cả chu kỳ)', 'Endometrium monitoring', 1000000, 'Ultrasound', true),
('XN-AMH', 'Xét nghiệm AMH', 'AMH test', 800000, 'LabTest', true),
('XN-TDD', 'Tinh dịch đồ', 'Semen analysis', 500000, 'LabTest', true),
('XN-NT', 'XN Nội tiết (E2+P4+LH)', 'Hormone panel', 600000, 'LabTest', true),
('XN-TM', 'XN Tiền mê', 'Pre-anesthesia tests', 2000000, 'LabTest', true),
('TT-CH', 'Chọc hút + Chuyển phôi', 'Retrieval + Transfer', 25000000, 'Procedure', true),
('TT-IUI', 'Thủ thuật IUI', 'IUI procedure', 5000000, 'Procedure', true),
('GM-001', 'Phí gây mê', 'Anesthesia fee', 3000000, 'Procedure', true),
('LABO', 'Phí nuôi phôi', 'Embryo culture', 5000000, 'Lab', true),
('TR-TOP1', 'Trữ phôi (top đầu)', 'Embryo freezing (first)', 8000000, 'Cryo', true),
('TR-TOP+', 'Trữ phôi (top thêm)', 'Embryo freezing (additional)', 2000000, 'Cryo', true),
('NHTT-SD', 'Sử dụng mẫu NHTT', 'Sperm bank usage', 15000000, 'SpermBank', true);
```

---

## Sprint 9: Reports

### Sample Report Data

**Daily Summary:**
```json
{
  "date": "2026-02-06",
  "patients": {
    "newRegistrations": 5,
    "returnVisits": 23,
    "total": 28
  },
  "procedures": {
    "ultrasounds": 18,
    "consultations": 15,
    "eggRetrievals": 2,
    "embryoTransfers": 1,
    "iui": 1
  },
  "revenue": {
    "consultations": 4500000,
    "ultrasounds": 3600000,
    "procedures": 55000000,
    "medications": 12000000,
    "total": 75100000
  }
}
```

**Monthly Success Rates:**
```json
{
  "period": "2026-01",
  "ivf": {
    "startedCycles": 45,
    "retrievals": 42,
    "transfers": 38,
    "clinicalPregnancy": 18,
    "rate": "47.4%"
  },
  "iui": {
    "cycles": 25,
    "pregnancies": 4,
    "rate": "16.0%"
  },
  "frozenTransfer": {
    "transfers": 22,
    "pregnancies": 12,
    "rate": "54.5%"
  }
}
```

**Cryo Inventory Summary:**
```json
{
  "date": "2026-02-06",
  "embryos": {
    "total": 156,
    "byGrade": {"AA": 12, "AB": 45, "BB": 58, "BC": 28, "CC": 13},
    "expiringSoon": 8
  },
  "sperm": {
    "totalDonors": 15,
    "activeDonors": 12,
    "totalSamples": 45,
    "availableSamples": 28
  },
  "tanks": [
    {"id": "T1", "capacity": 500, "used": 312, "available": 188},
    {"id": "T2", "capacity": 500, "used": 45, "available": 455}
  ]
}
```
