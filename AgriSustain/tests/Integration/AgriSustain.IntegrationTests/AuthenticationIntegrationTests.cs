using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Authentication;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class AuthenticationIntegrationTests
{
    [Fact]
    public async Task Register_ValidRetailCustomer_InitiatesOtpSuccessfully()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client(null); // Anonymous

        var request = new RegisterRequest(
            FullName: "Nguyễn Văn A",
            PhoneNumber: "0912345678",
            Email: "newcustomer@example.test",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Role: "RetailCustomer",
            AgreeTerms: true);

        // 1. Submit Registration (SCR-07 / UC-AUTH-01)
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 2. Verify OTP (UC-AUTH-08)
        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-otp",
            new VerifyOtpRequest("newcustomer@example.test", "123456"));
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var authResult = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.Equal("Nguyễn Văn A", authResult.FullName);
        Assert.Equal("newcustomer@example.test", authResult.Email);
        Assert.Equal("RetailCustomer", authResult.Role);

        // Verify account exists in state
        var snapshot = await factory.Store.SnapshotAsync();
        var user = Assert.Single(snapshot.Accounts.Values, u => u.Email == "newcustomer@example.test");
        Assert.Equal("Active", user.Status);
    }

    [Fact]
    public async Task Register_ValidFarmer_InitializesUnverifiedFarmerProfile()
    {
        // Corresponds to IT-AUTH01-02
        using var factory = new ApiFactory();
        using var client = factory.Client(null);

        var request = new RegisterRequest(
            FullName: "Trần Văn Nông",
            PhoneNumber: "0934567890",
            Email: "newfarmer@example.test",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Role: "Farmer",
            AgreeTerms: true);

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-otp",
            new VerifyOtpRequest("newfarmer@example.test", "123456"));
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var authResult = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.Equal("Farmer", authResult.Role);

        var snapshot = await factory.Store.SnapshotAsync();
        var user = Assert.Single(snapshot.Accounts.Values, u => u.Email == "newfarmer@example.test");
        Assert.Equal("Farmer", user.Role);
        Assert.Equal("NotSubmitted", user.VerificationStatus);
    }

    [Fact]
    public async Task Register_DuplicateEmailOrPhone_RejectsWithConflict()
    {
        // Corresponds to IT-AUTH01-04
        using var factory = new ApiFactory();
        using var client = factory.Client(null);

        // Attempt to register with existing email (user10@example.test is seeded)
        var request = new RegisterRequest(
            FullName: "Trùng Email",
            PhoneNumber: "0988776655",
            Email: "user10@example.test",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Role: "RetailCustomer",
            AgreeTerms: true);

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("DUPLICATE_EMAIL", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VerifyOtp_IncorrectOtpMoreThan5Times_LocksAndInvalidates()
    {
        // Corresponds to IT-AUTH01-05
        using var factory = new ApiFactory();
        using var client = factory.Client(null);

        var request = new RegisterRequest(
            FullName: "Nguyễn Văn B",
            PhoneNumber: "0911223344",
            Email: "testlockedotp@example.test",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Role: "RetailCustomer",
            AgreeTerms: true);

        await client.PostAsJsonAsync("/api/auth/register", request);

        // Enter wrong OTP 5 times
        for (int i = 0; i < 5; i++)
        {
            var failResp = await client.PostAsJsonAsync("/api/auth/verify-otp",
                new VerifyOtpRequest("testlockedotp@example.test", "000000"));
            Assert.Equal(HttpStatusCode.BadRequest, failResp.StatusCode);
        }

        // 6th attempt should lock/invalidate OTP
        var lockedResp = await client.PostAsJsonAsync("/api/auth/verify-otp",
            new VerifyOtpRequest("testlockedotp@example.test", "123456"));
        Assert.Equal(HttpStatusCode.Forbidden, lockedResp.StatusCode);
        Assert.Contains("OTP_LOCKED", await lockedResp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResendOtp_ValidPendingRegistration_RenewsOtpTimer()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client(null);

        var request = new RegisterRequest(
            FullName: "Nguyễn Văn C",
            PhoneNumber: "0977665544",
            Email: "resendtest@example.test",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Role: "RetailCustomer",
            AgreeTerms: true);

        await client.PostAsJsonAsync("/api/auth/register", request);

        var resendResp = await client.PostAsJsonAsync("/api/auth/resend-otp",
            new ResendOtpRequest("resendtest@example.test"));
        Assert.Equal(HttpStatusCode.OK, resendResp.StatusCode);
    }
}
