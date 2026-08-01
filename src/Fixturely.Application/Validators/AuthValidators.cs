using Fixturely.Application.DTOs.Auth;
using Fixturely.Domain.Exceptions;
using FluentValidation;

namespace Fixturely.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithErrorCode(ErrorCodes.UsernameRequired)
            .Length(3, 32).WithErrorCode(ErrorCodes.UsernameLengthInvalid)
            .Matches("^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username may only contain letters, digits, dots, dashes and underscores.")
            .WithErrorCode(ErrorCodes.UsernameInvalidCharset);

        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);

        RuleFor(x => x.Password)
            .SetValidator(new PasswordRuleValidator());
    }
}

public sealed class PasswordRuleValidator : AbstractValidator<string>
{
    public PasswordRuleValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithErrorCode(ErrorCodes.PasswordRequired)
            .MinimumLength(8).WithErrorCode(ErrorCodes.PasswordTooShort)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase character.")
                .WithErrorCode(ErrorCodes.PasswordRequiresUppercase)
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase character.")
                .WithErrorCode(ErrorCodes.PasswordRequiresLowercase)
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
                .WithErrorCode(ErrorCodes.PasswordRequiresDigit)
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.")
                .WithErrorCode(ErrorCodes.PasswordRequiresSpecialChar);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.EmailOrUserName).NotEmpty().WithErrorCode(ErrorCodes.EmailOrUsernameRequired);
        RuleFor(x => x.Password).NotEmpty().WithErrorCode(ErrorCodes.PasswordRequired);
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.UserIdRequired);
        RuleFor(x => x.Token).NotEmpty().WithErrorCode(ErrorCodes.TokenRequired);
    }
}

public sealed class ResendConfirmationRequestValidator : AbstractValidator<ResendConfirmationRequest>
{
    public ResendConfirmationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithErrorCode(ErrorCodes.EmailRequired).EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.UserIdRequired);
        RuleFor(x => x.Token).NotEmpty().WithErrorCode(ErrorCodes.TokenRequired);
        RuleFor(x => x.NewPassword).SetValidator(new PasswordRuleValidator());
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode(ErrorCodes.RefreshTokenRequired);
    }
}
