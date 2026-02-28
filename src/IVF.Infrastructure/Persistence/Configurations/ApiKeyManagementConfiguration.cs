using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class ApiKeyManagementConfiguration : IEntityTypeConfiguration<ApiKeyManagement>
{
    public void Configure(EntityTypeBuilder<ApiKeyManagement> builder)
    {
        builder.ToTable("api_key_management");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.KeyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(k => k.ServiceName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(k => k.KeyPrefix)
            .HasMaxLength(50);

        builder.Property(k => k.KeyHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(k => k.Environment)
            .HasMaxLength(50);

        builder.HasIndex(k => new { k.ServiceName, k.KeyName }).IsUnique();
        builder.HasIndex(k => k.IsActive);
        builder.HasIndex(k => k.ExpiresAt);

        builder.HasQueryFilter(k => !k.IsDeleted);
    }
}
