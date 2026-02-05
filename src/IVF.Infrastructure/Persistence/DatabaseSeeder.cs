using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IVF.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

            // Check if already seeded
            if (await context.Users.AnyAsync())
            {
                Console.WriteLine("[Seeder] Data already exists. Skipping seed.");
                return;
            }

            Console.WriteLine("[Seeder] Starting database seed...");

            // Seed Users
            var adminUser = User.Create(
                "admin",
                BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                "Quản trị viên",
                "Admin",
                "IT"
            );

            var doctorUser = User.Create(
                "doctor1",
                BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                "BS. Nguyễn Văn A",
                "Doctor",
                "Consultation"
            );

            var nurseUser = User.Create(
                "nurse1",
                BCrypt.Net.BCrypt.HashPassword("Nurse@123"),
                "DD. Trần Thị B",
                "Nurse",
                "Ultrasound"
            );

            var receptionUser = User.Create(
                "reception1",
                BCrypt.Net.BCrypt.HashPassword("Reception@123"),
                "Lê Văn C",
                "Reception",
                "Reception"
            );

            context.Users.AddRange(adminUser, doctorUser, nurseUser, receptionUser);

            // Seed Sample Patients
            var patient1 = Patient.Create(
                "BN-2026-000001",
                "Nguyễn Thị Hoa",
                DateTime.SpecifyKind(new DateTime(1990, 5, 15), DateTimeKind.Utc),
                Gender.Female,
                PatientType.Infertility,
                "079090123456",
                "0901234567",
                "123 Nguyễn Huệ, Q1, TP.HCM"
            );

            var patient2 = Patient.Create(
                "BN-2026-000002",
                "Trần Văn Minh",
                DateTime.SpecifyKind(new DateTime(1988, 3, 20), DateTimeKind.Utc),
                Gender.Male,
                PatientType.Infertility,
                "079088345678",
                "0912345678",
                "456 Lê Lợi, Q1, TP.HCM"
            );

            var patient3 = Patient.Create(
                "BN-2026-000003",
                "Phạm Thị Lan",
                DateTime.SpecifyKind(new DateTime(1992, 8, 10), DateTimeKind.Utc),
                Gender.Female,
                PatientType.Infertility,
                "079092567890",
                "0923456789",
                "789 Hai Bà Trưng, Q3, TP.HCM"
            );

            var patient4 = Patient.Create(
                "BN-2026-000004",
                "Lê Văn Hùng",
                DateTime.SpecifyKind(new DateTime(1985, 12, 5), DateTimeKind.Utc),
                Gender.Male,
                PatientType.Infertility,
                "079085678901",
                "0934567890",
                "321 Võ Văn Tần, Q3, TP.HCM"
            );

            context.Patients.AddRange(patient1, patient2, patient3, patient4);
            await context.SaveChangesAsync();

            Console.WriteLine("[Seeder] Users and Patients seeded.");

            // Seed Couples
            var couple1 = Couple.Create(patient1.Id, patient2.Id, DateTime.SpecifyKind(new DateTime(2015, 6, 10), DateTimeKind.Utc), 5);
            var couple2 = Couple.Create(patient3.Id, patient4.Id, DateTime.SpecifyKind(new DateTime(2018, 9, 20), DateTimeKind.Utc), 3);

            context.Couples.AddRange(couple1, couple2);
            await context.SaveChangesAsync();

            Console.WriteLine("[Seeder] Couples seeded.");

            // Seed Treatment Cycles
            var cycle1 = TreatmentCycle.Create(
                couple1.Id,
                "CK-2026-0001",
                TreatmentMethod.ICSI,
                DateTime.UtcNow.AddDays(-10),
                "Chu kỳ ICSI đầu tiên"
            );
            cycle1.AdvancePhase(CyclePhase.OvarianStimulation);

            var cycle2 = TreatmentCycle.Create(
                couple2.Id,
                "CK-2026-0002",
                TreatmentMethod.IUI,
                DateTime.UtcNow.AddDays(-5),
                "Chu kỳ IUI"
            );

            context.TreatmentCycles.AddRange(cycle1, cycle2);
            await context.SaveChangesAsync();

            Console.WriteLine("[Seeder] Treatment Cycles seeded.");

            // Seed Queue Tickets
            var ticket1 = QueueTicket.Create("REC-001", QueueType.Reception, TicketPriority.Normal, patient1.Id, "REC");
            var ticket2 = QueueTicket.Create("REC-002", QueueType.Reception, TicketPriority.Normal, patient3.Id, "REC");
            var ticket3 = QueueTicket.Create("US-001", QueueType.Ultrasound, TicketPriority.Normal, patient1.Id, "US", cycle1.Id);

            context.QueueTickets.AddRange(ticket1, ticket2, ticket3);

            // Seed Ultrasounds
            var ultrasound1 = Ultrasound.Create(cycle1.Id, DateTime.UtcNow, "NangNoan", doctorUser.Id);
            ultrasound1.RecordFollicles(8, 6, "18,16,14,12", "15,14,12", 8.5m, "Buồng trứng đáp ứng tốt");

            context.Ultrasounds.Add(ultrasound1);

            // Seed Embryos
            var embryo1 = Embryo.Create(cycle1.Id, 1, DateTime.UtcNow.AddDays(-3));
            embryo1.UpdateGrade(EmbryoGrade.AA, EmbryoDay.D3);

            var embryo2 = Embryo.Create(cycle1.Id, 2, DateTime.UtcNow.AddDays(-3));
            embryo2.UpdateGrade(EmbryoGrade.AB, EmbryoDay.D3);

            var embryo3 = Embryo.Create(cycle1.Id, 3, DateTime.UtcNow.AddDays(-3));
            embryo3.UpdateGrade(EmbryoGrade.BB, EmbryoDay.D3);

            context.Embryos.AddRange(embryo1, embryo2, embryo3);

            await context.SaveChangesAsync();

            Console.WriteLine("[Seeder] Database seed completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Seeder] Error during seeding: {ex.Message}");
            Console.WriteLine($"[Seeder] Stack: {ex.StackTrace}");
            // Don't rethrow - allow API to start even if seeding fails
        }
    }
}
