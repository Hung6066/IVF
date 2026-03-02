# IVF System — Implementation Plan for Missing Features

> Generated: 2026-03-02 | System audit: 85% feature complete

---

## Tóm tắt thay đổi ComplianceScoringEngine v2

### Đã hoàn thành ✅

| Thay đổi                    | Chi tiết                                                                                                                    |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| **HIPAA**: 10 → 15 controls | +11: Minimum Necessary, +12: Workforce Security, +13: Incident Response, +14: Contingency Plan (DR), +15: Automatic Logoff  |
| **SOC 2**: 7 → 12 controls  | +CC6.8: Key Management, +CC7.3: Incident Detection, +A1.3: Backup Testing, +CC5.2: Least Privilege, +CC3.4: Risk Assessment |
| **GDPR**: 7 → 11 controls   | +33: Breach Notification, +17: Right to Erasure, +28: Processor Security, +6: Lawful Basis                                  |
| **Total**: 24 → 38 controls | Max score: 240 → 380 points                                                                                                 |
| **Bug fix**: HIPAA-10       | `leases.Count >= 0` (always true) → proper validation                                                                       |
| **New dependencies**        | `ISecurityEventService`, `IVaultDrService` injected                                                                         |
| **Remediation field**       | `ControlResult` now includes actionable remediation text                                                                    |
| **Deeper validation**       | Checks quality, not just count > 0 (e.g., encryption configs ≥ 3, audit logs ≥ 100)                                         |
| **Frontend updated**        | Pass/Partial/Fail summary stats, remediation section shows actionable guidance                                              |
| **Tests**: 23 passing       | All new controls tested, bug fix verified                                                                                   |

---

## Features còn thiếu — Kế hoạch implement

### Phase 1: Pharmacy Module (Ưu tiên cao)

**Lý do**: Pharmacy là module quan trọng nhất còn thiếu. Frontend đã có dashboard nhưng không có backend.

#### Backend (Application + Infrastructure)

| Task                              | Scope                                                                                           | Effort |
| --------------------------------- | ----------------------------------------------------------------------------------------------- | ------ |
| P1.1: Domain entities             | `Drug`, `DrugCategory`, `Prescription`, `PrescriptionItem`, `StockTransaction`, `PurchaseOrder` | Medium |
| P1.2: CQRS handlers               | `CreateDrug`, `UpdateDrug`, `DeleteDrug`, `SearchDrugs`, `GetDrugById`                          | Medium |
| P1.3: Prescription handlers       | `CreatePrescription`, `DispensePrescription`, `GetPatientPrescriptions`                         | Medium |
| P1.4: Stock management            | `AddStock`, `AdjustStock`, `GetStockLevels`, `GetLowStockAlerts`, `GetExpiringDrugs`            | Medium |
| P1.5: Purchase orders             | `CreatePO`, `ReceivePO`, `ListPOs`                                                              | Small  |
| P1.6: Minimal API endpoints       | `PharmacyEndpoints.cs` — ~15 routes                                                             | Medium |
| P1.7: EF Core configs + migration | Entity configurations, FK relationships                                                         | Small  |

#### Frontend (Wire up existing dashboard)

| Task                         | Scope                                                    | Effort |
| ---------------------------- | -------------------------------------------------------- | ------ |
| P1.8: Pharmacy service       | `pharmacy.service.ts` — HTTP calls for all endpoints     | Small  |
| P1.9: Connect dashboard      | Wire existing pharmacy dashboard components to real APIs | Medium |
| P1.10: Drug CRUD dialogs     | Add/edit drug forms, category management                 | Medium |
| P1.11: Prescription workflow | Create prescription from patient context, dispense UI    | Medium |

---

### Phase 2: Injection Protocol Module (Ưu tiên cao)

**Lý do**: Injection tracking là phần quan trọng trong quy trình IVF (kích thích buồng trứng).

#### Backend

| Task                    | Scope                                                                           | Effort |
| ----------------------- | ------------------------------------------------------------------------------- | ------ |
| I2.1: Domain entities   | `InjectionProtocol`, `InjectionSchedule`, `InjectionRecord`, `DosageAdjustment` | Medium |
| I2.2: Protocol handlers | `CreateProtocol`, `GetProtocolByCycleId`, `UpdateDosage`, `RecordInjection`     | Medium |
| I2.3: Schedule handlers | `GenerateSchedule`, `GetTodayInjections`, `MarkAdministered`                    | Medium |
| I2.4: Endpoints         | `InjectionEndpoints.cs` — ~10 routes                                            | Small  |
| I2.5: Cycle integration | Link protocols to `TreatmentCycle` entity                                       | Small  |

