using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AuthService.Domain.Entities;

namespace AuthService.Infrastructure.Persistence.Configurations;

public class PasswordResetVerificationConfig : IEntityTypeConfiguration<PasswordResetVerification>
{
    public void Configure(EntityTypeBuilder<PasswordResetVerification> builder)
    {
        builder.ToTable("PasswordResetVerifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.HashedToken).IsRequired();
        
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_PasswordResetVerification_UserId");
        builder.HasIndex(x => x.HashedToken).IsUnique()
            .HasDatabaseName("IX_PasswordResetVerification_HashedToken");;
        
        // Make sure we clean up on foreign key deletion
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}