using AgriSustain.Application.DTOs.Authentication;
using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Vui lòng nhập Họ và tên (tối đa 50 ký tự).")
            .MaximumLength(150);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và đủ 10 chữ số).");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Địa chỉ email không được để trống.")
            .EmailAddress().WithMessage("Địa chỉ email không đúng định dạng.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Mật khẩu xác nhận không khớp.");

        RuleFor(x => x.Role)
            .Must(role => role is "RetailCustomer" or "Farmer")
            .WithMessage("Vai trò tài khoản phải là RetailCustomer hoặc Farmer.");

        RuleFor(x => x.AgreeTerms)
            .Equal(true)
            .WithMessage("Bạn cần đồng ý với Điều khoản dịch vụ & Chính sách bảo mật của AgriSustain.");
    }
}

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Vui lòng nhập mã OTP.")
            .Length(6).WithMessage("Vui lòng nhập đủ 6 chữ số mã OTP.")
            .Matches(@"^\d{6}$").WithMessage("Mã OTP chỉ bao gồm chữ số.");
    }
}

public sealed class ResendOtpRequestValidator : AbstractValidator<ResendOtpRequest>
{
    public ResendOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");
    }
}
