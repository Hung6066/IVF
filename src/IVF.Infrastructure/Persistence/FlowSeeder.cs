using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

public class FlowSeeder : IFlowSeeder
{
    private readonly IvfDbContext _context;
    private Guid _adminUserId;
    private Guid _doctorUserId;

    public FlowSeeder(IvfDbContext context)
    {
        _context = context;
    }

    public async Task SeedFlowDataAsync(CancellationToken cancellationToken = default)
    {
        // Resolve existing users for FK references
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        var doctor = await _context.Users.FirstOrDefaultAsync(u => u.Username == "doctor1");
        _adminUserId = admin?.Id ?? Guid.Empty;
        _doctorUserId = doctor?.Id ?? _adminUserId;

        // Seed service catalog & inventory first
        await SeedServicesAsync();
        await SeedInventoryAsync();

        // ═══════════════════════════════════════════════════════════════
        // FLOW 1 — KHÁM LẦN ĐẦU (First Visit)
        // New patient arrives → reception queue → first consultation
        // ═══════════════════════════════════════════════════════════════
        var p1 = await CreatePatientAsync("BN-TEST-001", "Nguyễn Thị Tư Vấn", 1995, Gender.Female, "0901000001");
        var p1h = await CreatePatientAsync("BN-TEST-002", "Trần Văn Chồng Tư Vấn", 1990, Gender.Male, "0901000002");
        var c1 = await CreateCoupleAsync(p1, p1h);
        var cy1 = await CreateCycleAsync(c1, "CK-TEST-01", TreatmentMethod.ICSI, CyclePhase.Consultation);

        // Queue ticket for reception
        await CreateQueueTicketAsync("Q-TEST-001", QueueType.Reception, p1.Id, "RECEPTION", cy1.Id);
        // First consultation
        await CreateConsultationAsync(p1.Id, "FirstVisit", cy1.Id, "Vô sinh nguyên phát, kết hôn 3 năm chưa có thai");
        // Consent form
        await CreateConsentFormAsync(p1.Id, "IVF_GENERAL", "Đồng ý điều trị IVF", cy1.Id);
        // Appointment for follow-up
        await CreateAppointmentAsync(p1.Id, AppointmentType.Consultation, cy1.Id, 2, "Tái khám sau xét nghiệm");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 2 — TƯ VẤN SAU XÉT NGHIỆM (Post-Lab Consultation)
        // Patient has lab results → doctor reviews → creates treatment plan
        // ═══════════════════════════════════════════════════════════════
        var p2 = await CreatePatientAsync("BN-TEST-003", "Phạm Thị Kích Thích", 1992, Gender.Female, "0901000003");
        var p2h = await CreatePatientAsync("BN-TEST-004", "Lê Văn Chồng KT", 1988, Gender.Male, "0901000004");
        var c2 = await CreateCoupleAsync(p2, p2h);
        var cy2 = await CreateCycleAsync(c2, "CK-TEST-02", TreatmentMethod.ICSI, CyclePhase.OvarianStimulation);

        if (!await _context.TreatmentIndications.AnyAsync(x => x.CycleId == cy2.Id))
            _context.TreatmentIndications.Add(TreatmentIndication.Create(cy2.Id));

        // Lab orders with results
        await CreateLabOrderAsync(p2.Id, "HORMONE_PANEL", cy2.Id, "XN hormone cơ bản");
        await CreateLabOrderAsync(p2h.Id, "SEMEN_ANALYSIS", cy2.Id, "Tinh dịch đồ");
        // Consultation after lab
        await CreateConsultationAsync(p2.Id, "FollowUp", cy2.Id, "FSH=6.5, AMH=3.2, AFC=12 → chỉ định ICSI");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 3 — KÍCH THÍCH BUỒNG TRỨNG (Ovarian Stimulation)
        // Patient in stimulation Day 5 → drugs + follicle monitoring
        // ═══════════════════════════════════════════════════════════════
        if (!await _context.StimulationData.AnyAsync(x => x.CycleId == cy2.Id))
        {
            var stim2 = StimulationData.Create(cy2.Id);
            stim2.Update(DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-5), 5,
                3, 1, 6.5m,
                null, null, null, null, null, null, null, null, null,
                null, null, null, 0, null, null);
            stim2.SetDrugs(new[] { StimulationDrug.Create(stim2.Id, 0, "Gonal F", 5, "150IU") });
            _context.StimulationData.Add(stim2);
        }
        // Ultrasound for follicle monitoring
        await CreateUltrasoundAsync(cy2.Id, "FollicleMonitoring");
        // Prescription for stim drugs
        await CreatePrescriptionAsync(p2.Id, cy2.Id, "Thuốc KTBT ngày 1-5");
        // Queue ticket for ultrasound
        await CreateQueueTicketAsync("Q-TEST-002", QueueType.Ultrasound, p2.Id, "ULTRASOUND", cy2.Id);

