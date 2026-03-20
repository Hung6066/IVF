# 📋 ĐÁNH GIÁ TOÀN BỘ HỆ THỐNG IVF — GAP ANALYSIS & CHECKLIST

> **Ngày đánh giá**: 19/03/2026 (cập nhật lần cuối: 19/03/2026 — ✅ **HOÀN THÀNH 100% tất cả 11 phases**)  
> **So sánh**: `IVFMD_Implementation_Plan.md` (11 phase, ~130 tasks) vs triển khai thực tế  
> **Kết quả**: ✅ **Tất cả 11 phases đã hoàn thành đầy đủ.** Backend 100% (entities + CQRS + endpoints). Frontend 100% (services + UI components + routes). Mọi clinical workflow đã được triển khai.

---

## 📊 TỔNG QUAN TRẠNG THÁI

| Phase        | Mô tả                     | Trạng thái    | % Hoàn thành |
| ------------ | ------------------------- | ------------- | :----------: |
| **Phase 1**  | Nền tảng & Quản lý BN     | ✅ Hoàn thành |   **100%**   |
| **Phase 2**  | Khám & Tư vấn             | ✅ Hoàn thành |   **100%**   |
| **Phase 3**  | KTBT & Theo dõi           | ✅ Hoàn thành |   **100%**   |
| **Phase 4**  | Thủ thuật (OPU, IUI, IVM) | ✅ Hoàn thành |   **100%**   |
| **Phase 5**  | Phôi học (LABO)           | ✅ Hoàn thành |   **100%**   |
| **Phase 5b** | FET / CBNMTC              | ✅ Hoàn thành |   **100%**   |
| **Phase 6**  | Thai kỳ sớm               | ✅ Hoàn thành |   **100%**   |
| **Phase 7**  | Người cho trứng           | ✅ Hoàn thành |   **100%**   |
| **Phase 8**  | NHTT                      | ✅ Hoàn thành |   **100%**   |
| **Phase 9**  | Kho & Vật tư              | ✅ Hoàn thành |   **100%**   |
| **Phase 10** | Tài chính & Báo cáo       | ✅ Hoàn thành |   **100%**   |

---

## ✅ ĐÃ TRIỂN KHAI TỐT (Không cần thay đổi)

### Hạ tầng & Bảo mật (Vượt kế hoạch)

- ✅ Clean Architecture 4 lớp (Domain → Application → Infrastructure → API)
- ✅ CQRS + MediatR pipeline (Validation, FeatureGate, VaultPolicy, ZeroTrust, FieldAccess)
- ✅ Multi-tenancy với `ITenantEntity` + query filters
- ✅ JWT Auth + Refresh Token + Triple auth pipeline (Vault → ApiKey → JWT)
- ✅ RBAC với `HasRoleDirective`, dynamic permissions, `featureGuard`
- ✅ KeyVault (envelope encryption, auto-unseal, key rotation)
- ✅ WAF (custom rules, event logging, analytics)
- ✅ Zero Trust (conditional access policies)
- ✅ Digital Signing (PKI, SignServer, EJBCA, document signing)
- ✅ Certificate Authority (per-tenant Sub-CA)
- ✅ Enterprise Security (MFA, passkeys, geo-fencing, incident response)
- ✅ SSO/SAML integration
- ✅ Monitoring (Prometheus, Grafana, Loki, Serilog structured logging)
- ✅ Backup/Restore (DB + MinIO + PKI, automated schedules)
- ✅ HIPAA/GDPR Compliance (DSR, processing activities, data retention, consent management)
- ✅ AI Governance (bias testing, model versioning)

### Angular Frontend — Nền tảng

- ✅ Angular 21, standalone components, signals
- ✅ Tailwind CSS v4 + SCSS
- ✅ Auth (login, JWT interceptor, guards)
- ✅ Layout (MainLayoutComponent, sidebar, router-outlet)
- ✅ Dynamic menu (admin-configurable sidebar)
- ✅ Shared components (patient-search, doctor-search, cycle-search, toast, signature-pad)
- ✅ Cookie consent banner (GDPR)
- ✅ Notification bell (SignalR)

### Module Patient — Hoàn chỉnh

- ✅ Backend: CreatePatient, UpdatePatient, AdvancedSearch, Analytics, AuditTrail, FollowUp
- ✅ Backend: Biometrics (photo upload, fingerprint enrollment)
- ✅ Backend: GDPR (data retention, consent)
- ✅ Frontend: patient-list, patient-form, patient-detail, patient-analytics, patient-biometrics, patient-documents, patient-audit-trail

### Module Couple — Hoàn chỉnh

- ✅ Backend: CreateCouple, UpdateCouple, SetSpermDonor
- ✅ Frontend: couple-list, couple-form

### Module Queue — Hoàn chỉnh

- ✅ Backend: IssueTicket, CallTicket, StartService, CompleteTicket, SkipTicket
- ✅ Frontend: queue-display (real-time via SignalR), reception-dashboard
- ✅ SignalR hub `/hubs/queue`

### Module Embryo — Phần lớn có

- ✅ Backend: CreateEmbryo, UpdateEmbryo, GradeEmbryo, TransferEmbryo, FreezeEmbryo, ThawEmbryo
- ✅ Backend: CryoStorage stats, locations

### Module Forms/Reports — Rất mạnh

- ✅ Backend: Full CQRS (categories, templates, fields, responses, reports, amendments, concepts)
- ✅ Frontend: form-builder (drag-drop), form-renderer, conversational forms, report-builder, report-viewer, report-designer, amendment workflow, concept picker

### Module Documents — Rất mạnh

- ✅ Backend: Upload, versioning, metadata, digital signing (PKI), amendments
- ✅ Frontend: patient-documents (per-patient document management)

---

## ✅ ĐÃ TRIỂN KHAI ĐẦY ĐỦ — CHECKLIST CHI TIẾT

### PHASE 1 — Nền tảng & Quản lý BN

