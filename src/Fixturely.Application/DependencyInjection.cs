using Fixturely.Application.Auth;
using Fixturely.Application.Tournaments;
using Fixturely.Application.Tournaments.Fixtures;
using Fixturely.Application.Tournaments.Formats;
using Fixturely.Application.Tournaments.Matches;
using Fixturely.Application.Tournaments.Members;
using Fixturely.Application.Tournaments.Standings;
using Fixturely.Application.Tournaments.Bracket;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fixturely.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Validators.RegisterRequestValidator>();

        services.AddScoped<ITournamentFormatEngine, LeagueFormatEngine>();
        services.AddScoped<ITournamentFormatEngine, GroupStageFormatEngine>();
        services.AddScoped<ITournamentFormatEngine, KnockoutFormatEngine>();
        services.AddScoped<ITournamentFormatEngine, GroupKnockoutFormatEngine>();

        services.AddScoped<Abstractions.Security.ITournamentAuthorizationService, TournamentAuthorizationService>();
        services.AddScoped<TournamentService>();
        services.AddScoped<ParticipantService>();
        services.AddScoped<FixtureGenerationService>();
        services.AddScoped<MatchService>();
        services.AddScoped<QualificationService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<AuthService>();

        services.AddSingleton<TieBreakerService>();
        services.AddSingleton<StandingsCalculationService>();
        services.AddSingleton<BracketProgressionService>();
        services.AddScoped<TournamentQueryService>();

        return services;
    }
}