        // ═══════════════════════════════════════════════════════════════
        // FLOW 4 — TIÊM RỤNG TRỨNG (Trigger Shot)
        // Follicles ready → trigger injection scheduled
        // ═══════════════════════════════════════════════════════════════
        var p3 = await CreatePatientAsync("BN-TEST-005", "Hoàng Thị Trigger", 1993, Gender.Female, "0901000005");
        var p3h = await CreatePatientAsync("BN-TEST-006", "Ngô Văn Chồng Trigger", 1985, Gender.Male, "0901000006");
        var c3 = await CreateCoupleAsync(p3, p3h);
        var cy3 = await CreateCycleAsync(c3, "CK-TEST-03", TreatmentMethod.ICSI, CyclePhase.TriggerShot);

        if (!await _context.StimulationData.AnyAsync(x => x.CycleId == cy3.Id))
        {
            var stim3 = StimulationData.Create(cy3.Id);
            stim3.Update(DateTime.UtcNow.AddDays(-25), DateTime.UtcNow.AddDays(-10), 10,
                10, 8, 10.5m,
                null, null, null, null, null, null, null, null, null,
                null, null, null, 0, null, null);
            stim3.SetDrugs(new[]
            {
                StimulationDrug.Create(stim3.Id, 0, "Gonal F", 10, "150IU"),
                StimulationDrug.Create(stim3.Id, 1, "Cetrotide", 3, "0.25mg")
            });
            _context.StimulationData.Add(stim3);
        }
        // Medication administration: trigger shot
        await CreateMedicationAdminAsync(p3.Id, cy3.Id, "Ovitrelle 250mcg", "250mcg", "SC", true);
        // Appointment for OPU 36h after trigger
        await CreateAppointmentAsync(p3.Id, AppointmentType.EggRetrieval, cy3.Id, 2, "Chọc hút sau trigger 36h");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 5 — CHỌC HÚT TRỨNG (OPU / Egg Retrieval)
        // Trigger done → OPU procedure today
        // ═══════════════════════════════════════════════════════════════
        var p4 = await CreatePatientAsync("BN-TEST-007", "Lê Thị Chọc Hút", 1991, Gender.Female, "0901000007");
        var p4h = await CreatePatientAsync("BN-TEST-008", "Trần Văn Chồng CH", 1986, Gender.Male, "0901000008");
        var c4 = await CreateCoupleAsync(p4, p4h);
        var cy4 = await CreateCycleAsync(c4, "CK-TEST-04", TreatmentMethod.ICSI, CyclePhase.EggRetrieval);

