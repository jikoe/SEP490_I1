using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Validators.Authentication;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class AuthenticationValidationTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly VerifyOtpRequestValidator _verifyOtpValidator = new();
    private readonly ResendOtpRequestValidator _resendOtpValidator = new();

    [Fact]
    public async Task Register_ValidInputs_PassesValidation()
    {
        var request = new RegisterRequest(
            FullName: "Nguyễn Văn A",
            PhoneNumber: "0912345678",
            Email: "nguyenvana@gmail.com",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Role: "RetailCustomer",
            AgreeTerms: true);

        var result = await _registerValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "0912345678", "valid@email.com", "Pass1234", "Pass1234", "RetailCustomer", true)] // Blank FullName
    [InlineData("Nguyễn Văn A", "12345", "valid@email.com", "Pass1234", "Pass1234", "RetailCustomer", true)] // Invalid Phone
    [InlineData("Nguyễn Văn A", "0912345678", "invalid-email", "Pass1234", "Pass1234", "RetailCustomer", true)] // Invalid Email
    [InlineData("Nguyễn Văn A", "0912345678", "valid@email.com", "short", "short", "RetailCustomer", true)] // Short Password
    [InlineData("Nguyễn Văn A", "0912345678", "valid@email.com", "Pass1234", "Different1234", "RetailCustomer", true)] // Mismatched Password
    [InlineData("Nguyễn Văn A", "0912345678", "valid@email.com", "Pass1234", "Pass1234", "InvalidRole", true)] // Invalid Role
    [InlineData("Nguyễn Văn A", "0912345678", "valid@email.com", "Pass1234", "Pass1234", "RetailCustomer", false)] // Unchecked Terms
    public async Task Register_InvalidInputs_FailsValidation(
        string name, string phone, string email, string pass, string confirm, string role, bool terms)
    {
        var request = new RegisterRequest(name, phone, email, pass, confirm, role, terms);
        var result = await _registerValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyOtp_ValidInputs_PassesValidation()
    {
        var request = new VerifyOtpRequest("nguyenvana@gmail.com", "123456");
        var result = await _verifyOtpValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "123456")] // Empty email
    [InlineData("invalid-email", "123456")] // Invalid email
    [InlineData("valid@gmail.com", "")] // Empty OTP
    [InlineData("valid@gmail.com", "12345")] // 5 digits
    [InlineData("valid@gmail.com", "1234567")] // 7 digits
    [InlineData("valid@gmail.com", "abcdef")] // Non-numeric
    public async Task VerifyOtp_InvalidInputs_FailsValidation(string email, string otp)
    {
        var request = new VerifyOtpRequest(email, otp);
        var result = await _verifyOtpValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ResendOtp_ValidEmail_PassesValidation()
    {
        var request = new ResendOtpRequest("nguyenvana@gmail.com");
        var result = await _resendOtpValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ResendOtp_InvalidEmail_FailsValidation()
    {
        var request = new ResendOtpRequest("invalid-email");
        var result = await _resendOtpValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
    }
}
