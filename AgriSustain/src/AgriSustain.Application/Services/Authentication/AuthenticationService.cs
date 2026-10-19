using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Authentication;

public sealed class AuthenticationService(
    IThreeUcUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<RegisterRequest> registerValidator,
    IValidator<VerifyOtpRequest> verifyOtpValidator,
    IValidator<ResendOtpRequest> resendOtpValidator) : IAuthenticationService
{
    public async Task<Result<string>> InitiateRegistrationAsync(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(registerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            if (await session.Auth.EmailExistsAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken))
            {
                return Result<string>.Failure(new Error(
                    "DUPLICATE_EMAIL", "Email này đã được đăng ký trong hệ thống.", "Conflict"));
            }

            if (await session.Auth.PhoneExistsAsync(request.PhoneNumber.Trim(), cancellationToken))
            {
                return Result<string>.Failure(new Error(
                    "DUPLICATE_PHONE", "Số điện thoại này đã được đăng ký trong hệ thống.", "Conflict"));
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            // Default demo OTP or 6 digits
            string otp = "123456";
            var pending = new PendingRegistration(
                FullName: request.FullName.Trim(),
                PhoneNumber: request.PhoneNumber.Trim(),
                Email: request.Email.Trim().ToLowerInvariant(),
                PasswordHash: "!DEMO_HASH!" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.Password)),
                Role: request.Role.Trim(),
                OtpCode: otp,
                ExpiresAt: now.AddMinutes(5),
                CreatedAt: now,
                FailedAttempts: 0);

            await session.Auth.SavePendingRegistrationAsync(pending, cancellationToken);
            return Result<string>.Success("Mã OTP xác thực đã được gửi tới email của bạn.");
        }, cancellationToken);
    }

    public async Task<Result<AuthResponse>> VerifyOtpAndCompleteRegistrationAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(verifyOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<AuthResponse>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            string email = request.Email.Trim().ToLowerInvariant();
            PendingRegistration? pending = await session.Auth.FindPendingRegistrationAsync(email, cancellationToken);

            if (pending == null)
            {
                return Result<AuthResponse>.Failure(new Error(
                    "OTP_NOT_FOUND", "Không tìm thấy yêu cầu đăng ký cho email này. Vui lòng đăng ký lại.", "NotFound"));
            }

            if (pending.ExpiresAt < now)
            {
                await session.Auth.RemovePendingRegistrationAsync(email, cancellationToken);
                return Result<AuthResponse>.Failure(new Error(
                    "OTP_EXPIRED", "Mã OTP đã hết hạn. Vui lòng bấm gửi lại mã mới.", "Validation"));
            }

            if (pending.FailedAttempts >= 5)
            {
                await session.Auth.RemovePendingRegistrationAsync(email, cancellationToken);
                return Result<AuthResponse>.Failure(new Error(
                    "OTP_LOCKED", "Bạn đã nhập sai OTP quá 5 lần. Mã xác thực đã bị vô hiệu hóa.", "Forbidden"));
            }

            if (pending.OtpCode != request.OtpCode.Trim())
            {
                await session.Auth.UpdatePendingRegistrationAsync(
                    pending with { FailedAttempts = pending.FailedAttempts + 1 }, cancellationToken);

                return Result<AuthResponse>.Failure(new Error(
                    "INVALID_OTP", "Mã xác thực OTP không chính xác. Vui lòng kiểm tra lại.", "Validation"));
            }

            // Create user
            var user = await session.Auth.CreateUserAsync(
                pending.FullName, pending.PhoneNumber, pending.Email, pending.PasswordHash,
                pending.Role, now, cancellationToken);

            if (pending.Role == "Farmer")
            {
                await session.Auth.CreateFarmerProfileAsync(
                    user.Id, pending.FullName, pending.FullName, "Chưa cập nhật địa chỉ",
                    "01", "NotSubmitted", 5.00m, cancellationToken);
            }

            await session.Auth.RemovePendingRegistrationAsync(email, cancellationToken);

            await session.AddAuditAsync(new AuditRecord(
                ActorId: user.Id,
                EntityType: "User",
                TargetId: user.Id,
                Action: "Register",
                OldValue: "{}",
                NewValue: $"{{\"UserId\":{user.Id},\"Role\":\"{user.Role}\",\"Email\":\"{user.Email}\"}}",
                Reason: "Đăng ký tài khoản thành công qua OTP email.",
                OccurredAt: now), cancellationToken);

            return Result<AuthResponse>.Success(new AuthResponse(
                user.Id, user.Name, user.Email, user.Role, $"demo-token-{user.Id}",
                "MSG56: Đăng ký tài khoản thành công!"));
        }, cancellationToken);
    }

    public async Task<Result<string>> ResendOtpAsync(
        ResendOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(resendOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            string email = request.Email.Trim().ToLowerInvariant();
            PendingRegistration? pending = await session.Auth.FindPendingRegistrationAsync(email, cancellationToken);
            if (pending == null)
            {
                return Result<string>.Failure(new Error(
                    "OTP_NOT_FOUND", "Không tìm thấy yêu cầu đăng ký với email này.", "NotFound"));
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            var renewed = pending with
            {
                OtpCode = "123456",
                ExpiresAt = now.AddMinutes(5),
                FailedAttempts = 0
            };
            await session.Auth.UpdatePendingRegistrationAsync(renewed, cancellationToken);

            return Result<string>.Success("Mã OTP mới đã được gửi lại vào email của bạn.");
        }, cancellationToken);
    }
}