| #      | Task                                       | Backend | Frontend | Ghi chú                                                                                                                                 |
| ------ | ------------------------------------------ | :-----: | :------: | --------------------------------------------------------------------------------------------------------------------------------------- |
| P1.07d | Barcode/QR module (generate + scan)        |   ✅    |    ✅    | QR generation cho STT, hóa đơn, toa thuốc; scan component                                                                               |
| P1.10  | STT multi-liên (config số liên theo loại)  |   ✅    |    ✅    | QueueTicket hỗ trợ multi-liên                                                                                                           |
| P1.15  | In phiếu khám HM (PrintService + template) |   ✅    |    ✅    | PrintService + HTML template                                                                                                            |
| P1.16  | SMS/Zalo nhắc lịch                         |   ✅    |    ✅    | SMS gateway integration + Notification entity                                                                                           |
| P1.18  | File tracking hồ sơ giấy                   |   ✅    |    ✅    | `FileTracking` entity + CQRS (Create/Transfer/MarkReceived/MarkLost) + endpoints + `file-tracking.service.ts` + `file-tracking-list` UI |

---

### PHASE 2 — Khám & Tư vấn ✅ Hoàn thành

| #      | Task                                                           | Backend | Frontend | Ghi chú                                                                                                                                                       |
| ------ | -------------------------------------------------------------- | :-----: | :------: | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| P2.01  | SA phụ khoa — form nhập KQ, in KQ, medical audit               |   ✅    |    ✅    | `CreateUltrasoundCommand` + phân loại SA PK vs SA nang noãn. Frontend `ultrasound-dashboard` + `ultrasound-form` + `follicle-chart` + `endometrium-scan-form` |
| P2.02  | Chỉ định XN trên PM (thay giấy) — phân biệt XN tại IVFMD vs BV |   ✅    |    ✅    | `CreateLabOrderCommand` + `lab-orders` component với phân biệt IVFMD vs BV                                                                                    |
| P2.03  | Module XN: tiếp nhận → nhập KQ → trả KQ                        |   ✅    |    ✅    | `CreateLabOrderCommand`, `CollectLabSampleCommand`, `EnterLabResultCommand`, `DeliverLabResultCommand`. Frontend `lab-dashboard` + `lab-orders` hoàn chỉnh    |
| P2.04  | Phân loại XN (thường quy/nội tiết/tiền mê/BetaHCG)             |   ✅    |    ✅    | LabOrder entity + enum phân loại chi tiết (thường quy/nội tiết/tiền mê/BetaHCG)                                                                               |
| P2.05  | Workflow KQ XN (một số loại không trả BN)                      |   ✅    |    ✅    | `DeliverLabResultCommand` + routing logic theo loại XN                                                                                                        |
| P2.06  | Khám tư vấn lần đầu (form bệnh sử, tiền căn)                   |   ✅    |    ✅    | `CreateConsultationCommand`, `StartConsultationCommand`, `CompleteConsultationCommand`. Frontend `consultation-dashboard` + first-visit form + records        |
| P2.07  | Tư vấn sau KQ XN — chọn hướng điều trị                         |   ✅    |    ✅    | Consultation CQRS + logic liên kết XN → tư vấn + treatment-decision UI                                                                                        |
| P2.08  | Tạo chu kỳ điều trị                                            |   ✅    |    ✅    | `CreateCycleCommand` + frontend `cycle-form`                                                                                                                  |
| P2.09  | Module toa thuốc (BS chỉ định → NHS nhập → in)                 |   ✅    |    ✅    | `CreatePrescriptionCommand`, `EnterPrescriptionCommand`, `PrintPrescriptionCommand` + `prescription.service.ts`                                               |
| P2.09b | Template toa thuốc                                             |   ✅    |    ✅    | `PrescriptionTemplate` entity + `PrescriptionTemplateItem` + CQRS + endpoints + `prescription-template.service.ts` + `template-list` UI                       |
| P2.10  | Logic phân luồng (N2 VK / thực hiện / từ chối)                 |   ✅    |    ✅    | Phân luồng N2 VK / thực hiện / từ chối đã có                                                                                                                  |
| P2.10b | Miễn phí tư vấn (waive_consultation_fee)                       |   ✅    |    ✅    | `CycleFee.Waive()` + UI miễn giảm phí                                                                                                                         |
| P2.11  | XN tiền mê + ECG + hẹn khám tiền mê                            |   ✅    |    ✅    | LabOrder type=PreAnesthesia + auto-appointment                                                                                                                |
| P2.12  | Module Nhà thuốc (nhận toa → HĐ thuốc → thu tiền → phát)       |   ✅    |    ✅    | `DispensePrescriptionCommand` + `CancelPrescriptionCommand`. Frontend `pharmacy-dashboard` đầy đủ                                                             |
| P2.13  | Ghi nhận tiêm thuốc (medication_administrations)               |   ✅    |    ✅    | `MedicationAdministration` entity + CQRS + endpoints + `medication-admin.service.ts` + `injection-log` + `trigger-shot-record` UI                             |
| P2.14  | Workflow hoàn chỉnh lần đầu (end-to-end)                       |   ✅    |    ✅    | Backend CQRS đầy đủ (lab, consultation, prescription, pharmacy) + end-to-end orchestration                                                                    |

---

### PHASE 3 — KTBT & Theo dõi ✅ Hoàn thành

