using Fixturely.Domain.Enums;

namespace Fixturely.Application.Common;

/// <summary>
/// Centralizes the "how many participants can this tournament ever hold" rule so the API
/// (validation, bulk-add capacity checks) and the UI (via <see cref="Tournaments.TournamentDetailResponse"/>)
/// agree on the same number the fixture-generation engines actually enforce
/// (see <see cref="Tournaments.Formats.GroupStageFormatEngine"/> / GroupDrawHelper).
/// </summary>
public static class ParticipantCapacity
{
    public const int ParticipantsPerGroup = 4;

    /// <summary>
    /// Returns the exact number of participants a tournament of this format/group-count must
    /// have before a fixture can be generated, or <c>null</c> when the format has no fixed
    /// capacity (League and Knockout accept any participant count &gt;= 2).
    /// </summary>
    public static int? GetMaxParticipants(TournamentFormat format, int? numberOfGroups)
    {
        if (format is TournamentFormat.GroupStage or TournamentFormat.GroupKnockout)
        {
            return (numberOfGroups ?? 0) * ParticipantsPerGroup;
        }

        return null;
    }
}
