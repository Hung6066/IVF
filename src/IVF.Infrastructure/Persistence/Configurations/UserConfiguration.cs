using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.Department)
            .HasMaxLength(50);

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
