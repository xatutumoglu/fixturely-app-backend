using Fixturely.Domain.Entities;
using Fixturely.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixturely.Infrastructure.Persistence.Configurations;

public sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("Tournaments");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => t.OwnerUserId);
        builder.HasIndex(t => new { t.OwnerUserId, t.Status });

        // Deleting the owning AspNetUsers row deletes every tournament they own,
        // which in turn cascades (via the child relationships configured below)
        // to that tournament's members, participants, groups, rounds, matches,
        // and fixture generation histories. This is the single cascade path
        // from ApplicationUser into the tournament aggregate; TournamentMember's
        // own UserId (for non-owner members) is cleaned up separately by an
        // AFTER DELETE trigger on AspNetUsers (see the InitialCreate/Add
        // migration) instead of a second EF-level FK, because SQL Server
        // rejects a second cascade path into the same table.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Members)
            .WithOne()
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Participants)
            .WithOne()
            .HasForeignKey(p => p.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Groups)
            .WithOne()
            .HasForeignKey(g => g.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Rounds)
            .WithOne()
            .HasForeignKey(r => r.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Matches)
            .WithOne()
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.FixtureGenerationHistories)
            .WithOne()
            .HasForeignKey(h => h.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public sealed class TournamentMemberConfiguration : IEntityTypeConfiguration<TournamentMember>
{
    public void Configure(EntityTypeBuilder<TournamentMember> builder)
    {
        builder.ToTable("TournamentMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.RowVersion).IsRowVersion();

        builder.HasIndex(m => new { m.TournamentId, m.UserId }).IsUnique();

        // Intentionally no EF-level FK from UserId to ApplicationUser: this table
        // is already a cascade child of Tournament (above), and Tournament itself
        // cascades from ApplicationUser.OwnerUserId. A second cascade path into
        // this same table (via UserId) is rejected by SQL Server at migration
        // time ("may cause cycles or multiple cascade paths"). Membership rows
        // belonging to a deleted user who was NOT the tournament owner (i.e. a
        // plain member of someone else's tournament) are instead cleaned up by
        // the TR_AspNetUsers_CleanupTournamentMembers trigger created in the
        // AddUserForeignKeys migration.
    }
}

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ShortCode).HasMaxLength(10);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => new { p.TournamentId, p.Name });
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
