using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Domain.Entities;
using Fixturely.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        ApplyUtcDateTimeConversion(builder);
    }

    /// <summary>
    /// SQL Server's <c>datetime2</c> columns do not persist <see cref="DateTimeKind"/>, so every
    /// value EF Core materializes from the database comes back with <see cref="DateTimeKind.Unspecified"/>
    /// even though the application only ever writes UTC values (via <c>TimeProvider</c>). Left
    /// uncorrected, this causes API consumers that treat the returned timestamp as local time
    /// (or that convert it to another timezone assuming it is UTC-but-unmarked) to compute an
    /// incorrect offset. This converter forces every <see cref="DateTime"/>/<see cref="DateTime"/>?
    /// property's <see cref="DateTimeKind"/> to <see cref="DateTimeKind.Utc"/> on read, without
    /// altering the underlying stored value, so every timestamp this API serializes is an
    /// unambiguous UTC instant (serialized with a trailing "Z").
    /// </summary>
    private static void ApplyUtcDateTimeConversion(ModelBuilder builder)
    {
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            convertToProviderExpression: v => v,
            convertFromProviderExpression: v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            convertToProviderExpression: v => v,
            convertFromProviderExpression: v => v.HasValue
                ? v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                : v);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }
}