| #     | Task                                          | Backend | Frontend | Ghi chú                                                                                                               |
| ----- | --------------------------------------------- | :-----: | :------: | --------------------------------------------------------------------------------------------------------------------- |
| P3.01 | SA nang noãn + medical audit                  |   ✅    |    ✅    | `RecordFolliclesCommand` + `stimulation-tracking` UI + `follicle-chart` component                                     |
| P3.02 | Phiếu theo dõi nang noãn (timeline + biểu đồ) |   ✅    |    ✅    | Biểu đồ nang noãn `follicle-chart` component                                                                          |
| P3.03 | Logic thu phí 1 lần/chu kỳ (cycle_fees)       |   ✅    |    ✅    | `CycleFee` entity (IsOneTimePerCycle, Waive/Refund) + CQRS + endpoints + `cycle-fee.service.ts` + `cycle-fee-list` UI |
| P3.04 | Phân luồng bất thường (IVM/TPTB+HSG/Ngưng)    |   ✅    |    ✅    | Phân luồng IVM/TPTB+HSG/Ngưng đã có                                                                                   |
| P3.05 | Chỉ định thuốc KTBT (template toa)            |   ✅    |    ✅    | `CreatePrescriptionCommand` + `PrescriptionTemplate` + `template-list` UI với template picker                         |
| P3.06 | XN nội tiết theo chu kỳ (E2, P4, LH)          |   ✅    |    ✅    | `CreateLabOrderCommand` + loại XN nội tiết (E2, P4, LH) specific                                                      |
| P3.07 | Hẹn tái khám tự động                          |   ✅    |    ✅    | Appointment CQRS đầy đủ (7 commands, 6 queries) + `appointment-calendar` + `appointment-form` UI                      |
| P3.08 | Logic đánh giá nang đạt/chưa đạt              |   ✅    |    ✅    | `EvaluateFollicleReadiness` command + UI đánh giá                                                                     |
| P3.09 | Tiêm rụng trứng (is_trigger_shot)             |   ✅    |    ✅    | `StimulationData` trigger fields + `trigger-shot-record` UI                                                           |
| P3.10 | Tính giờ chọc hút/IUI (36h) + auto alert      |   ✅    |    ✅    | Tính giờ 36h + auto alert notification                                                                                |
| P3.11 | In phiếu hướng dẫn trước chọc hút             |   ✅    |    ✅    | PrintService + HTML template hướng dẫn                                                                                |
| P3.12 | Phân biệt IVF vs IUI (thuốc hoàng thể)        |   ✅    |    ✅    | Logic phân biệt IVF vs IUI trong CycleType                                                                            |
| P3.13 | SA nang noãn CK tự nhiên (QHTN)               |   ✅    |    ✅    | SA QHTN flow tích hợp vào stimulation tracking                                                                        |

---

### PHASE 4 — Thủ thuật (OPU, IUI, IVM) ✅ Hoàn thành

| #      | Task                                                  | Backend | Frontend | Ghi chú                                                                                                                                 |
| ------ | ----------------------------------------------------- | :-----: | :------: | --------------------------------------------------------------------------------------------------------------------------------------- |
| P4.01  | Checklist trước OPU (block nếu thiếu consent)         |   ✅    |    ✅    | Procedure pre-check checklist + block logic nếu thiếu consent                                                                           |
| P4.01b | Module Consent (tạo, ký, upload scan, tracking)       |   ✅    |    ✅    | `ConsentForm` entity + CQRS (Create/Sign/Revoke/UploadScan) + endpoints + `consent-form.service.ts` + `consent-list/create/detail` UI   |
| P4.02  | Thu phí CH + CP cùng lúc (cycle_fees + logic cấn trừ) |   ✅    |    ✅    | `CycleFee` multi-item + logic cấn trừ                                                                                                   |
| P4.03  | Lấy TT chồng (STT lấy mẫu)                            |   ✅    |    ✅    | STT lấy mẫu chồng tích hợp vào andrology workflow                                                                                       |
| P4.04  | Ghi nhận OPU + IVM_OPU                                |   ✅    |    ✅    | `CreateProcedureCommand`, `StartProcedureCommand`, `CompleteProcedureCommand` + `procedure.service.ts` + `procedure-list/create/opu` UI |
| P4.05  | Theo dõi sau OPU (HA trước/sau)                       |   ✅    |    ✅    | `CompleteProcedureCommand` PostOpNotes + vital signs tracking UI                                                                        |
| P4.06  | Toa thuốc trước 1 ngày (pre-prepare)                  |   ✅    |    ✅    | `CreatePrescriptionCommand` + pre-prepare logic D-1                                                                                     |
| P4.07  | Giao nhận mẫu NHS↔LABO                                |   ✅    |    ✅    | Sample handover workflow NHS↔LABO                                                                                                       |
| P4.08  | Checklist trước IUI + block consent                   |   ✅    |    ✅    | IUI pre-check checklist + consent block                                                                                                 |
| P4.09  | Lấy mẫu + lọc rửa IUI (2h)                            |   ✅    |    ✅    | `CreateSpermWashingCommand` + `andrology-dashboard` với semen-analysis + wash-iui UI                                                    |
| P4.10  | Ghi nhận IUI                                          |   ✅    |    ✅    | `CreateProcedureCommand` IUI + `procedure/iui/:cycleId` UI                                                                              |
| P4.11  | Hẹn thử thai + notification                           |   ✅    |    ✅    | `CreateAppointmentCommand` + auto-schedule pregnancy test + notification                                                                |
| P4.12  | Nhập KQ IUI vào PM                                    |   ✅    |    ✅    | IUI-specific result entry trong cycle phase                                                                                             |
| P4.13  | IVM pathway LABO (nuôi trứng non → ICSI)              |   ✅    |    ✅    | IVM pathway (nuôi trứng non → ICSI) đã có                                                                                               |

---

### PHASE 5 — Phôi học (LABO) ✅ Hoàn thành

| #     | Task                                       | Backend | Frontend | Ghi chú                                                                        |
| ----- | ------------------------------------------ | :-----: | :------: | ------------------------------------------------------------------------------ |
| P5.01 | Theo dõi phôi realtime                     |   ✅    |    ✅    | Backend đầy đủ + dedicated embryology UI trong cycle-detail + realtime updates |
| P5.02 | Màn hình báo phôi cho BN                   |   ✅    |    ✅    | Màn hình thông báo kết quả phôi cho bệnh nhân                                  |
| P5.03 | Quyết định CP tươi / TPTB                  |   ✅    |    ✅    | `TransferEmbryoCommand` + `FreezeEmbryoCommand` + decision workflow UI         |
| P5.04 | Ghi nhận chuyển phôi tươi                  |   ✅    |    ✅    | `TransferEmbryoCommand` + UI ghi nhận CP tươi                                  |
| P5.05 | Hợp đồng trữ phôi                          |   ✅    |    ✅    | Embryo storage contract + fee logic                                            |
| P5.06 | Logic tính top (N3: 1-3, N5&6: 1-2, ĐB: 1) |   ✅    |    ✅    | Business logic tính số phôi chuyển theo ngày                                   |
| P5.07 | Quản lý kho trữ phôi (tank, vị trí)        |   ✅    |    ✅    | `CryoLocation` entity + commands + cryo storage UI                             |
| P5.08 | Phiếu trữ phôi (in)                        |   ✅    |    ✅    | PrintService + phiếu trữ phôi HTML template                                    |
| P5.09 | SA niêm mạc trước CP tươi                  |   ✅    |    ✅    | SA niêm mạc pre-transfer tích hợp vào ultrasound workflow                      |
| P5.10 | Logic hoàn tiền CP → cấn trừ trữ phôi      |   ✅    |    ✅    | `RefundPaymentCommand` + cấn trừ phí trữ phôi                                  |

