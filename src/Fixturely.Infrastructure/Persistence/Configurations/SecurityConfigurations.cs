using Fixturely.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixturely.Infrastructure.Persistence.Configurations;

public sealed class TournamentInvitationConfiguration : IEntityTypeConfiguration<TournamentInvitation>
{
    public void Configure(EntityTypeBuilder<TournamentInvitation> builder)
    {
        builder.ToTable("TournamentInvitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvitedEmail).HasMaxLength(256).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(i => i.RowVersion).IsRowVersion();

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.TournamentId, i.InvitedEmail, i.Status });
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(r => r.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(r => r.CreatedByIp).HasMaxLength(64);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.SessionId);
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(512);
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => s.SessionId).IsUnique();
        builder.HasIndex(s => s.UserId);
    }
}

public sealed class EmailDeliveryEventConfiguration : IEntityTypeConfiguration<EmailDeliveryEvent>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryEvent> builder)
    {
        builder.ToTable("EmailDeliveryEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.FailureReason).HasMaxLength(1000);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.AttemptedAtUtc);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Category).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.OldValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.Reason).HasMaxLength(1000);
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => a.TournamentId);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.OccurredAtUtc);
    }
}
