using AgriSustain.Application.DTOs.Profiles;
using FluentValidation;

namespace AgriSustain.Application.Validators.Profiles;

public sealed class UpdateConsumerProfileValidator : AbstractValidator<UpdateConsumerProfileRequest>
{
    public UpdateConsumerProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và đủ 10 chữ số).");

        RuleFor(x => x.AddressDetail)
            .MaximumLength(500);
    }
}

public sealed class UpdateFarmerProfileValidator : AbstractValidator<UpdateFarmerProfileRequest>
{
    public UpdateFarmerProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và đủ 10 chữ số).");

        RuleFor(x => x.FarmName)
            .NotEmpty().WithMessage("Tên nông trại không được để trống.")
            .MaximumLength(200);

        RuleFor(x => x.RepresentativeName)
            .NotEmpty().WithMessage("Người đại diện không được để trống.")
            .MaximumLength(150);

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Địa chỉ nông trại không được để trống.")
            .MaximumLength(500);

        RuleFor(x => x.CultivatedArea)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CultivatedArea.HasValue)
            .WithMessage("Diện tích canh tác không được nhỏ hơn 0.");
    }
}