---

### PHASE 5b — FET / CBNMTC ✅ Hoàn thành

| #      | Task                                     | Backend | Frontend | Ghi chú                                                                                            |
| ------ | ---------------------------------------- | :-----: | :------: | -------------------------------------------------------------------------------------------------- |
| P5b.01 | Tạo chu kỳ FET (endometrium_prep_cycles) |   ✅    |    ✅    | `FetProtocol` entity + `CreateFetProtocolCommand` + `fet.service.ts` + `fet/create` + `fet/:id` UI |
| P5b.02 | Flow tiếp nhận N2 VK                     |   ✅    |    ✅    | Flow tiếp nhận N2 VK tích hợp vào FET workflow                                                     |
| P5b.03 | SA phụ khoa cho FET (2 BS)               |   ✅    |    ✅    | SA phụ khoa 2 BS tích hợp vào ultrasound + FET flow                                                |
| P5b.04 | BS tư vấn + thuốc CBNMTC                 |   ✅    |    ✅    | `UpdateHormoneTherapyCommand` + `fet/:id/hormone-therapy` UI                                       |
| P5b.05 | Logic phí cycle_fees SA NMTC             |   ✅    |    ✅    | `CycleFee` entity + CQRS + `cycle-fee-list` UI tích hợp FET flow                                   |
| P5b.06 | SA theo dõi NMTC (1 BS)                  |   ✅    |    ✅    | SA NMTC tracking tích hợp vào `endometrium-scan-form`                                              |
| P5b.07 | Biểu đồ độ dày NMTC                      |   ✅    |    ✅    | Biểu đồ độ dày NMTC tích hợp vào `follicle-chart` / `endometrium-scan`                             |
| P5b.08 | Điều chỉnh thuốc CBNMTC                  |   ✅    |    ✅    | `UpdateHormoneTherapyCommand` + adjust-specific UI                                                 |
| P5b.09 | NM đạt → lên lịch CP trữ                 |   ✅    |    ✅    | `ScheduleTransferCommand` + `MarkFetTransferredCommand` + `fet/:id/transfer` UI                    |
| P5b.10 | Ghi nhận chuyển phôi trữ                 |   ✅    |    ✅    | `MarkFetTransferredCommand` + UI ghi nhận CP trữ                                                   |
| P5b.11 | Hẹn thử thai + notification              |   ✅    |    ✅    | `CreateAppointmentCommand` + auto-schedule + notification                                          |
| P5b.12 | Alert hết hạn trữ phôi                   |   ✅    |    ✅    | Alert hết hạn trữ phôi qua Notification service                                                    |

---

### PHASE 6 — Thai kỳ sớm ✅ Hoàn thành

| #     | Task                                           | Backend | Frontend | Ghi chú                                                                                                  |
| ----- | ---------------------------------------------- | :-----: | :------: | -------------------------------------------------------------------------------------------------------- |
| P6.01 | Flow riêng thử thai (skip tiếp đón IVFMD)      |   ✅    |    ✅    | Flow thử thai riêng (bypass tiếp đón) tích hợp vào pregnancy module                                      |
| P6.02 | Chỉ BS thông báo KQ BetaHCG (permission check) |   ✅    |    ✅    | `PregnancyData.BetaHcg` + `UpdatePregnancyDataCommand` + RBAC permission check Doctor-only               |
| P6.03 | Phân luồng Dương/Âm + notification             |   ✅    |    ✅    | `pregnancy/result/:cycleId` UI + Dương/Âm routing + notification                                         |
| P6.04 | Khám thai 7 tuần (SA thai + toa 4 tuần)        |   ✅    |    ✅    | `PregnancyData` + `CreatePrescriptionCommand` + `pregnancy/prenatal-7w/:cycleId` UI                      |
| P6.05 | Hẹn tái khám thai (2w/1 thai, 1w/2 thai)       |   ✅    |    ✅    | `CreateAppointmentCommand` + auto-schedule logic theo loại thai (đơn/đôi)                                |
| P6.06 | Phát sổ khám thai                              |   ✅    |    ✅    | Sổ khám thai document generation + PrintService                                                          |
| P6.07 | Đóng chu kỳ IVF → chuyển QT thai BV            |   ✅    |    ✅    | `CompleteCycleCommand` + `UpdateBirthDataCommand` + discharge/transfer UI `pregnancy/discharge/:cycleId` |

---

### PHASE 7 — Người cho trứng ✅ Hoàn thành

| #     | Task                                       | Backend | Frontend | Ghi chú                                                                                                                                                             |
| ----- | ------------------------------------------ | :-----: | :------: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| P7.01 | Đăng ký NCT (ảnh + vân tay + check đã cho) |   ✅    |    ✅    | `EggDonor` entity + `CreateEggDonorCommand` + `UpdateEggDonorProfileCommand` + `egg-bank.service.ts` + `egg-donor/register` + `egg-donor/list` UI                   |
| P7.02 | SA phụ khoa NCT                            |   ✅    |    ✅    | `CreateUltrasoundCommand` + NCT-specific SA flow trong `egg-donor/:id`                                                                                              |
| P7.03 | XN sàng lọc NCT                            |   ✅    |    ✅    | `CreateLabOrderCommand` + screening workflow NCT tích hợp                                                                                                           |
| P7.04 | Tư vấn NCT + consent cho trứng             |   ✅    |    ✅    | `CreateConsultationCommand` + `ConsentForm` + consent UI flow tích hợp vào `egg-donor/:id`                                                                          |
| P7.05 | Gắn NCT với 2 cặp VC trên PM               |   ✅    |    ✅    | `EggDonorRecipient` entity + CQRS (Match/LinkToCycle/Complete/Cancel) + endpoints + `egg-donor-recipient.service.ts` + `egg-donor/matching` UI                      |
| P7.06 | XN tiền mê + ECG + SA nhũ                  |   ✅    |    ✅    | XN tiền mê + ECG + SA nhũ tích hợp vào NCT workflow                                                                                                                 |
| P7.07 | KTBT cho NCT                               |   ✅    |    ✅    | `OocyteSample` entity + `CreateOocyteSampleCommand` + `RecordOocyteQualityCommand` + `VitrifyOocytesCommand` + `egg-donor/:id/samples` + `egg-donor/:id/vitrify` UI |

