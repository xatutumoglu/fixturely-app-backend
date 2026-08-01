using Fixturely.Application.DTOs.Matches;
using Fixturely.Application.DTOs.Members;
using Fixturely.Application.DTOs.Participants;
using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using FluentValidation;

namespace Fixturely.Application.Validators;

public sealed class CreateTournamentRequestValidator : AbstractValidator<CreateTournamentRequest>
{
    public CreateTournamentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.TournamentNameRequired)
            .MaximumLength(150).WithErrorCode(ErrorCodes.TournamentNameTooLong);
        RuleFor(x => x.Description).MaximumLength(2000).WithErrorCode(ErrorCodes.TournamentDescriptionTooLong);
        RuleFor(x => x.Format).IsInEnum().WithErrorCode(ErrorCodes.TournamentFormatInvalid);
        RuleFor(x => x.LegMode).IsInEnum().WithErrorCode(ErrorCodes.TournamentLegModeInvalid);

        When(x => x.Format is TournamentFormat.GroupStage or TournamentFormat.GroupKnockout, () =>
        {
            RuleFor(x => x.NumberOfGroups)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("Number of groups is required for group-based tournament formats.")
                .WithErrorCode(ErrorCodes.NumberOfGroupsRequired);
        });

        When(x => x.Format == TournamentFormat.GroupKnockout, () =>
        {
            RuleFor(x => x.NumberOfGroups)
                .Must(n => n is 2 or 4 or 8 or 16)
                .WithMessage("Group + knockout tournaments must use 2, 4, 8, or 16 groups.")
                .WithErrorCode(ErrorCodes.GroupKnockoutInvalidGroupCount);
        });
    }
}

public sealed class UpdateTournamentRequestValidator : AbstractValidator<UpdateTournamentRequest>
{
    public UpdateTournamentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.TournamentNameRequired)
            .MaximumLength(150).WithErrorCode(ErrorCodes.TournamentNameTooLong);
        RuleFor(x => x.Description).MaximumLength(2000).WithErrorCode(ErrorCodes.TournamentDescriptionTooLong);
        RuleFor(x => x.RowVersion).NotEmpty().WithErrorCode(ErrorCodes.RowVersionRequired);
    }
}

public sealed class BulkDeleteTournamentsRequestValidator : AbstractValidator<BulkDeleteTournamentsRequest>
{
    public BulkDeleteTournamentsRequestValidator()
    {
        RuleFor(x => x.TournamentIds)
            .NotEmpty().WithMessage("At least one tournament id is required.")
            .WithErrorCode(ErrorCodes.TournamentIdsRequired);
    }
}

public sealed class CreateParticipantRequestValidator : AbstractValidator<CreateParticipantRequest>
{
    public CreateParticipantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.ParticipantNameRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.ParticipantNameTooLong);
        RuleFor(x => x.ShortCode).MaximumLength(10).WithErrorCode(ErrorCodes.ParticipantShortCodeTooLong);
    }
}

public sealed class UpdateParticipantRequestValidator : AbstractValidator<UpdateParticipantRequest>
{
    public UpdateParticipantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.ParticipantNameRequired)
            .MaximumLength(100).WithErrorCode(ErrorCodes.ParticipantNameTooLong);
        RuleFor(x => x.ShortCode).MaximumLength(10).WithErrorCode(ErrorCodes.ParticipantShortCodeTooLong);
    }
}

public sealed class BulkCreateParticipantsRequestValidator : AbstractValidator<BulkCreateParticipantsRequest>
{
    public BulkCreateParticipantsRequestValidator()
    {
        RuleFor(x => x.Participants)
            .NotEmpty().WithMessage("At least one participant is required.").WithErrorCode(ErrorCodes.ParticipantsRequired)
            .Must(p => p.Count <= 200).WithMessage("At most 200 participants can be added at once.")
                .WithErrorCode(ErrorCodes.ParticipantsMaxExceeded);

        RuleForEach(x => x.Participants).SetValidator(new CreateParticipantRequestValidator());
    }
}

