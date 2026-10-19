using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using Db = AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession
{
    public async Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _db.User
            .AsNoTracking()
            .Include(u => u.DeliveryAddress)
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var defaultAddress = user.DeliveryAddress.FirstOrDefault(a => a.IsDefault)
            ?? user.DeliveryAddress.FirstOrDefault();

        DeliveryAddressDto? addressDto = defaultAddress == null ? null : new DeliveryAddressDto(
            defaultAddress.DeliveryAddressId,
            defaultAddress.RecipientName,
            defaultAddress.RecipientPhone,
            defaultAddress.ProvinceCity,
            defaultAddress.District,
            defaultAddress.Ward,
            defaultAddress.AddressDetail,
            defaultAddress.ProvinceCode,
            defaultAddress.WardCode,
            defaultAddress.IsDefault);

        FarmerProfileDto? farmerDto = user.FarmerProfile == null ? null : new FarmerProfileDto(
            user.FarmerProfile.FarmerProfileId,
            user.FarmerProfile.FarmName,
            user.FarmerProfile.RepresentativeName,
            user.FarmerProfile.ProductionScale,
            null,
            user.FarmerProfile.CultivationZone,
            user.FarmerProfile.GPSCoordinates,
            user.FarmerProfile.BusinessLicenseNumber,
            user.FarmerProfile.Address,
            user.FarmerProfile.ProvinceCode,
            user.FarmerProfile.VerificationStatus,
            user.FarmerProfile.ReputationScore,
            user.FarmerProfile.ApprovedAt.HasValue ? Utc(user.FarmerProfile.ApprovedAt.Value) : null,
            user.FarmerProfile.RejectReason);

        int rewardPoints = Points.GetValueOrDefault(userId, 100);

        return new UserProfileResponse(
            user.UserId,
            user.FullName,
            user.PhoneNumber,
            user.Email,
            user.Role,
            user.Status,
            user.AvatarUrl,
            null,
            null,
            rewardPoints,
            addressDto,
            farmerDto);
    }

    public async Task UpdateConsumerProfileAsync(
        long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.User
            .Include(u => u.DeliveryAddress)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) return;

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(request.AddressDetail) &&
            !string.IsNullOrWhiteSpace(request.ProvinceCity) &&
            !string.IsNullOrWhiteSpace(request.Ward))
        {
            var defaultAddress = user.DeliveryAddress.FirstOrDefault(a => a.IsDefault)
                ?? user.DeliveryAddress.FirstOrDefault();

            if (defaultAddress == null)
            {
                defaultAddress = new Db.DeliveryAddress
                {
                    UserId = userId,
                    RecipientName = request.FullName.Trim(),
                    RecipientPhone = request.PhoneNumber.Trim(),
                    ProvinceCity = request.ProvinceCity.Trim(),
                    District = request.District?.Trim(),
                    Ward = request.Ward.Trim(),
                    AddressDetail = request.AddressDetail.Trim(),
                    ProvinceCode = request.ProvinceCode,
                    WardCode = request.WardCode,
                    IsDefault = true,
                    Status = "Active"
                };
                _db.DeliveryAddress.Add(defaultAddress);
            }
            else
            {
                defaultAddress.RecipientName = request.FullName.Trim();
                defaultAddress.RecipientPhone = request.PhoneNumber.Trim();
                defaultAddress.ProvinceCity = request.ProvinceCity.Trim();
                defaultAddress.District = request.District?.Trim();
                defaultAddress.Ward = request.Ward.Trim();
                defaultAddress.AddressDetail = request.AddressDetail.Trim();
                if (!string.IsNullOrWhiteSpace(request.ProvinceCode)) defaultAddress.ProvinceCode = request.ProvinceCode;
                if (!string.IsNullOrWhiteSpace(request.WardCode)) defaultAddress.WardCode = request.WardCode;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFarmerProfileAsync(
        long userId, UpdateFarmerProfileRequest request, bool requiresReverification, CancellationToken cancellationToken)
    {
        var user = await _db.User
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) return;

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();

        if (user.FarmerProfile != null)
        {
            user.FarmerProfile.FarmName = request.FarmName.Trim();
            user.FarmerProfile.RepresentativeName = request.RepresentativeName.Trim();
            user.FarmerProfile.Address = request.Address.Trim();
            if (!string.IsNullOrWhiteSpace(request.ProvinceCode)) user.FarmerProfile.ProvinceCode = request.ProvinceCode.Trim();
            if (request.CultivationZone != null) user.FarmerProfile.CultivationZone = request.CultivationZone.Trim();
            if (request.ProductionScale != null) user.FarmerProfile.ProductionScale = request.ProductionScale.Trim();
            if (request.GPSCoordinates != null) user.FarmerProfile.GPSCoordinates = request.GPSCoordinates.Trim();
            if (request.BusinessLicenseNumber != null) user.FarmerProfile.BusinessLicenseNumber = request.BusinessLicenseNumber.Trim();

            if (requiresReverification)
            {
                user.FarmerProfile.VerificationStatus = "PendingApproval";
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitFarmerKycAsync(
        long userId, string? notes, CancellationToken cancellationToken)
    {
        var profile = await _db.FarmerProfile
            .FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);

        if (profile != null)
        {
            profile.VerificationStatus = "PendingApproval";
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
