using AgriSustain.Application.DTOs.Authentication;
using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(1024);
        RuleFor(request => request.NewPassword).NotEmpty().MaximumLength(1024);
        RuleFor(request => request.ConfirmNewPassword).NotEmpty().MaximumLength(1024);
    }
}
