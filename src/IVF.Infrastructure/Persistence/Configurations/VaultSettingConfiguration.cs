using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultSettingConfiguration : IEntityTypeConfiguration<VaultSetting>
{
    public void Configure(EntityTypeBuilder<VaultSetting> builder)
    {
        builder.ToTable("vault_settings");
        builder.HasKey(s => s.Key);

        builder.Property(s => s.Key).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ValueJson).HasColumnType("jsonb").IsRequired();
    }
}