#### Frontend

| Task                           | Scope                                              | Effort |
| ------------------------------ | -------------------------------------------------- | ------ |
| I2.6: Injection service        | `injection.service.ts`                             | Small  |
| I2.7: Protocol UI              | Protocol builder, dosage calculator, calendar view | Large  |
| I2.8: Daily injection tracking | Mark as administered, nurse sign-off               | Medium |

---

### Phase 3: Reception Module (Ưu tiên trung bình)

**Lý do**: Frontend đã có dashboard, cần backend cho check-in workflow.

#### Backend

| Task                    | Scope                                                                       | Effort |
| ----------------------- | --------------------------------------------------------------------------- | ------ |
| R3.1: Domain entities   | `CheckIn`, `VisitType` enum, `WaitingRoom`                                  | Small  |
| R3.2: Handlers          | `CheckInPatient`, `GetWaitingList`, `CompleteVisit`, `GetTodayAppointments` | Medium |
| R3.3: Queue integration | Link check-in to SignalR queue hub for real-time updates                    | Small  |
| R3.4: Endpoints         | `ReceptionEndpoints.cs` — ~8 routes                                         | Small  |

#### Frontend

| Task                      | Scope                                         | Effort |
| ------------------------- | --------------------------------------------- | ------ |
| R3.5: Reception service   | `reception.service.ts`                        | Small  |
| R3.6: Wire dashboard      | Connect existing reception components to APIs | Medium |
| R3.7: Document collection | Upload/scan interface during check-in         | Small  |

---

### Phase 4: Enhanced Reporting (Ưu tiên trung bình)

**Lý do**: Reporting cơ bản đã có, cần nâng cấp cho clinical analytics.

| Task                           | Scope                                           | Effort |
| ------------------------------ | ----------------------------------------------- | ------ |
| R4.1: Clinical outcome reports | Success rate by method/doctor/age group         | Medium |
| R4.2: Financial reports        | Revenue by period/service, outstanding invoices | Medium |
| R4.3: PDF export               | Integrate QuestPDF for report export            | Medium |
| R4.4: Excel export             | Generate Excel files for data analysis          | Small  |
| R4.5: Scheduled reports        | Email periodic reports to management            | Medium |
| R4.6: Custom report queries    | Dynamic SQL builder for ad-hoc reporting        | Large  |

---

### Phase 5: Consultation Module (Ưu tiên thấp)

| Task                            | Scope                                               | Effort |
| ------------------------------- | --------------------------------------------------- | ------ |
| C5.1: Consultation templates    | Predefined forms for different visit types          | Medium |
| C5.2: Doctor notes              | Rich text notes with template insertion             | Medium |
| C5.3: Follow-up scheduling      | Auto-create follow-up appointments                  | Small  |
| C5.4: Treatment recommendations | Template-based recommendations with doctor sign-off | Medium |

---

### Phase 6: Unified Inventory System (Ưu tiên thấp)

| Task                        | Scope                                                     | Effort |
| --------------------------- | --------------------------------------------------------- | ------ |
| V6.1: Inventory abstraction | Generic `InventoryItem`, `StockLocation`, `StockMovement` | Large  |
| V6.2: Lab consumables       | Track lab reagents, culture media, pipettes               | Medium |
| V6.3: Alerts                | Low stock, expiry alerts via SignalR notifications        | Medium |
| V6.4: Purchase workflow     | Request → Approve → Order → Receive → Stock               | Large  |

---

## Dependency Map

```
Phase 1 (Pharmacy) ─────────────────────────── Can start immediately
Phase 2 (Injection) ──── Depends on Phase 1 ── Uses Drug entities for medication
Phase 3 (Reception) ─────────────────────────── Can start immediately (parallel with P1)
Phase 4 (Reporting) ─────────────────────────── Can start immediately
Phase 5 (Consultation) ── Optional ──────────── Low priority
Phase 6 (Inventory) ───── Depends on Phase 1 ── Extends pharmacy stock system
```

## Priority Order

1. **Phase 1 + Phase 3** (parallel) — Pharmacy + Reception = highest clinical value
2. **Phase 2** — Injection protocols = core IVF workflow
3. **Phase 4** — Enhanced reporting = management/compliance need
4. **Phase 5-6** — Nice-to-have enhancements

## Architecture Notes

- All new features follow existing Clean Architecture (Domain → Application → Infrastructure → API)
- All handlers use CQRS pattern with MediatR
- All endpoints use Minimal API pattern
- All new entities extend `BaseEntity` (soft-delete, audit timestamps)
- All sensitive fields auto-encrypted via `VaultEncryptionInterceptor`
- All access controlled via existing JWT + RBAC system