---

### PHASE 8 — NHTT ✅ Hoàn thành

| #     | Task                                      | Backend | Frontend | Ghi chú                                                                      |
| ----- | ----------------------------------------- | :-----: | :------: | ---------------------------------------------------------------------------- |
| P8.01 | Tư vấn xin TT + giấy giới thiệu           |   ✅    |    ✅    | Consultation CQRS + giấy giới thiệu template                                 |
| P8.02 | XN sàng lọc NH                            |   ✅    |    ✅    | `SpermDonor` entity + screening workflow + `sperm-bank/screening` UI         |
| P8.03 | Sinh trắc NH (ảnh + vân tay + so khớp)    |   ✅    |    ✅    | Backend biometric commands + `sperm-bank-dashboard` với biometric enrollment |
| P8.04 | Báo KQ HIV nhanh (15 phút)                |   ✅    |    ✅    | HIV rapid test result flow + notification 15 phút                            |
| P8.05 | Quyết định đủ chuẩn / tư vấn tìm NH khác  |   ✅    |    ✅    | Approval/rejection workflow trong `sperm-bank/approve` UI                    |
| P8.06 | Lấy mẫu lần 1 + consent cam kết tham gia  |   ✅    |    ✅    | `CreateSampleCommand` + `ConsentForm` + `sperm-bank/sample-1` UI             |
| P8.07 | Lấy mẫu lần 2 + hẹn HIV lần 2 sau 3 tháng |   ✅    |    ✅    | Sample-2 flow + auto-appointment HIV retest sau 3 tháng                      |
| P8.08 | XN HIV lần 2 + xác minh sinh trắc lại     |   ✅    |    ✅    | HIV retest + biometric re-verification trong `sperm-bank/hiv-retest`         |
| P8.09 | Cấp mã NHTT + phiếu NHTT                  |   ✅    |    ✅    | Donor code generation + phiếu NHTT PrintService                              |
| P8.10 | Thu tiền sử dụng mẫu                      |   ✅    |    ✅    | `CycleFee` + `RecordPaymentCommand` + billing integration                    |
| P8.11 | Hoán đổi & lấy mẫu NHTT (LABO)            |   ✅    |    ✅    | Sample swap workflow LABO tích hợp vào `sperm-bank/inventory`                |
| P8.12 | Theo dõi mẫu + alert 3 tháng HIV retest   |   ✅    |    ✅    | Alert tự động 3 tháng HIV retest qua Notification service                    |
| P8.13 | Logic hủy mẫu                             |   ✅    |    ✅    | Sample cancellation logic + status tracking                                  |

---

### PHASE 9 — Kho & Vật tư ✅ Hoàn thành

| #     | Task                               | Backend | Frontend | Ghi chú                                                                                                                                      |
| ----- | ---------------------------------- | :-----: | :------: | -------------------------------------------------------------------------------------------------------------------------------------------- |
| P9.01 | Sổ bàn giao thuốc (KTV gây mê)     |   ✅    |    ✅    | `InventoryItem` entity + `RecordUsageCommand` + `inventory.service.ts` + dedicated handover UI                                               |
| P9.02 | Kiểm tồn kho 2 lần/ngày            |   ✅    |    ✅    | `GetLowStockAlertsQuery` + `SearchInventoryItemsQuery` + `inventory/alerts` scheduled check UI                                               |
| P9.03 | Phiếu bù tủ trực → Khoa Dược duyệt |   ✅    |    ✅    | `InventoryRequest` entity + CQRS (Create/Approve/Reject/Fulfill) + endpoints + `inventory-request.service.ts` + approval UI                  |
| P9.04 | Kiểm kho VTTH 3-5 ngày             |   ✅    |    ✅    | `GetExpiringItemsQuery` + `GetLowStockAlertsQuery` + expiry tracking UI                                                                      |
| P9.05 | Phiếu hao phí → duyệt              |   ✅    |    ✅    | `InventoryRequest` (type=Usage) + approval workflow + `inventory/usage` UI                                                                   |
| P9.06 | Đặt mua VTTH                       |   ✅    |    ✅    | `InventoryRequest` (type=PurchaseOrder) + `inventory/stock` purchase order UI                                                                |
| P9.07 | Nhập/xuất kho PM                   |   ✅    |    ✅    | `ImportStockCommand` + `RecordUsageCommand` + `AdjustStockCommand` + `GetStockTransactionsQuery` + `inventory/import` + `inventory/usage` UI |

---

### PHASE 10 — Tài chính & Báo cáo ✅ Hoàn thành

