using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Profiles;

public sealed class ProfileService(
    IThreeUcUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<UpdateConsumerProfileRequest> consumerValidator,
    IValidator<UpdateFarmerProfileRequest> farmerValidator) : IProfileService
{
    public Task<Result<UserProfileResponse>> GetCurrentProfileAsync(
        CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để xem thông tin hồ sơ.", "Unauthorized"));
            }

            var profile = await session.Profiles.GetProfileAsync(currentUser.UserId.Value, cancellationToken);
            return profile == null
                ? Result<UserProfileResponse>.Failure(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"))
                : Result<UserProfileResponse>.Success(profile);
        }, cancellationToken);

    public async Task<Result<UserProfileResponse>> UpdateConsumerProfileAsync(
        UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(consumerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để cập nhật hồ sơ.", "Unauthorized"));
            }

            long userId = currentUser.UserId.Value;
            var current = await session.Profiles.GetProfileAsync(userId, cancellationToken);
            if (current == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản.", "NotFound"));
            }

            string before = JsonSerializer.Serialize(current);
            await session.Profiles.UpdateConsumerProfileAsync(userId, request, cancellationToken);

            DateTimeOffset now = timeProvider.GetUtcNow();
            var updated = await session.Profiles.GetProfileAsync(userId, cancellationToken);

            await session.AddAuditAsync(new AuditRecord(
                ActorId: userId,
                EntityType: "User",
                TargetId: userId,
                Action: "UpdateConsumerProfile",
                OldValue: before,
                NewValue: JsonSerializer.Serialize(updated),
                Reason: "Người dùng cập nhật thông tin cá nhân và địa chỉ mặc định.",
                OccurredAt: now), cancellationToken);

            return Result<UserProfileResponse>.Success(updated!);
        }, cancellationToken);
    }

    public async Task<Result<UserProfileResponse>> UpdateFarmerProfileAsync(
        UpdateFarmerProfileRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(farmerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để cập nhật hồ sơ nông trại.", "Unauthorized"));
            }

            long userId = currentUser.UserId.Value;
            var current = await session.Profiles.GetProfileAsync(userId, cancellationToken);
            if (current == null || current.FarmerProfile == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "FARMER_NOT_FOUND", "Tài khoản không phải là nông dân hoặc chưa khởi tạo hồ sơ nông trại.", "NotFound"));
            }

            // Check if modification of KYC-sensitive fields requires re-verification
            bool requiresReverification = false;
            if (current.FarmerProfile.VerificationStatus == "Approved")
            {
                bool legalChanged = !string.Equals(current.FarmerProfile.BusinessLicenseNumber, request.BusinessLicenseNumber?.Trim(), StringComparison.OrdinalIgnoreCase)
                                 || !string.Equals(current.FarmerProfile.RepresentativeName, request.RepresentativeName.Trim(), StringComparison.OrdinalIgnoreCase);
                if (legalChanged)
                {
                    requiresReverification = true;
                }
            }

            string before = JsonSerializer.Serialize(current.FarmerProfile);
            await session.Profiles.UpdateFarmerProfileAsync(userId, request, requiresReverification, cancellationToken);

            DateTimeOffset now = timeProvider.GetUtcNow();
            var updated = await session.Profiles.GetProfileAsync(userId, cancellationToken);

            await session.AddAuditAsync(new AuditRecord(
                ActorId: userId,
                EntityType: "FarmerProfile",
                TargetId: current.FarmerProfile.FarmerProfileId,
                Action: "UpdateFarmerProfile",
                OldValue: before,
                NewValue: JsonSerializer.Serialize(updated?.FarmerProfile),
                Reason: requiresReverification
                    ? "Cập nhật thông tin pháp lý KYC yêu cầu thẩm định lại."
                    : "Cập nhật thông tin quản lý nông trại.",
                OccurredAt: now), cancellationToken);

            return Result<UserProfileResponse>.Success(updated!);
        }, cancellationToken);
    }

    public Task<Result<UserProfileResponse>> SubmitFarmerKycAsync(
        SubmitKycRequest request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để nộp hồ sơ KYC.", "Unauthorized"));
            }

            long userId = currentUser.UserId.Value;
            var current = await session.Profiles.GetProfileAsync(userId, cancellationToken);
            if (current == null || current.FarmerProfile == null)
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "FARMER_NOT_FOUND", "Không tìm thấy hồ sơ nông trại.", "NotFound"));
            }

            if (string.IsNullOrWhiteSpace(current.FarmerProfile.FarmName) ||
                string.IsNullOrWhiteSpace(current.FarmerProfile.Address))
            {
                return Result<UserProfileResponse>.Failure(new Error(
                    "INCOMPLETE_KYC", "Vui lòng điền đầy đủ tên nông trại và địa chỉ trước khi nộp thẩm định.", "Validation"));
            }

            await session.Profiles.SubmitFarmerKycAsync(userId, request.Notes, cancellationToken);

            DateTimeOffset now = timeProvider.GetUtcNow();
            var updated = await session.Profiles.GetProfileAsync(userId, cancellationToken);

            await session.AddAuditAsync(new AuditRecord(
                ActorId: userId,
                EntityType: "FarmerProfile",
                TargetId: current.FarmerProfile.FarmerProfileId,
                Action: "SubmitKyc",
                OldValue: current.FarmerProfile.VerificationStatus,
                NewValue: "PendingApproval",
                Reason: request.Notes ?? "Nông dân nộp hồ sơ thẩm định KYC (UC-VO-01).",
                OccurredAt: now), cancellationToken);

            return Result<UserProfileResponse>.Success(updated!);
        }, cancellationToken);
}
