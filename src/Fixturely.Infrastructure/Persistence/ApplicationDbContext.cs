using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Domain.Entities;
using Fixturely.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fixturely.Infrastructure.Persistence;

public sealed class ApplicationDbContext :
    IdentityUserContext<ApplicationUser, Guid>,
    IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Tournament> Tournaments => Set<Tournament>();

    public DbSet<TournamentMember> TournamentMembers => Set<TournamentMember>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<TournamentGroup> TournamentGroups => Set<TournamentGroup>();

    public DbSet<GroupParticipant> GroupParticipants => Set<GroupParticipant>();

    public DbSet<TournamentRound> TournamentRounds => Set<TournamentRound>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<FixtureGenerationHistory> FixtureGenerationHistories => Set<FixtureGenerationHistory>();

    public DbSet<TournamentInvitation> TournamentInvitations => Set<TournamentInvitation>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<EmailDeliveryEvent> EmailDeliveryEvents => Set<EmailDeliveryEvent>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<TieBreakResolution> TieBreakResolutions => Set<TieBreakResolution>();

    public void SetOriginalRowVersion(object entity, byte[] rowVersion)
    {
        Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