| #       | Task                                       | Backend | Frontend | Ghi chú                                                                                                               |
| ------- | ------------------------------------------ | :-----: | :------: | --------------------------------------------------------------------------------------------------------------------- |
| P10.01  | Hóa đơn tổng hợp (nhiều DV)                |   ✅    |    ✅    | `CreateInvoiceCommand` + `AddInvoiceItemCommand` + `invoice-list` + create/payment/history UI                         |
| P10.02  | Thu tiền đa hình thức + kế toán approve CK |   ✅    |    ✅    | `RecordPaymentCommand` + approval workflow CK + billing UI                                                            |
| P10.03  | In hóa đơn 2 liên + QR                     |   ✅    |    ✅    | PrintService HTML template 2 liên + QR code generation                                                                |
| P10.04  | Hoàn tiền / cấn trừ                        |   ✅    |    ✅    | `VoidInvoiceCommand` + `RefundPaymentCommand` + endpoints `/invoices/{id}/void` + `/invoices/{id}/refund` + refund UI |
| P10.05  | Bảng giá dịch vụ                           |   ✅    |    ✅    | `ServiceCatalog` + `Pricing` features + clinical service pricing hoàn chỉnh                                           |
| P10.06  | Thu phí đặc biệt (IVFMD vs BV)             |   ✅    |    ✅    | Thu phí phân biệt IVFMD vs BV trong billing flow                                                                      |
| P10.06b | Tích hợp HIS BV                            |   ✅    |    ✅    | HIS integration API endpoints                                                                                         |
| P10.07  | Dashboard tổng quan (charts)               |   ✅    |    ✅    | `GetDashboardStatsQuery` + frontend `dashboard` với charts                                                            |
| P10.08  | BC tài chính                               |   ✅    |    ✅    | `GetMonthlyRevenueQuery` + `reports/financial` UI hoàn chỉnh                                                          |
| P10.09  | BC y khoa                                  |   ✅    |    ✅    | `GetCycleSuccessRatesQuery` + `reports/clinical` UI hoàn chỉnh                                                        |
| P10.10  | BC NHTT                                    |   ✅    |    ✅    | Sperm bank report + `reports/sperm-bank-report` UI                                                                    |
| P10.11  | BC kho                                     |   ✅    |    ✅    | `GetLowStockAlertsQuery` + `GetExpiringItemsQuery` + `GetStockTransactionsQuery` + `reports/inventory-report` UI      |
| P10.12  | Export PDF/Excel                           |   ✅    |    ✅    | Export PDF/Excel từ mọi báo cáo (PrintService + XLSX generation)                                                      |

---

## ✅ ĐÃ HOÀN THÀNH TOÀN BỘ — TẤT CẢ ĐÃ TRIỂN KHAI

### Backend — Domain Entities cần tạo mới

| #     | Entity                         | Bảng DB                          | Mô tả                                                                                      |
| ----- | ------------------------------ | -------------------------------- | ------------------------------------------------------------------------------------------ |
| ~~1~~ | ~~`ConsentForm`~~              | ~~`consent_forms`~~              | ✅ ĐÃ XONG — entity + CQRS + endpoints + frontend service                                  |
| ~~2~~ | ~~`MedicationAdministration`~~ | ~~`medication_administrations`~~ | ✅ ĐÃ XONG — entity + CQRS + endpoints + frontend service                                  |
| ~~3~~ | ~~`CycleFee`~~                 | ~~`cycle_fees`~~                 | ✅ ĐÃ XONG — entity + CQRS + endpoints + frontend service                                  |
| ~~4~~ | ~~`PrescriptionTemplate`~~     | ~~`prescription_templates`~~     | ✅ ĐÃ XONG — entity + child PrescriptionTemplateItem + CQRS + endpoints + frontend service |
| ~~5~~ | ~~`FileTracking`~~             | ~~`file_tracking`~~              | ✅ ĐÃ XONG — entity + child FileTransfer + CQRS + endpoints + frontend service             |
| ~~6~~ | ~~`EggDonorRecipient`~~        | ~~`egg_donor_recipients`~~       | ✅ ĐÃ XONG — entity + CQRS + endpoints + frontend service                                  |
| ~~7~~ | ~~`InventoryRequest`~~         | ~~`inventory_requests`~~         | ✅ ĐÃ XONG — entity + approval workflow CQRS + endpoints + frontend service                |
| ~~8~~ | ~~`DrugCatalog`~~              | ~~`drug_catalog`~~               | ✅ ĐÃ XONG — entity + CQRS + endpoints + frontend service                                  |

> **Tất cả 8 entities đã triển khai** (19/03/2026). Không còn entity nào còn thiếu trong danh sách ban đầu.

### Backend — CQRS Features cần tạo mới

| #      | Feature Folder              | Commands | Queries                                                                                              |
| ------ | --------------------------- | -------- | ---------------------------------------------------------------------------------------------------- |
| ~~1~~  | ~~`Prescriptions/`~~        | ✅ ĐÃ CÓ | ✅ 6 Commands + 5 Queries                                                                            |
| ~~2~~  | ~~`Pharmacy/`~~             | ✅ ĐÃ CÓ | ✅ Tích hợp trong Prescriptions (Dispense, Cancel)                                                   |
| ~~3~~  | ~~`Appointments/`~~         | ✅ ĐÃ CÓ | ✅ 7 Commands + 6 Queries                                                                            |
| ~~4~~  | ~~`Consultations/`~~        | ✅ ĐÃ CÓ | ✅ 4 Commands + 4 Queries                                                                            |
| ~~5~~  | ~~`Stimulation/`~~          | ✅ ĐÃ CÓ | ✅ RecordFollicleScan, RecordTriggerShot, RecordMedicationLog, EvaluateFollicleReadiness + 3 Queries |
| ~~6~~  | ~~`Procedures/`~~           | ✅ ĐÃ CÓ | ✅ 5 Commands + 5 Queries                                                                            |
| ~~7~~  | ~~`FET/`~~                  | ✅ ĐÃ CÓ | ✅ 5 Commands + 3 Queries (thay EndometriumPrep)                                                     |
| ~~8~~  | ~~`Pregnancy/`~~            | ✅ ĐÃ CÓ | ✅ RecordBetaHCG, NotifyBetaHCGResult, RecordPrenatalExam, DischargeCycle + 3 Queries                |
| ~~9~~  | ~~`EggBank/`~~              | ✅ ĐÃ CÓ | ✅ 5 Commands + 4 Queries                                                                            |
| ~~10~~ | ~~`Consent/`~~              | ✅ ĐÃ CÓ | ✅ CreateConsentForm + SignConsent + RevokeConsent + UploadScan + 3 Queries                          |
| ~~11~~ | ~~`Inventory/`~~            | ✅ ĐÃ CÓ | ✅ 5 Commands + 5 Queries                                                                            |
| ~~12~~ | ~~`MedicationAdmin/`~~      | ✅ ĐÃ CÓ | ✅ RecordAdministration + MarkSkipped + MarkRefused + 3 Queries                                      |
| ~~13~~ | ~~`CycleFees/`~~            | ✅ ĐÃ CÓ | ✅ CreateCycleFee + WaiveCycleFee + RefundCycleFee + 3 Queries                                       |
| ~~14~~ | ~~`FileTracking/`~~         | ✅ ĐÃ CÓ | ✅ CreateFileTracking + TransferFile + MarkReceived + MarkLost + 4 Queries                           |
| ~~15~~ | ~~`Notifications/` (CQRS)~~ | ✅ ĐÃ CÓ | ✅ SendNotification + BroadcastNotification + MarkRead + MarkAllRead + 3 Queries                     |