        if (!await _context.StimulationData.AnyAsync(x => x.CycleId == cy4.Id))
        {
            var stim4 = StimulationData.Create(cy4.Id);
            stim4.Update(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-12), 11,
                12, 10, 11.5m,
                "Ovitrelle", null, DateTime.UtcNow.AddDays(-2), null, new TimeSpan(21, 0, 0), null, null, null, null,
                null, DateTime.UtcNow, null, null, null, null);
            stim4.SetDrugs(new[]
            {
                StimulationDrug.Create(stim4.Id, 0, "Gonal F", 11, "150IU"),
                StimulationDrug.Create(stim4.Id, 1, "Cetrotide", 4, "0.25mg")
            });
            _context.StimulationData.Add(stim4);
        }
        // OPU Procedure
        await CreateProcedureAsync(p4.Id, "OPU", "Chọc hút trứng", cy4.Id, "Gây mê tĩnh mạch");
        // Consent for OPU
        await CreateConsentFormAsync(p4.Id, "OPU_CONSENT", "Đồng ý thủ thuật chọc hút trứng", cy4.Id);

        // ═══════════════════════════════════════════════════════════════
        // FLOW 6 — BƠM TINH TRÙNG (IUI)
        // IUI cycle: sperm washing → insemination
        // ═══════════════════════════════════════════════════════════════
        var p9 = await CreatePatientAsync("BN-TEST-017", "Đỗ Thị IUI", 1996, Gender.Female, "0901000017");
        var p9h = await CreatePatientAsync("BN-TEST-018", "Bùi Văn Chồng IUI", 1992, Gender.Male, "0901000018");
        var c9 = await CreateCoupleAsync(p9, p9h);
        var cy9 = await CreateCycleAsync(c9, "CK-TEST-09", TreatmentMethod.IUI, CyclePhase.EggRetrieval);

        // Sperm washing for IUI
        await CreateSpermWashingAsync(cy9.Id, p9h.Id, "Swim-up");
        // Semen analysis pre-wash
        await CreateSemenAnalysisAsync(p9h.Id, AnalysisType.PreWash, cy9.Id);
        // IUI Procedure
        await CreateProcedureAsync(p9.Id, "IUI", "Bơm tinh trùng vào buồng tử cung", cy9.Id);

        // ═══════════════════════════════════════════════════════════════
        // FLOW 7 — BÁO PHÔI + CHUYỂN PHÔI + TRỮ PHÔI (Embryo Report/Transfer/Freeze)
        // ═══════════════════════════════════════════════════════════════

        // 7a. Embryo Culture
        var p5 = await CreatePatientAsync("BN-TEST-009", "Phạm Thị Nuôi Phôi", 1994, Gender.Female, "0901000009");
        var p5h = await CreatePatientAsync("BN-TEST-010", "Nguyễn Văn Chồng NP", 1989, Gender.Male, "0901000010");
        var c5 = await CreateCoupleAsync(p5, p5h);
        var cy5 = await CreateCycleAsync(c5, "CK-TEST-05", TreatmentMethod.ICSI, CyclePhase.EmbryoCulture);

        if (!await _context.CultureData.AnyAsync(x => x.CycleId == cy5.Id))
            _context.CultureData.Add(CultureData.Create(cy5.Id));
        if (!await _context.Embryos.AnyAsync(x => x.CycleId == cy5.Id))
        {
            _context.Embryos.Add(Embryo.Create(cy5.Id, 1, DateTime.UtcNow.AddDays(-1), "4C-G1", EmbryoDay.D2));
            _context.Embryos.Add(Embryo.Create(cy5.Id, 2, DateTime.UtcNow.AddDays(-1), "3C-G2", EmbryoDay.D2));
            _context.Embryos.Add(Embryo.Create(cy5.Id, 3, DateTime.UtcNow.AddDays(-1), "2C-G1", EmbryoDay.D2));
        }

        // 7b. Embryo Transfer
        var p6 = await CreatePatientAsync("BN-TEST-011", "Vũ Thị Chuyển Phôi", 1990, Gender.Female, "0901000011");
        var p6h = await CreatePatientAsync("BN-TEST-012", "Đặng Văn Chồng CP", 1987, Gender.Male, "0901000012");
        var c6 = await CreateCoupleAsync(p6, p6h);
        var cy6 = await CreateCycleAsync(c6, "CK-TEST-06", TreatmentMethod.ICSI, CyclePhase.EmbryoTransfer);

        if (!await _context.Embryos.AnyAsync(x => x.CycleId == cy6.Id))
        {
            _context.Embryos.Add(Embryo.Create(cy6.Id, 1, DateTime.UtcNow.AddDays(-5), "AA", EmbryoDay.D5, EmbryoStatus.Transferred));
            _context.Embryos.Add(Embryo.Create(cy6.Id, 2, DateTime.UtcNow.AddDays(-5), "AB", EmbryoDay.D5, EmbryoStatus.Frozen));
            _context.Embryos.Add(Embryo.Create(cy6.Id, 3, DateTime.UtcNow.AddDays(-5), "BB", EmbryoDay.D5, EmbryoStatus.Frozen));
        }
        if (!await _context.TransferData.AnyAsync(x => x.CycleId == cy6.Id))
            _context.TransferData.Add(TransferData.Create(cy6.Id));
        // ET Procedure
        await CreateProcedureAsync(p6.Id, "ET", "Chuyển phôi ngày 5", cy6.Id);
        // Freezing contract for remaining embryos
        await CreateFreezingContractAsync(cy6.Id, p6.Id, "HD-FREEZE-001");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 8 — CHUYỂN PHÔI ĐÔNG LẠNH (FET)
        // Frozen embryo transfer with endometrium preparation
        // ═══════════════════════════════════════════════════════════════
        var p10 = await CreatePatientAsync("BN-TEST-019", "Lý Thị FET", 1991, Gender.Female, "0901000019");
        var p10h = await CreatePatientAsync("BN-TEST-020", "Mai Văn Chồng FET", 1988, Gender.Male, "0901000020");
        var c10 = await CreateCoupleAsync(p10, p10h);
        var cy10 = await CreateCycleAsync(c10, "CK-TEST-10", TreatmentMethod.FET, CyclePhase.EmbryoTransfer);

        // FET Protocol
        if (!await _context.FetProtocols.AnyAsync(x => x.CycleId == cy10.Id))
            _context.FetProtocols.Add(FetProtocol.Create(cy10.Id, "HRT", DateTime.UtcNow.AddDays(-21), 1, "Phác đồ HRT chuẩn bị NMTC"));

        // Endometrium scans
        await CreateEndometriumScanAsync(cy10.Id, DateTime.UtcNow.AddDays(-14), 5, 4.5m, "Thin");
        await CreateEndometriumScanAsync(cy10.Id, DateTime.UtcNow.AddDays(-7), 12, 8.2m, "Trilaminar");
        await CreateEndometriumScanAsync(cy10.Id, DateTime.UtcNow.AddDays(-2), 17, 10.5m, "Trilaminar");

        // Frozen embryos for thaw
        if (!await _context.Embryos.AnyAsync(x => x.CycleId == cy10.Id))
        {
            _context.Embryos.Add(Embryo.Create(cy10.Id, 1, DateTime.UtcNow.AddMonths(-3), "AA", EmbryoDay.D5, EmbryoStatus.Thawed));
        }
        // Prescription for endometrium prep
        await CreatePrescriptionAsync(p10.Id, cy10.Id, "Thuốc chuẩn bị NMTC cho FET");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 9 — THỬ THAI (Pregnancy Test / Beta HCG)
        // ═══════════════════════════════════════════════════════════════

        // 9a. Luteal Phase post-transfer
        var p7 = await CreatePatientAsync("BN-TEST-013", "Hoàng Thị Hoàng Thể", 1995, Gender.Female, "0901000013");
        var p7h = await CreatePatientAsync("BN-TEST-014", "Lê Văn Chồng HT", 1990, Gender.Male, "0901000014");
        var c7 = await CreateCoupleAsync(p7, p7h);
        var cy7 = await CreateCycleAsync(c7, "CK-TEST-07", TreatmentMethod.ICSI, CyclePhase.LutealSupport);
        if (!await _context.LutealPhaseData.AnyAsync(x => x.CycleId == cy7.Id))
            _context.LutealPhaseData.Add(LutealPhaseData.Create(cy7.Id));
        // Medication for luteal support
        await CreateMedicationAdminAsync(p7.Id, cy7.Id, "Progesterone 200mg", "200mg", "PV", false);
        await CreatePrescriptionAsync(p7.Id, cy7.Id, "Hỗ trợ hoàng thể sau chuyển phôi");

        // 9b. Pregnancy test
        var p8 = await CreatePatientAsync("BN-TEST-015", "Trần Thị Thử Thai", 1993, Gender.Female, "0901000015");
        var p8h = await CreatePatientAsync("BN-TEST-016", "Ngô Văn Chồng TT", 1988, Gender.Male, "0901000016");
        var c8 = await CreateCoupleAsync(p8, p8h);
        var cy8 = await CreateCycleAsync(c8, "CK-TEST-08", TreatmentMethod.ICSI, CyclePhase.PregnancyTest);
        if (!await _context.PregnancyData.AnyAsync(x => x.CycleId == cy8.Id))
            _context.PregnancyData.Add(PregnancyData.Create(cy8.Id));
        // Lab order for Beta HCG
        await CreateLabOrderAsync(p8.Id, "BETA_HCG", cy8.Id, "Xét nghiệm Beta HCG ngày 14 sau chuyển phôi");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 10 — THAI 7 TUẦN (7-Week Prenatal)
        // Confirm pregnancy with ultrasound at 7 weeks
        // ═══════════════════════════════════════════════════════════════
        var p11 = await CreatePatientAsync("BN-TEST-021", "Nguyễn Thị Thai 7W", 1994, Gender.Female, "0901000021");
        var p11h = await CreatePatientAsync("BN-TEST-022", "Phạm Văn Chồng 7W", 1990, Gender.Male, "0901000022");
        var c11 = await CreateCoupleAsync(p11, p11h);
        var cy11 = await CreateCycleAsync(c11, "CK-TEST-11", TreatmentMethod.ICSI, CyclePhase.Completed);

        if (!await _context.PregnancyData.AnyAsync(x => x.CycleId == cy11.Id))
            _context.PregnancyData.Add(PregnancyData.Create(cy11.Id));
        // 7-week prenatal ultrasound
        await CreateUltrasoundAsync(cy11.Id, "Prenatal7W");
        // Appointment for prenatal follow-up
        await CreateAppointmentAsync(p11.Id, AppointmentType.FollowUp, cy11.Id, 14, "Tái khám thai 12 tuần");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 11 — CHO TRỨNG (Egg Donor)
        // Register egg donor + oocyte samples
        // ═══════════════════════════════════════════════════════════════
        var pDonorEgg = await CreatePatientAsync("BN-TEST-023", "Trần Thị Cho Trứng", 1997, Gender.Female, "0901000023", PatientType.EggDonor);
        await CreateEggDonorAsync("ED-001", pDonorEgg.Id);
        // Lab screening for donor
        await CreateLabOrderAsync(pDonorEgg.Id, "DONOR_SCREENING", null, "XN sàng lọc cho trứng");
        // Consent form for egg donation
        await CreateConsentFormAsync(pDonorEgg.Id, "EGG_DONOR_CONSENT", "Đồng ý hiến trứng");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 12 — NGÂN HÀNG TINH TRÙNG (Sperm Bank)
        // Register sperm donor + collect/freeze samples
        // ═══════════════════════════════════════════════════════════════
        var pDonorSperm = await CreatePatientAsync("BN-TEST-024", "Lê Văn Cho Tinh Trùng", 1995, Gender.Male, "0901000024", PatientType.SpermDonor);
        var spermDonor = await CreateSpermDonorAsync("SD-001", pDonorSperm.Id);
        // Sperm sample
        if (spermDonor != null)
            await CreateSpermSampleAsync(spermDonor.Id, "SS-001");
        // Consent form for sperm donation
        await CreateConsentFormAsync(pDonorSperm.Id, "SPERM_DONOR_CONSENT", "Đồng ý hiến tinh trùng");
        // Semen analysis for donor
        await CreateSemenAnalysisAsync(pDonorSperm.Id, AnalysisType.PreWash);

        // ═══════════════════════════════════════════════════════════════
        // FLOW 13 — NAM KHOA (Andrology)
        // Semen analysis + consultation
        // ═══════════════════════════════════════════════════════════════
        var pAndro = await CreatePatientAsync("BN-TEST-025", "Võ Văn Nam Khoa", 1989, Gender.Male, "0901000025");
        // Queue for andrology
        await CreateQueueTicketAsync("Q-TEST-003", QueueType.Andrology, pAndro.Id, "ANDROLOGY");
        // Semen analysis
        await CreateSemenAnalysisAsync(pAndro.Id, AnalysisType.PreWash);
        // Andrology consultation
        await CreateConsultationAsync(pAndro.Id, "Andrology", null, "Thiểu tinh, tinh trùng yếu");
        // Lab order
        await CreateLabOrderAsync(pAndro.Id, "SEMEN_ANALYSIS", null, "Tinh dịch đồ đầy đủ");

        // ═══════════════════════════════════════════════════════════════
        // FLOW 14 — KHO & VẬT TƯ (Inventory)
        // (Seeded separately in SeedInventoryAsync above)
        // ═══════════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════════
        // FLOW 15 — TÀI CHÍNH (Billing)
        // Create invoices with items and payments
        // ═══════════════════════════════════════════════════════════════
        await SeedBillingAsync(p1.Id, cy1.Id, p6.Id, cy6.Id);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════

    private async Task<Patient> CreatePatientAsync(string code, string name, int yob, Gender gender, string phone, PatientType type = PatientType.Infertility)
    {
        var existing = await _context.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PatientCode == code);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); await _context.SaveChangesAsync(); }
            return existing;
        }

        var p = Patient.Create(code, name, DateTime.UtcNow.AddYears(-(DateTime.Now.Year - yob)), gender, type,
            identityNumber: $"0{yob}00{code[^3..]}", phone: phone, address: "TP. Hồ Chí Minh");
        _context.Patients.Add(p);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            _context.Entry(p).State = EntityState.Detached;
            existing = await _context.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PatientCode == code);
            if (existing != null)
            {
                if (existing.IsDeleted) { existing.Restore(); await _context.SaveChangesAsync(); }
                return existing;
            }
            throw;
        }
        return p;
    }

    private async Task<Couple> CreateCoupleAsync(Patient wife, Patient husband)
    {
        var existing = await _context.Couples.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.WifeId == wife.Id && c.HusbandId == husband.Id);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); await _context.SaveChangesAsync(); }
            return existing;
        }

        var c = Couple.Create(wife.Id, husband.Id, DateTime.UtcNow.AddYears(-2), 2);
        _context.Couples.Add(c);
        await _context.SaveChangesAsync();
        return c;
    }

    private async Task<TreatmentCycle> CreateCycleAsync(Couple couple, string code, TreatmentMethod method, CyclePhase phase)
    {
        var existing = await _context.TreatmentCycles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CycleCode == code);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); await _context.SaveChangesAsync(); }
            return existing;
        }

        var cycle = TreatmentCycle.Create(couple.Id, code, method, DateTime.UtcNow);
        cycle.AdvancePhase(phase);
        _context.TreatmentCycles.Add(cycle);
        await _context.SaveChangesAsync();
        return cycle;
    }

    private async Task CreateQueueTicketAsync(string ticketNumber, QueueType type, Guid patientId, string deptCode, Guid? cycleId = null)
    {
        if (await _context.QueueTickets.AnyAsync(q => q.TicketNumber == ticketNumber))
            return;
        var ticket = QueueTicket.Create(ticketNumber, type, TicketPriority.Normal, patientId, deptCode, cycleId);
        _context.QueueTickets.Add(ticket);
    }

    private async Task CreateConsultationAsync(Guid patientId, string type, Guid? cycleId, string complaint)
    {
        if (cycleId.HasValue && await _context.Consultations.AnyAsync(c => c.PatientId == patientId && c.CycleId == cycleId))
            return;
        if (!cycleId.HasValue && await _context.Consultations.AnyAsync(c => c.PatientId == patientId && c.ConsultationType == type))
            return;
        var consultation = Consultation.Create(patientId, _doctorUserId, DateTime.UtcNow, type, cycleId, complaint);
        _context.Consultations.Add(consultation);
    }

    private async Task CreateConsentFormAsync(Guid patientId, string consentType, string title, Guid? cycleId = null)
    {
        if (await _context.ConsentForms.AnyAsync(c => c.PatientId == patientId && c.ConsentType == consentType))
            return;
        var consent = ConsentForm.Create(patientId, consentType, title, cycleId: cycleId);
        _context.ConsentForms.Add(consent);
    }

    private async Task CreateAppointmentAsync(Guid patientId, AppointmentType type, Guid? cycleId, int daysFromNow, string notes)
    {
        if (cycleId.HasValue && await _context.Appointments.AnyAsync(a => a.PatientId == patientId && a.CycleId == cycleId && a.Type == type))
            return;
        var appt = Appointment.Create(patientId, DateTime.UtcNow.AddDays(daysFromNow), type, cycleId, _doctorUserId, notes: notes);
        _context.Appointments.Add(appt);
    }

    private async Task CreateLabOrderAsync(Guid patientId, string orderType, Guid? cycleId, string notes)
    {
        if (cycleId.HasValue && await _context.LabOrders.AnyAsync(l => l.PatientId == patientId && l.CycleId == cycleId && l.OrderType == orderType))
            return;
        if (!cycleId.HasValue && await _context.LabOrders.AnyAsync(l => l.PatientId == patientId && l.OrderType == orderType))
            return;
        var order = LabOrder.Create(patientId, _doctorUserId, orderType, cycleId, notes);
        _context.LabOrders.Add(order);
    }

    private async Task CreateUltrasoundAsync(Guid cycleId, string type)
    {
        if (await _context.Ultrasounds.AnyAsync(u => u.CycleId == cycleId && u.UltrasoundType == type))
            return;
        var us = Ultrasound.Create(cycleId, DateTime.UtcNow, type, _doctorUserId);
        _context.Ultrasounds.Add(us);
    }

    private async Task CreatePrescriptionAsync(Guid patientId, Guid cycleId, string notes)
    {
        if (await _context.Prescriptions.AnyAsync(p => p.PatientId == patientId && p.CycleId == cycleId))
            return;
        var rx = Prescription.Create(patientId, _doctorUserId, DateTime.UtcNow, cycleId, notes);
        _context.Prescriptions.Add(rx);
    }

    private async Task CreateMedicationAdminAsync(Guid patientId, Guid cycleId, string medName, string dosage, string route, bool isTrigger)
    {
        if (await _context.MedicationAdministrations.AnyAsync(m => m.PatientId == patientId && m.CycleId == cycleId && m.MedicationName == medName))
            return;
        var med = MedicationAdministration.Create(patientId, cycleId, _doctorUserId, medName, dosage, route,
            DateTime.UtcNow, isTriggerShot: isTrigger);
        _context.MedicationAdministrations.Add(med);
    }

    private async Task CreateProcedureAsync(Guid patientId, string procType, string procName, Guid? cycleId, string? anesthesia = null)
    {
        if (cycleId.HasValue && await _context.Procedures.AnyAsync(p => p.PatientId == patientId && p.CycleId == cycleId && p.ProcedureType == procType))
            return;
        var proc = Procedure.Create(patientId, _doctorUserId, procType, procName, DateTime.UtcNow, cycleId, anesthesiaType: anesthesia);
        _context.Procedures.Add(proc);
    }

    private async Task CreateSpermWashingAsync(Guid cycleId, Guid patientId, string method)
    {
        if (await _context.SpermWashings.AnyAsync(w => w.CycleId == cycleId))
            return;
        var wash = SpermWashing.Create(cycleId, patientId, method, DateTime.UtcNow);
        _context.SpermWashings.Add(wash);
    }

    private async Task CreateSemenAnalysisAsync(Guid patientId, AnalysisType type, Guid? cycleId = null)
    {
        if (cycleId.HasValue && await _context.SemenAnalyses.AnyAsync(s => s.PatientId == patientId && s.CycleId == cycleId))
            return;
        if (!cycleId.HasValue && await _context.SemenAnalyses.AnyAsync(s => s.PatientId == patientId))
            return;
        var sa = SemenAnalysis.Create(patientId, DateTime.UtcNow, type, cycleId, _doctorUserId);
        _context.SemenAnalyses.Add(sa);
    }

    private async Task CreateEndometriumScanAsync(Guid cycleId, DateTime scanDate, int cycleDay, decimal thickness, string pattern)
    {
        if (await _context.EndometriumScans.AnyAsync(e => e.CycleId == cycleId && e.CycleDay == cycleDay))
            return;
        var scan = EndometriumScan.Create(cycleId, scanDate, cycleDay, thickness, pattern, doneByUserId: _doctorUserId);
        _context.EndometriumScans.Add(scan);
    }

    private async Task CreateFreezingContractAsync(Guid cycleId, Guid patientId, string contractNumber)
    {
        if (await _context.EmbryoFreezingContracts.AnyAsync(f => f.CycleId == cycleId))
            return;
        var contract = EmbryoFreezingContract.Create(cycleId, patientId, contractNumber,
            DateTime.UtcNow, DateTime.UtcNow, 12, 2000000m);
        _context.EmbryoFreezingContracts.Add(contract);
    }

    private async Task<EggDonor?> CreateEggDonorAsync(string donorCode, Guid patientId)
    {
        if (await _context.EggDonors.AnyAsync(d => d.DonorCode == donorCode))
            return await _context.EggDonors.FirstOrDefaultAsync(d => d.DonorCode == donorCode);
        var donor = EggDonor.Create(donorCode, patientId);
        _context.EggDonors.Add(donor);
        await _context.SaveChangesAsync();

        // Add oocyte sample
        if (!await _context.OocyteSamples.AnyAsync(s => s.DonorId == donor.Id))
        {
            var sample = OocyteSample.Create(donor.Id, "OS-001", DateTime.UtcNow);
            _context.OocyteSamples.Add(sample);
        }
        return donor;
    }

    private async Task<SpermDonor?> CreateSpermDonorAsync(string donorCode, Guid patientId)
    {
        if (await _context.SpermDonors.AnyAsync(d => d.DonorCode == donorCode))
            return await _context.SpermDonors.FirstOrDefaultAsync(d => d.DonorCode == donorCode);
        var donor = SpermDonor.Create(donorCode, patientId);
        _context.SpermDonors.Add(donor);
        await _context.SaveChangesAsync();
        return donor;
    }

    private async Task CreateSpermSampleAsync(Guid donorId, string sampleCode)
    {
        if (await _context.SpermSamples.AnyAsync(s => s.SampleCode == sampleCode))
            return;
        var sample = SpermSample.Create(donorId, sampleCode, DateTime.UtcNow, SpecimenType.Sperm);
        _context.SpermSamples.Add(sample);
    }

    // ═══════════════════════════════════════════════════════════════
    // BILLING SEED DATA
    // ═══════════════════════════════════════════════════════════════

    private async Task SeedBillingAsync(Guid patient1Id, Guid cycle1Id, Guid patient6Id, Guid cycle6Id)
    {
        // Invoice 1: Consultation fee (Draft)
        if (!await _context.Invoices.AnyAsync(i => i.InvoiceNumber == "INV-TEST-001"))
        {
            var inv1 = Invoice.Create("INV-TEST-001", patient1Id, DateTime.UtcNow, cycle1Id, createdByUserId: _adminUserId);
            _context.Invoices.Add(inv1);
            await _context.SaveChangesAsync();
            _context.InvoiceItems.Add(InvoiceItem.Create(inv1.Id, "TV-001", "Khám tư vấn IVF", 1, 300000m));
            _context.InvoiceItems.Add(InvoiceItem.Create(inv1.Id, "XN-001", "Xét nghiệm máu tổng quát", 1, 150000m));
            _context.InvoiceItems.Add(InvoiceItem.Create(inv1.Id, "XN-002", "Xét nghiệm hormone FSH", 1, 200000m));
        }

        // Invoice 2: ICSI cycle (Issued + Partially Paid)
        if (!await _context.Invoices.AnyAsync(i => i.InvoiceNumber == "INV-TEST-002"))
        {
            var inv2 = Invoice.Create("INV-TEST-002", patient6Id, DateTime.UtcNow.AddDays(-10), cycle6Id, createdByUserId: _adminUserId);
            _context.Invoices.Add(inv2);
            await _context.SaveChangesAsync();
            _context.InvoiceItems.Add(InvoiceItem.Create(inv2.Id, "TT-001", "Chọc hút trứng (OPU)", 1, 8000000m));
            _context.InvoiceItems.Add(InvoiceItem.Create(inv2.Id, "XN-006", "Nuôi cấy phôi", 1, 5000000m));
            _context.InvoiceItems.Add(InvoiceItem.Create(inv2.Id, "TT-002", "Chuyển phôi (ET)", 1, 5000000m));
            _context.InvoiceItems.Add(InvoiceItem.Create(inv2.Id, "XN-007", "Đông lạnh phôi", 2, 2000000m));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // INVENTORY SEED DATA
    // ═══════════════════════════════════════════════════════════════

    private async Task SeedInventoryAsync()
    {
        var items = new (string Code, string Name, string Category, string Unit, int Min, int Max, decimal Price, string Mfr)[]
        {
            ("VT-001", "Kim chọc hút trứng 17G", "Vật tư thủ thuật", "cái", 10, 100, 250000m, "Cook Medical"),
            ("VT-002", "Catheter chuyển phôi Wallace", "Vật tư thủ thuật", "cái", 5, 50, 500000m, "Smiths Medical"),
            ("VT-003", "Đĩa nuôi cấy phôi 60mm", "Vật tư phòng lab", "hộp", 5, 30, 350000m, "Falcon"),
            ("VT-004", "Môi trường nuôi cấy G1 Plus", "Môi trường", "lọ", 3, 20, 1800000m, "Vitrolife"),
            ("VT-005", "Môi trường nuôi cấy G2 Plus", "Môi trường", "lọ", 3, 20, 1800000m, "Vitrolife"),
            ("VT-006", "Ống nghiệm Falcon 15ml", "Vật tư phòng lab", "hộp", 10, 50, 120000m, "Corning"),
            ("VT-007", "Găng tay không bột size M", "Vật tư tiêu hao", "hộp", 20, 100, 85000m, "Ansell"),
            ("VT-008", "Cryotop - que đông phôi", "Vật tư trữ lạnh", "cái", 10, 100, 180000m, "Kitazato"),
        };

        foreach (var (code, name, category, unit, min, max, price, mfr) in items)
        {
            if (await _context.InventoryItems.AnyAsync(i => i.Code == code))
                continue;
            var item = InventoryItem.Create(code, name, category, unit, min, max, price, manufacturer: mfr);
            _context.InventoryItems.Add(item);
        }
        await _context.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // SERVICE CATALOG SEED DATA
    // ═══════════════════════════════════════════════════════════════

    private async Task SeedServicesAsync()
    {
        var servicesToSeed = new (string Code, string Name, ServiceCategory Category, decimal Price, string Unit, string Description)[]
        {
            // Lab Tests
            ("XN-001", "Xét nghiệm máu tổng quát", ServiceCategory.LabTest, 150000m, "lần", "Complete Blood Count"),
            ("XN-002", "Xét nghiệm hormone FSH", ServiceCategory.LabTest, 200000m, "lần", "Follicle Stimulating Hormone"),
            ("XN-003", "Xét nghiệm hormone LH", ServiceCategory.LabTest, 200000m, "lần", "Luteinizing Hormone"),
            ("XN-004", "Xét nghiệm hormone AMH", ServiceCategory.LabTest, 500000m, "lần", "Anti-Mullerian Hormone"),
            ("XN-005", "Xét nghiệm tinh dịch đồ", ServiceCategory.LabTest, 300000m, "lần", "Semen Analysis"),
            ("XN-006", "Nuôi cấy phôi", ServiceCategory.LabTest, 5000000m, "lần", "Embryo Culture"),
            ("XN-007", "Đông lạnh phôi", ServiceCategory.LabTest, 2000000m, "lần", "Embryo Freezing"),
            ("XN-008", "Đông lạnh tinh trùng", ServiceCategory.LabTest, 1000000m, "lần", "Sperm Freezing"),
            ("XN-009", "Xét nghiệm Beta HCG", ServiceCategory.LabTest, 250000m, "lần", "Beta Human Chorionic Gonadotropin"),
            ("XN-010", "Xét nghiệm hormone E2", ServiceCategory.LabTest, 200000m, "lần", "Estradiol"),
            ("XN-011", "Xét nghiệm hormone P4", ServiceCategory.LabTest, 200000m, "lần", "Progesterone"),
            // Ultrasound
            ("SA-001", "Siêu âm theo dõi nang trứng", ServiceCategory.Ultrasound, 200000m, "lần", "Follicle Monitoring"),
            ("SA-002", "Siêu âm thai", ServiceCategory.Ultrasound, 250000m, "lần", "Pregnancy Ultrasound"),
            ("SA-003", "Siêu âm nội mạc tử cung", ServiceCategory.Ultrasound, 200000m, "lần", "Endometrium Monitoring"),
            // Procedures
            ("TT-001", "Chọc hút trứng (OPU)", ServiceCategory.Procedure, 8000000m, "lần", "Oocyte Pickup"),
            ("TT-002", "Chuyển phôi (ET)", ServiceCategory.Procedure, 5000000m, "lần", "Embryo Transfer"),
            ("TT-003", "Rã đông phôi", ServiceCategory.Procedure, 1500000m, "lần", "Embryo Thawing"),
            ("TT-004", "Bơm tinh trùng (IUI)", ServiceCategory.Procedure, 3000000m, "lần", "Intrauterine Insemination"),
            // Consultation
            ("TV-001", "Khám tư vấn IVF", ServiceCategory.Consultation, 300000m, "lần", "IVF Consultation"),
            ("TV-002", "Tư vấn dinh dưỡng", ServiceCategory.Consultation, 200000m, "lần", "Nutrition Counseling"),
            ("TV-003", "Tái khám sau xét nghiệm", ServiceCategory.Consultation, 200000m, "lần", "Post-Lab Follow-up"),
            // Andrology
            ("NK-001", "Khám nam khoa", ServiceCategory.Andrology, 250000m, "lần", "Andrology Consultation"),
            ("NK-002", "Lấy tinh trùng TESE", ServiceCategory.Andrology, 10000000m, "lần", "Testicular Sperm Extraction"),
            ("NK-003", "Rửa tinh trùng (Swim-up)", ServiceCategory.Andrology, 500000m, "lần", "Sperm Washing"),
            // Medications
            ("TH-001", "Gonal-F 450IU", ServiceCategory.Medication, 2500000m, "lọ", "Ovarian Stimulation"),
            ("TH-002", "Puregon 300IU", ServiceCategory.Medication, 2000000m, "lọ", "Ovarian Stimulation"),
            ("TH-003", "Ovitrelle 250mcg", ServiceCategory.Medication, 800000m, "lọ", "Trigger Injection"),
            ("TH-004", "Progesterone 200mg", ServiceCategory.Medication, 150000m, "viên", "Luteal Support"),
            ("TH-005", "Estradiol Valerate 2mg", ServiceCategory.Medication, 100000m, "viên", "Endometrium Preparation"),
            // Sperm Bank
            ("NH-001", "Lưu trữ tinh trùng (năm)", ServiceCategory.SpermBank, 3000000m, "năm", "Annual Sperm Storage"),
            ("NH-002", "Xử lý mẫu tinh trùng", ServiceCategory.SpermBank, 1500000m, "lần", "Sperm Processing"),
        };

        foreach (var (code, name, category, price, unit, description) in servicesToSeed)
        {
            if (await _context.ServiceCatalogs.AnyAsync(s => s.Code == code))
                continue;
            _context.ServiceCatalogs.Add(ServiceCatalog.Create(code, name, category, price, unit, description));
        }
        await _context.SaveChangesAsync();
    }
}
