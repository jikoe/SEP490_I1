using AgriSustain.Domain.Entities.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriSustain.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("User");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserId");
        builder.Property(x => x.Username).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.IsActive)
            .HasColumnName("Status")
            .HasConversion(value => value ? "Active" : "Inactive", value => value == "Active")
            .HasMaxLength(50)
            .IsUnicode(false);
        builder.Property(x => x.IsEmailVerified).IsRequired();
        builder.Property(x => x.FailedLoginCount).IsRequired();
        builder.Ignore(x => x.UpdatedAt);
    }
}
