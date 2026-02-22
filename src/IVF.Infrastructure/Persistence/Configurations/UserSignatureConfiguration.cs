using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVF.Infrastructure.Persistence.Configurations;

public class UserSignatureConfiguration : IEntityTypeConfiguration<UserSignature>
{
    public void Configure(EntityTypeBuilder<UserSignature> builder)
    {
        builder.ToTable("user_signatures");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SignatureImageBase64)
            .IsRequired();

        builder.Property(s => s.ImageMimeType)
            .HasMaxLength(50)
            .HasDefaultValue("image/png");

        builder.Property(s => s.CertificateSubject)
            .HasMaxLength(200);

        builder.Property(s => s.CertificateSerialNumber)
            .HasMaxLength(100);

        builder.Property(s => s.WorkerName)
            .HasMaxLength(100);

        builder.Property(s => s.KeystorePath)
            .HasMaxLength(500);

        builder.Property(s => s.CertStatus)
            .HasConversion<int>()
            .HasDefaultValue(CertificateStatus.None);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.UserId);

        // Matching query filter — resolves the EF warning about
        // the required User relationship having a filter while UserSignature does not.
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
