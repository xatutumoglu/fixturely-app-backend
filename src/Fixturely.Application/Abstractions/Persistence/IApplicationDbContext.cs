using Fixturely.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fixturely.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<Tournament> Tournaments { get; }

    DbSet<TournamentMember> TournamentMembers { get; }

    DbSet<Participant> Participants { get; }

    DbSet<TournamentGroup> TournamentGroups { get; }

    DbSet<GroupParticipant> GroupParticipants { get; }

    DbSet<TournamentRound> TournamentRounds { get; }

    DbSet<Match> Matches { get; }

    DbSet<FixtureGenerationHistory> FixtureGenerationHistories { get; }

    DbSet<TournamentInvitation> TournamentInvitations { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<UserSession> UserSessions { get; }

    DbSet<EmailDeliveryEvent> EmailDeliveryEvents { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<TieBreakResolution> TieBreakResolutions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the original RowVersion value EF Core should compare against when saving, so that
    /// a stale client-supplied concurrency token results in a <see cref="DbUpdateConcurrencyException"/>.
    /// </summary>
    void SetOriginalRowVersion(object entity, byte[] rowVersion);
}