### Frontend — Feature Modules ✅ Tất cả hoàn thành

| #     | Module             | Folder                      | Số Components | Routes                                                                                                                                          |
| ----- | ------------------ | --------------------------- | :-----------: | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| ~~1~~ | ~~`stimulation/`~~ | ~~`features/stimulation/`~~ |     ✅ 4      | `/stimulation/:cycleId`, `/stimulation/follicle-scan`, `/stimulation/trigger-decision`, `/stimulation/medication-log`                           |
| ~~2~~ | ~~`fet/`~~         | ~~`features/fet/`~~         |     ✅ 4      | `/fet/create`, `/fet/:id`, `/fet/:id/hormone-therapy`, `/fet/:id/transfer` — ✅ Backend CQRS + service + UI                                     |
| ~~3~~ | ~~`pregnancy/`~~   | ~~`features/pregnancy/`~~   |     ✅ 4      | `/pregnancy/beta-hcg/:cycleId`, `/pregnancy/result/:cycleId`, `/pregnancy/prenatal-7w/:cycleId`, `/pregnancy/discharge/:cycleId`                |
| ~~4~~ | ~~`egg-donor/`~~   | ~~`features/egg-donor/`~~   |     ✅ 5      | `/egg-donor/register`, `/egg-donor/list`, `/egg-donor/:id`, `/egg-donor/:id/samples`, `/egg-donor/:id/vitrify` — ✅ Backend CQRS + service + UI |
| ~~5~~ | ~~`inventory/`~~   | ~~`features/inventory/`~~   |     ✅ 4      | `/inventory/stock`, `/inventory/import`, `/inventory/usage`, `/inventory/alerts` — ✅ Backend CQRS + service + UI                               |
| ~~6~~ | ~~`procedure/`~~   | ~~`features/procedure/`~~   |     ✅ 5      | `/procedure/list`, `/procedure/create`, `/procedure/:id`, `/procedure/opu/:cycleId`, `/procedure/iui/:cycleId` — ✅ Backend CQRS + service + UI |
| ~~7~~ | ~~`consent/`~~     | ~~`features/consent/`~~     |     ✅ 3      | `/consent/list`, `/consent/create`, `/consent/:id` — ✅ Backend CQRS + `consent-form.service.ts` + UI                                           |

---

## ✅ FRONTEND SHELLS — ĐÃ HOÀN THIỆN

Tất cả feature folder đã được xây dựng đầy đủ:

| #   | Feature         | Trạng thái    | Components                                                                                                    |
| --- | --------------- | ------------- | ------------------------------------------------------------------------------------------------------------- |
| 1   | `andrology/`    | ✅ Hoàn thành | `andrology-dashboard` + semen-analysis form + wash-iui + wash-icsi + result detail                            |
| 2   | `lab/`          | ✅ Hoàn thành | `lab-dashboard` + `lab-orders` + result-entry detail + result-view                                            |
| 3   | `consultation/` | ✅ Hoàn thành | `consultation-dashboard` + first-visit form + follow-up + treatment-decision                                  |
| 4   | `pharmacy/`     | ✅ Hoàn thành | `pharmacy-dashboard` + pending queue + dispense detail                                                        |
| 5   | `injection/`    | ✅ Hoàn thành | `injection-dashboard` + `injection-log` + `trigger-shot-record`                                               |
| 6   | `billing/`      | ✅ Hoàn thành | `invoice-list` + create + payment + refund + history + price-list                                             |
| 7   | `sperm-bank/`   | ✅ Hoàn thành | `sperm-bank-dashboard` + screening + sample-1 + sample-2 + hiv-retest + approve + inventory + usage + destroy |
| 8   | `reports/`      | ✅ Hoàn thành | `reports-dashboard` + `financial-report` + `clinical-report` + `inventory-report`                             |
| 9   | `appointments/` | ✅ Hoàn thành | `appointments-dashboard` + `appointment-calendar` + `appointment-form`                                        |
| 10  | `ultrasounds/`  | ✅ Hoàn thành | `ultrasound-dashboard` + `ultrasound-form` + `follicle-chart` + `endometrium-scan-form`                       |

---

## 🟢 ĐÃ CÓ NHƯNG VƯỢT KẾ HOẠCH (Bonus)

Hệ thống đã triển khai nhiều tính năng **không có trong kế hoạch gốc** nhưng rất có giá trị:

| #   | Tính năng                                 | Ghi chú                                         |
| --- | ----------------------------------------- | ----------------------------------------------- |
| 1   | Dynamic Form Builder (21 sub-components)  | Tạo biểu mẫu y khoa linh hoạt mà không cần code |
| 2   | Digital Signing (PKI, SignServer, EJBCA)  | Ký số tài liệu theo chuẩn                       |
| 3   | Certificate Authority (per-tenant Sub-CA) | Mỗi tenant có CA riêng                          |
| 4   | Zero Trust Security                       | Conditional access policies                     |
| 5   | WAF (Web Application Firewall)            | Custom rules, event logging                     |
| 6   | KeyVault (envelope encryption)            | Quản lý khóa bảo mật                            |
| 7   | Enterprise User Management                | Groups, sessions, IAM, login analytics          |
| 8   | SSO/SAML Integration                      | Đăng nhập tập trung                             |
| 9   | AI Governance                             | Bias testing, model versioning                  |
| 10  | Infrastructure Monitoring (real-time)     | VPS metrics qua SignalR                         |
| 11  | Compliance (HIPAA/GDPR)                   | DSR, processing activities, training, evidence  |
| 12  | DNS/Domain Management                     | Cloudflare DNS + custom domains                 |
| 13  | Backup/Restore (automated)                | Lịch backup, system restore                     |
| 14  | Form Amendments                           | Sửa đổi biểu mẫu có workflow approval           |
| 15  | Medical Concepts (SNOMED/ICD)             | Concept mapping cho form fields                 |
| 16  | Conversational Forms                      | Fill form kiểu chat                             |

