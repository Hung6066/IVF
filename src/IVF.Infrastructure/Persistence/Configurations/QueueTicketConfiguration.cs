using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
{
    public void Configure(EntityTypeBuilder<QueueTicket> builder)
    {
        builder.ToTable("queue_tickets");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.TicketNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.QueueType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(q => q.DepartmentCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(q => q.Patient)
            .WithMany(p => p.QueueTickets)
            .HasForeignKey(q => q.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Cycle)
            .WithMany()
            .HasForeignKey(q => q.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.CalledByUser)
            .WithMany()
            .HasForeignKey(q => q.CalledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.DepartmentCode, q.IssuedAt });
    }
}