public sealed class BulkDeleteParticipantsRequestValidator : AbstractValidator<BulkDeleteParticipantsRequest>
{
    public BulkDeleteParticipantsRequestValidator()
    {
        RuleFor(x => x.ParticipantIds)
            .NotEmpty().WithMessage("At least one participant id is required.")
            .WithErrorCode(ErrorCodes.ParticipantIdsRequired);
    }
}

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);
        RuleFor(x => x.Role)
            .Must(r => r is TournamentMemberRole.ScoreManager or TournamentMemberRole.Viewer)
            .WithMessage("Invited members must be assigned the ScoreManager or Viewer role.")
            .WithErrorCode(ErrorCodes.InviteRoleRestricted);
    }
}

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .Must(r => r is TournamentMemberRole.ScoreManager or TournamentMemberRole.Viewer)
            .WithMessage("Members can only be assigned the ScoreManager or Viewer role.")
            .WithErrorCode(ErrorCodes.ChangeRoleRestricted);
    }
}

public sealed class BulkInviteMembersRequestValidator : AbstractValidator<BulkInviteMembersRequest>
{
    public BulkInviteMembersRequestValidator()
    {
        RuleFor(x => x.Emails)
            .NotEmpty().WithMessage("At least one email address is required.").WithErrorCode(ErrorCodes.EmailsRequired)
            .Must(e => e.Count <= 100).WithMessage("At most 100 invitations can be sent at once.")
                .WithErrorCode(ErrorCodes.EmailsMaxExceeded);

        RuleForEach(x => x.Emails)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);

        RuleFor(x => x.Role)
            .Must(r => r is TournamentMemberRole.ScoreManager or TournamentMemberRole.Viewer)
            .WithMessage("Invited members must be assigned the ScoreManager or Viewer role.")
            .WithErrorCode(ErrorCodes.InviteRoleRestricted);
    }
}

public sealed class BulkRemoveMembersRequestValidator : AbstractValidator<BulkRemoveMembersRequest>
{
    public BulkRemoveMembersRequestValidator()
    {
        RuleFor(x => x.MemberIds)
            .NotEmpty().WithMessage("At least one member id is required.")
            .WithErrorCode(ErrorCodes.MemberIdsRequired);
    }
}

public sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithErrorCode(ErrorCodes.TokenRequired);
    }
}

public sealed class UpdateMatchScoreRequestValidator : AbstractValidator<UpdateMatchScoreRequest>
{
    public UpdateMatchScoreRequestValidator()
    {
        RuleFor(x => x.HomeRegularTimeScore).GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ScoresMustBeNonNegative);
        RuleFor(x => x.AwayRegularTimeScore).GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ScoresMustBeNonNegative);
        RuleFor(x => x.HomeExtraTimeScore)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ScoresMustBeNonNegative)
            .When(x => x.HomeExtraTimeScore is not null);
        RuleFor(x => x.AwayExtraTimeScore)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ScoresMustBeNonNegative)
            .When(x => x.AwayExtraTimeScore is not null);
        RuleFor(x => x.HomePenaltyScore)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ScoresMustBeNonNegative)
            .When(x => x.HomePenaltyScore is not null);
        RuleFor(x => x.AwayPenaltyScore)
            .GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ScoresMustBeNonNegative)
            .When(x => x.AwayPenaltyScore is not null);
        RuleFor(x => x.RowVersion).NotEmpty().WithErrorCode(ErrorCodes.RowVersionRequired);
    }
}

public sealed class ScheduleMatchRequestValidator : AbstractValidator<ScheduleMatchRequest>
{
    public ScheduleMatchRequestValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty().WithErrorCode(ErrorCodes.RowVersionRequired);
        RuleFor(x => x.Venue).MaximumLength(200).WithErrorCode(ErrorCodes.VenueTooLong);
    }
}

public sealed class InvalidateMatchRequestValidator : AbstractValidator<InvalidateMatchRequest>
{
    public InvalidateMatchRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithErrorCode(ErrorCodes.ReasonRequired)
            .MaximumLength(500).WithErrorCode(ErrorCodes.ReasonTooLong);
        RuleFor(x => x.RowVersion).NotEmpty().WithErrorCode(ErrorCodes.RowVersionRequired);
    }
}