---

## 📌 TỔNG KẾT — TẤT CẢ PHASES ĐÃ HOÀN THÀNH ✅

Dựa trên tần suất sử dụng tại phòng khám IVF và dependencies — **tất cả đã được triển khai**:

### ✅ Tất cả đã hoàn thành — Không còn hạng mục nào cần triển khai

1. ~~**Prescription/Pharmacy CQRS**~~ (P2.09, P2.12) ✅ — 6 commands, 5 queries, 10 endpoints
2. ~~**Lab Orders CQRS**~~ (P2.02-P2.05) ✅ — 4 commands, 5 queries, 8 endpoints + lab-orders UI
3. ~~**Consultation CQRS**~~ (P2.06-P2.07) ✅ — 4 commands, 4 queries, 9 endpoints
4. ~~**Appointment CQRS**~~ (P3.07) ✅ — 7 commands, 6 queries, IMediator refactor
5. ~~**FET Protocol**~~ (P5b.01-P5b.12) ✅ — 5 commands, 3 queries, FetProtocol entity
6. ~~**Procedure Feature**~~ (P4.04-P4.12) ✅ — 5 commands, 5 queries, 10 endpoints
7. ~~**Egg Donor/Egg Bank**~~ (P7.01-P7.07) ✅ — 5 commands, 4 queries, 9 endpoints
8. ~~**Inventory**~~ (P9.01-P9.07) ✅ — 5 commands, 5 queries, 9 endpoints
9. ~~**Consent Module**~~ (P4.01b) ✅ — ConsentForm entity + 4 commands + 3 queries + endpoints + `consent-form.service.ts` + UI
10. ~~**MedicationAdmin**~~ (P2.13) ✅ — MedicationAdministration entity + 3 commands + 3 queries + endpoints + `medication-admin.service.ts` + UI
11. ~~**CycleFee logic**~~ (P3.03) ✅ — CycleFee entity + Waive/Refund + 3 queries + endpoints + `cycle-fee.service.ts` + `cycle-fee-list` UI
12. ~~**DrugCatalog**~~ ✅ — entity + Create/Update/ToggleActive + 4 queries + endpoints + `drug-catalog.service.ts` + `drug-catalog-list` UI
13. ~~**PrescriptionTemplate**~~ (P2.09b) ✅ — entity + child items + CQRS + endpoints + `prescription-template.service.ts` + `template-list` UI
14. ~~**FileTracking**~~ (P1.18) ✅ — entity + child FileTransfer + 4 commands + 4 queries + endpoints + `file-tracking.service.ts` + `file-tracking-list` UI
15. ~~**EggDonorRecipient**~~ (P7.05) ✅ — entity + Match/LinkToCycle/Complete/Cancel + endpoints + `egg-donor-recipient.service.ts`
16. ~~**InventoryRequest**~~ (P9.03) ✅ — entity + Create/Approve/Reject/Fulfill + endpoints + `inventory-request.service.ts`
17. ~~**Billing VoidInvoice + RefundPayment**~~ (P10.04) ✅ — VoidInvoiceCommand + RefundPaymentCommand + endpoints + refund UI
18. ~~**Notifications CQRS**~~ ✅ — NotificationCommands.cs + NotificationQueries.cs (MediatR pipeline)
19. ~~**Stimulation UI**~~ ✅ — `stimulation-tracking` + `follicle-chart` + `trigger-shot-record` + `medication-log`
20. ~~**Consent UI**~~ ✅ — `consent-list` + `consent-create` + `consent-detail` components
21. ~~**Pregnancy dedicated flow**~~ ✅ — `pregnancy/beta-hcg` + `pregnancy/result` + `pregnancy/prenatal-7w` + `pregnancy/discharge`
22. ~~**Injection UI**~~ ✅ — `injection-log` + `trigger-shot-record` routes added
23. ~~**FET UI**~~ ✅ — `fet/create` + `fet/:id` + `fet/:id/hormone-therapy` + `fet/:id/transfer`
24. ~~**Procedure UI**~~ ✅ — `procedure/list` + `procedure/opu/:cycleId` + `procedure/iui/:cycleId`
25. ~~**Egg Donor UI**~~ ✅ — `egg-donor/register` + `egg-donor/list` + `egg-donor/:id` + samples + vitrify
26. ~~**Inventory UI**~~ ✅ — `inventory/stock` + `inventory/import` + `inventory/usage` + `inventory/alerts`
27. ~~**Sperm Bank workflows**~~ ✅ — Toàn bộ P8 flow (screening → sample → HIV → approve → code)
28. ~~**Reports expansion**~~ ✅ — `reports/financial` + `reports/clinical` + `reports/inventory-report`
29. ~~**Billing UI**~~ ✅ — create + payment + refund + history + price-list

| Hạng mục                             | Kế hoạch |      Đã có       | Thiếu |     %     |
| ------------------------------------ | :------: | :--------------: | :---: | :-------: |
| **Domain Entities**                  |   ~35    | 36+ (122 files)  |   0   | **100%**  |
| **CQRS Feature Folders**             |   ~40    |        40        |   0   | **100%**  |
| **API Endpoints**                    |   ~110   | ~140+ (78 files) |   —   | **100%+** |
| **Frontend Services**                |   ~40    |       64+        |   —   | **100%+** |
| **Frontend Feature Modules**         |   ~30    |        30        |   0   | **100%**  |
| **Frontend Components (hoàn chỉnh)** |   ~70    |       70+        |   0   | **100%**  |
| **Frontend Shells (hoàn thiện)**     |    10    |        10        |   0   | **100%**  |

> **Kết luận**: ✅ **Hệ thống IVF đã hoàn thành 100%** — Backend (entities + CQRS + EF config + DI + endpoints + frontend services) và Frontend (UI components + routes + clinical workflows) đều đạt 100%. Tất cả 11 phases của `IVFMD_Implementation_Plan.md` đã được triển khai đầy đủ tính đến 19/03/2026.
