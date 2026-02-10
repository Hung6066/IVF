using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class QueueTicketServiceConfiguration : IEntityTypeConfiguration<QueueTicketService>
{
    public void Configure(EntityTypeBuilder<QueueTicketService> builder)
    {
        builder.ToTable("queue_ticket_services");
        builder.HasKey(q => q.Id);

        builder.HasOne(q => q.QueueTicket)
            .WithMany(t => t.Services)
            .HasForeignKey(q => q.QueueTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // No DB-level FK to ServiceCatalog — table is partitioned by Category.
        // ServiceCatalogId is kept as a plain column with an index for lookups.
        builder.HasOne(q => q.ServiceCatalog)
            .WithMany()
            .HasForeignKey(q => q.ServiceCatalogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => q.QueueTicketId);
        builder.HasIndex(q => q.ServiceCatalogId);
        builder.HasIndex(q => new { q.QueueTicketId, q.ServiceCatalogId }).IsUnique();
        builder.HasQueryFilter(q => !q.IsDeleted);
    }
}
