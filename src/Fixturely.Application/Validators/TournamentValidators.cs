using Fixturely.Application.DTOs.Matches;
using Fixturely.Application.DTOs.Members;
using Fixturely.Application.DTOs.Participants;
using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Domain.Enums;
using FluentValidation;

namespace Fixturely.Application.Validators;

public sealed class CreateTournamentRequestValidator : AbstractValidator<CreateTournamentRequest>
{
    public CreateTournamentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.LegMode).IsInEnum();

        When(x => x.Format is TournamentFormat.GroupStage or TournamentFormat.GroupKnockout, () =>
        {
            RuleFor(x => x.NumberOfGroups)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("Number of groups is required for group-based tournament formats.");
        });

        When(x => x.Format == TournamentFormat.GroupKnockout, () =>
        {
            RuleFor(x => x.NumberOfGroups)
                .Must(n => n is 2 or 4 or 8 or 16)
                .WithMessage("Group + knockout tournaments must use 2, 4, 8, or 16 groups.");
        });
    }
}

public sealed class UpdateTournamentRequestValidator : AbstractValidator<UpdateTournamentRequest>
{
    public UpdateTournamentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class CreateParticipantRequestValidator : AbstractValidator<CreateParticipantRequest>
{
    public CreateParticipantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortCode).MaximumLength(10);
    }
}

public sealed class UpdateParticipantRequestValidator : AbstractValidator<UpdateParticipantRequest>
{
    public UpdateParticipantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortCode).MaximumLength(10);
    }
}

public sealed class BulkCreateParticipantsRequestValidator : AbstractValidator<BulkCreateParticipantsRequest>
{
    public BulkCreateParticipantsRequestValidator()
    {
        RuleFor(x => x.Participants)
            .NotEmpty().WithMessage("At least one participant is required.")
            .Must(p => p.Count <= 200).WithMessage("At most 200 participants can be added at once.");

        RuleForEach(x => x.Participants).SetValidator(new CreateParticipantRequestValidator());
    }
}

public sealed class BulkDeleteParticipantsRequestValidator : AbstractValidator<BulkDeleteParticipantsRequest>
{
    public BulkDeleteParticipantsRequestValidator()
    {
        RuleFor(x => x.ParticipantIds).NotEmpty().WithMessage("At least one participant id is required.");
    }
}

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role)
            .Must(r => r is TournamentMemberRole.ScoreManager or TournamentMemberRole.Viewer)
            .WithMessage("Invited members must be assigned the ScoreManager or Viewer role.");
    }
}

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .Must(r => r is TournamentMemberRole.ScoreManager or TournamentMemberRole.Viewer)
            .WithMessage("Members can only be assigned the ScoreManager or Viewer role.");
    }
}

public sealed class BulkInviteMembersRequestValidator : AbstractValidator<BulkInviteMembersRequest>
{
    public BulkInviteMembersRequestValidator()
    {
        RuleFor(x => x.Emails)
            .NotEmpty().WithMessage("At least one email address is required.")
            .Must(e => e.Count <= 100).WithMessage("At most 100 invitations can be sent at once.");

        RuleForEach(x => x.Emails).NotEmpty().EmailAddress();

        RuleFor(x => x.Role)
            .Must(r => r is TournamentMemberRole.ScoreManager or TournamentMemberRole.Viewer)
            .WithMessage("Invited members must be assigned the ScoreManager or Viewer role.");
    }
}

public sealed class BulkRemoveMembersRequestValidator : AbstractValidator<BulkRemoveMembersRequest>
{
    public BulkRemoveMembersRequestValidator()
    {
        RuleFor(x => x.MemberIds).NotEmpty().WithMessage("At least one member id is required.");
    }
}

public sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class UpdateMatchScoreRequestValidator : AbstractValidator<UpdateMatchScoreRequest>
{
    public UpdateMatchScoreRequestValidator()
    {
        RuleFor(x => x.HomeRegularTimeScore).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AwayRegularTimeScore).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HomeExtraTimeScore).GreaterThanOrEqualTo(0).When(x => x.HomeExtraTimeScore is not null);
        RuleFor(x => x.AwayExtraTimeScore).GreaterThanOrEqualTo(0).When(x => x.AwayExtraTimeScore is not null);
        RuleFor(x => x.HomePenaltyScore).GreaterThanOrEqualTo(0).When(x => x.HomePenaltyScore is not null);
        RuleFor(x => x.AwayPenaltyScore).GreaterThanOrEqualTo(0).When(x => x.AwayPenaltyScore is not null);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class ScheduleMatchRequestValidator : AbstractValidator<ScheduleMatchRequest>
{
    public ScheduleMatchRequestValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x.Venue).MaximumLength(200);
    }
}

public sealed class InvalidateMatchRequestValidator : AbstractValidator<InvalidateMatchRequest>
{
    public InvalidateMatchRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
