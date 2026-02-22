using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("user_permissions");
        builder.HasKey(up => up.Id);

        builder.Property(up => up.PermissionCode)
            .HasColumnName("Permission")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(up => new { up.UserId, up.PermissionCode }).IsUnique();
        builder.HasIndex(up => up.PermissionCode);
        builder.HasQueryFilter(up => !up.IsDeleted);
    }
}
