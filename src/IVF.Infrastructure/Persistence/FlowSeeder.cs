using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

public class FlowSeeder : IFlowSeeder
{
    private readonly IvfDbContext _context;

    public FlowSeeder(IvfDbContext context)
    {
        _context = context;
    }

    public async Task SeedFlowDataAsync(CancellationToken cancellationToken = default)
    {
        // 1. Patient ready for Consultation (New)
        var p1 = await CreatePatientAsync("BN-TEST-001", "Nguyễn Thị Tư Vấn", 1995, Gender.Female);
        var p1h = await CreatePatientAsync("BN-TEST-002", "Trần Văn Chồng Tư Vấn", 1990, Gender.Male);
        var c1 = await CreateCoupleAsync(p1, p1h);
        var cy1 = await CreateCycleAsync(c1, "CK-TEST-01", TreatmentMethod.ICSI, CyclePhase.Consultation);

        // 2. Patient in Stimulation (Day 5)
        var p2 = await CreatePatientAsync("BN-TEST-003", "Phạm Thị Kích Thích", 1992, Gender.Female);
        var p2h = await CreatePatientAsync("BN-TEST-004", "Lê Văn Chồng KT", 1988, Gender.Male);
        var c2 = await CreateCoupleAsync(p2, p2h);
        var cy2 = await CreateCycleAsync(c2, "CK-TEST-02", TreatmentMethod.ICSI, CyclePhase.OvarianStimulation);
        
        // Add minimal Indication
        if (!await _context.TreatmentIndications.AnyAsync(x => x.CycleId == cy2.Id))
        {
            var ind2 = TreatmentIndication.Create(cy2.Id);
            _context.TreatmentIndications.Add(ind2);
        }
        
        // Add Stimulation Data
        if (!await _context.StimulationData.AnyAsync(x => x.CycleId == cy2.Id))
        {
            var stim2 = StimulationData.Create(cy2.Id);
            stim2.Update(DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-5), 5, 
                "Gonal F", 5, "150IU", 
                null, 0, null, null, 0, null, null, 0, null, 
                3, 1, 6.5m, 
                null, null, null, null, null, null, null, null, null, null, null, null, 0, null, null);
            _context.StimulationData.Add(stim2);
        }
        
        // 3. Patient ready for Trigger (Follicles ready)
        var p3 = await CreatePatientAsync("BN-TEST-005", "Hoàng Thị Trigger", 1993, Gender.Female);
        var p3h = await CreatePatientAsync("BN-TEST-006", "Ngô Văn Chồng Trigger", 1985, Gender.Male);
        var c3 = await CreateCoupleAsync(p3, p3h);
        var cy3 = await CreateCycleAsync(c3, "CK-TEST-03", TreatmentMethod.ICSI, CyclePhase.TriggerShot);
        
        if (!await _context.StimulationData.AnyAsync(x => x.CycleId == cy3.Id))
        {
            var stim3 = StimulationData.Create(cy3.Id);
            stim3.Update(DateTime.UtcNow.AddDays(-25), DateTime.UtcNow.AddDays(-10), 10, 
                "Gonal F", 10, "150IU", 
                "Cetrotide", 3, "0.25mg", null, 0, null, null, 0, null, 
                10, 8, 10.5m, 
                null, null, null, null, null, null, null, null, null, null, null, null, 0, null, null);
            _context.StimulationData.Add(stim3);
        }
        
