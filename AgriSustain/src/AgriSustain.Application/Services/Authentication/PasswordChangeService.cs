using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities.Accounts;
using FluentValidation;

namespace AgriSustain.Application.Services.Authentication;

public sealed class PasswordChangeService(
    IPasswordRepository repository,
    IPasswordHasher hasher,
    ICurrentUser currentUser,
    IValidator<ChangePasswordRequest> validator,
    PasswordPolicy policy) : IPasswordChangeService
{
    public async Task<Result<string>> ChangeAsync(
        ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Failure("UNAUTHENTICATED", "Vui lòng đăng nhập.", "Unauthorized");
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Failure("MSG02", "Vui lòng nhập đầy đủ các trường mật khẩu hợp lệ.");
        }

        string? hash = await repository.FindActiveHashAsync(userId, cancellationToken);
        if (hash == null)
        {
            return Failure("UNAUTHENTICATED", "Tài khoản không còn hoạt động.", "Unauthorized");
        }

        if (!hasher.Verify(request.CurrentPassword, hash))
        {
            return Failure("MSG04", "Mật khẩu hiện tại không chính xác. Vui lòng thử lại.");
        }

        if (!policy.IsSatisfiedBy(request.NewPassword))
        {
            return Failure("MSG06", "Mật khẩu mới không đáp ứng chính sách bảo mật.");
        }

        if (hasher.Verify(request.NewPassword, hash))
        {
            return Failure("MSG12", "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return Failure("MSG05", "Mật khẩu xác nhận không khớp.");
        }

        bool updated = await repository.ReplaceAsync(
            userId, hash, hasher.Hash(request.NewPassword), cancellationToken);
        return updated
            ? Result<string>.Success(
                "Đổi mật khẩu thành công. Hãy dùng mật khẩu mới khi đăng nhập.")
            : Failure("MSG07", "Không thể đổi mật khẩu. Vui lòng thử lại.", "Conflict");
    }

    private static Result<string> Failure(
        string code, string message, string kind = "Validation") =>
        Result<string>.Failure(new Error(code, message, kind));
}
