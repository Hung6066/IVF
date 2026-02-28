using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class VaultUserPolicyConfiguration : IEntityTypeConfiguration<VaultUserPolicy>
{
    public void Configure(EntityTypeBuilder<VaultUserPolicy> builder)
    {
        builder.ToTable("vault_user_policies");
        builder.HasKey(up => up.Id);

        builder.HasOne(up => up.Policy)
            .WithMany()
            .HasForeignKey(up => up.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(up => new { up.UserId, up.PolicyId }).IsUnique();
        builder.HasQueryFilter(up => !up.IsDeleted);
    }
}