        // 4. Patient ready for Egg Retrieval (Trigger done)
        var p4 = await CreatePatientAsync("BN-TEST-007", "Lê Thị Chọc Hút", 1991, Gender.Female);
        var p4h = await CreatePatientAsync("BN-TEST-008", "Trần Văn Chồng CH", 1986, Gender.Male);
        var c4 = await CreateCoupleAsync(p4, p4h);
        var cy4 = await CreateCycleAsync(c4, "CK-TEST-04", TreatmentMethod.ICSI, CyclePhase.EggRetrieval);
        // Add Stimulation Data with Trigger info
        if (!await _context.StimulationData.AnyAsync(x => x.CycleId == cy4.Id))
        {
            var stim4 = StimulationData.Create(cy4.Id);
            stim4.Update(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-12), 11, 
                "Gonal F", 11, "150IU", "Cetrotide", 4, "0.25mg", null, 0, null, null, 0, null, 
                12, 10, 11.5m, 
                "Ovitrelle", null, DateTime.UtcNow.AddDays(-2), null, new TimeSpan(21, 0, 0), null, null, null, null, 
                null, DateTime.UtcNow, null, null, null, null); // Aspiration Date = Today
            _context.StimulationData.Add(stim4);
        }


        // 5. Patient in Embryo Culture (Retrieval done)
        var p5 = await CreatePatientAsync("BN-TEST-009", "Phạm Thị Nuôi Phôi", 1994, Gender.Female);
        var p5h = await CreatePatientAsync("BN-TEST-010", "Nguyễn Văn Chồng NP", 1989, Gender.Male);
        var c5 = await CreateCoupleAsync(p5, p5h);
        var cy5 = await CreateCycleAsync(c5, "CK-TEST-05", TreatmentMethod.ICSI, CyclePhase.EmbryoCulture);
        // Add Culture Data
        if (!await _context.CultureData.AnyAsync(x => x.CycleId == cy5.Id))
        {
            var cult5 = CultureData.Create(cy5.Id);
            _context.CultureData.Add(cult5);
        }
        // Add some Embryos
        if (!await _context.Embryos.AnyAsync(x => x.CycleId == cy5.Id))
        {
            var e5_1 = Embryo.Create(cy5.Id, 1, DateTime.UtcNow.AddDays(-1));
            e5_1.UpdateGrade(EmbryoGrade.AB, EmbryoDay.D1);
            _context.Embryos.Add(e5_1);
        }


        // 6. Patient ready for Embryo Transfer
        var p6 = await CreatePatientAsync("BN-TEST-011", "Vũ Thị Chuyển Phôi", 1990, Gender.Female);
        var p6h = await CreatePatientAsync("BN-TEST-012", "Đặng Văn Chồng CP", 1987, Gender.Male);
        var c6 = await CreateCoupleAsync(p6, p6h);
        var cy6 = await CreateCycleAsync(c6, "CK-TEST-06", TreatmentMethod.ICSI, CyclePhase.EmbryoTransfer); 
        
        // Add fake embryos for transfer
        if (!await _context.Embryos.AnyAsync(x => x.CycleId == cy6.Id))
        {
            var e6_1 = Embryo.Create(cy6.Id, 1, DateTime.UtcNow.AddDays(-5));
            e6_1.UpdateGrade(EmbryoGrade.AA, EmbryoDay.D5);
            var e6_2 = Embryo.Create(cy6.Id, 2, DateTime.UtcNow.AddDays(-5));
            e6_2.UpdateGrade(EmbryoGrade.AB, EmbryoDay.D5);
            _context.Embryos.AddRange(e6_1, e6_2);
        }
        
        // Add Transfer Data
        if (!await _context.TransferData.AnyAsync(x => x.CycleId == cy6.Id))
        {
            var trans6 = TransferData.Create(cy6.Id);
            _context.TransferData.Add(trans6);
        }


        // 7. Patient in Luteal Phase (Post-Transfer)
        var p7 = await CreatePatientAsync("BN-TEST-013", "Hoàng Thị Hoàng Thể", 1995, Gender.Female);
        var p7h = await CreatePatientAsync("BN-TEST-014", "Lê Văn Chồng HT", 1990, Gender.Male);
        var c7 = await CreateCoupleAsync(p7, p7h);
        var cy7 = await CreateCycleAsync(c7, "CK-TEST-07", TreatmentMethod.ICSI, CyclePhase.LutealSupport);
        if (!await _context.LutealPhaseData.AnyAsync(x => x.CycleId == cy7.Id))
        {
            var luteal7 = LutealPhaseData.Create(cy7.Id);
            _context.LutealPhaseData.Add(luteal7);
        }


        // 8. Patient for Pregnancy Test
        var p8 = await CreatePatientAsync("BN-TEST-015", "Trần Thị Thử Thai", 1993, Gender.Female);
        var p8h = await CreatePatientAsync("BN-TEST-016", "Ngô Văn Chồng TT", 1988, Gender.Male);
        var c8 = await CreateCoupleAsync(p8, p8h);
        var cy8 = await CreateCycleAsync(c8, "CK-TEST-08", TreatmentMethod.ICSI, CyclePhase.PregnancyTest);
        if (!await _context.PregnancyData.AnyAsync(x => x.CycleId == cy8.Id))
        {
            var preg8 = PregnancyData.Create(cy8.Id);
            _context.PregnancyData.Add(preg8);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Patient> CreatePatientAsync(string code, string name, int yob, Gender gender)
    {
        // Try strict check first
        var existing = await _context.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PatientCode == code);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); await _context.SaveChangesAsync(); }
            return existing;
        }

        var p = Patient.Create(code, name, DateTime.UtcNow.AddYears(-(DateTime.Now.Year - yob)), gender, PatientType.Infertility, "0123456789", "0909000000", "Test Address");
        _context.Patients.Add(p);
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Concurrent race or weird state: Entry exists. Detach and fetch.
            _context.Entry(p).State = EntityState.Detached;
            existing = await _context.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PatientCode == code);
            if (existing != null)
            {
                if (existing.IsDeleted) { existing.Restore(); await _context.SaveChangesAsync(); }
                return existing;
            }
            throw; // Should not happen if 23505 was real
        }
        return p;
    }

    private async Task<Couple> CreateCoupleAsync(Patient wife, Patient husband)
    {
        var existing = await _context.Couples.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.WifeId == wife.Id && c.HusbandId == husband.Id);
        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                existing.Restore();
                await _context.SaveChangesAsync();
            }
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
            if (existing.IsDeleted)
            {
                existing.Restore();
                await _context.SaveChangesAsync();
            }
            // Ensure phase is correct even if existing
            if (existing.CurrentPhase != phase)
            {
               // Optional: Updating phase if we want to RESET the test data to the requested state
               // existing.AdvancePhase(phase);
            }
            return existing;
        }

        var cycle = TreatmentCycle.Create(couple.Id, code, method, DateTime.UtcNow);
        cycle.AdvancePhase(phase);
        _context.TreatmentCycles.Add(cycle);
        await _context.SaveChangesAsync();
        return cycle;
    }
}
