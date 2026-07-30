using Fixturely.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixturely.Infrastructure.Persistence.Configurations;

public sealed class TournamentGroupConfiguration : IEntityTypeConfiguration<TournamentGroup>
{
    public void Configure(EntityTypeBuilder<TournamentGroup> builder)
    {
        builder.ToTable("TournamentGroups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).HasMaxLength(50).IsRequired();
        builder.Property(g => g.RowVersion).IsRowVersion();

        builder.HasMany(g => g.GroupParticipants)
            .WithOne()
            .HasForeignKey(gp => gp.TournamentGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => new { g.TournamentId, g.OrderIndex }).IsUnique();
    }
}

public sealed class GroupParticipantConfiguration : IEntityTypeConfiguration<GroupParticipant>
{
    public void Configure(EntityTypeBuilder<GroupParticipant> builder)
    {
        builder.ToTable("GroupParticipants");
        builder.HasKey(gp => gp.Id);
        builder.Property(gp => gp.RowVersion).IsRowVersion();

        builder.HasIndex(gp => new { gp.TournamentGroupId, gp.ParticipantId }).IsUnique();
    }
}

public sealed class TournamentRoundConfiguration : IEntityTypeConfiguration<TournamentRound>
{
    public void Configure(EntityTypeBuilder<TournamentRound> builder)
    {
        builder.ToTable("TournamentRounds");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => new { r.TournamentId, r.RoundNumber });
    }
}

public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Venue).HasMaxLength(200);
        builder.Property(m => m.RowVersion).IsRowVersion();

        builder.HasIndex(m => m.TournamentId);
        builder.HasIndex(m => m.RoundId);
        builder.HasIndex(m => m.TournamentGroupId);
        builder.HasIndex(m => m.TieIdentifier);
        builder.HasIndex(m => m.HomeParticipantId);
        builder.HasIndex(m => m.AwayParticipantId);

        builder.HasOne<TournamentRound>()
            .WithMany()
            .HasForeignKey(m => m.RoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TournamentGroup>()
            .WithMany()
            .HasForeignKey(m => m.TournamentGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FixtureGenerationHistoryConfiguration : IEntityTypeConfiguration<FixtureGenerationHistory>
{
    public void Configure(EntityTypeBuilder<FixtureGenerationHistory> builder)
    {
        builder.ToTable("FixtureGenerationHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.RandomSeed).HasMaxLength(64).IsRequired();
        builder.Property(h => h.DrawMetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(h => h.RowVersion).IsRowVersion();

        builder.HasIndex(h => new { h.TournamentId, h.GenerationNumber }).IsUnique();
    }
}

public sealed class TieBreakResolutionConfiguration : IEntityTypeConfiguration<TieBreakResolution>
{
    public void Configure(EntityTypeBuilder<TieBreakResolution> builder)
    {
        builder.ToTable("TieBreakResolutions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TiedParticipantIdsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(t => t.ResolutionDetailsJson).HasColumnType("nvarchar(max)");
        builder.Property(t => t.RandomSeed).HasMaxLength(64);
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => t.TournamentId);
    }
}
