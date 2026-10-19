using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Profiles;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class ProfileIntegrationTests
{
    [Fact]
    public async Task GetProfile_RetailCustomer_ReturnsProfileAndAddress()
    {
        // Corresponds to IT-AUTH03-01 step 1-2
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer"); // User 10

        var response = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Khách mua demo", profile.FullName);
        Assert.Equal("RetailCustomer", profile.Role);
        Assert.NotNull(profile.DeliveryAddress);
        Assert.True(profile.RewardPoints > 0);
    }

    [Fact]
    public async Task UpdateProfile_RetailCustomer_SavesChangesAndAudit()
    {
        // Corresponds to IT-AUTH03-01 step 3-4
        using var factory = new ApiFactory();
        using var client = factory.Client("retail-customer");

        var updateReq = new UpdateConsumerProfileRequest(
            FullName: "Nguyễn Văn A Mới",
            PhoneNumber: "0912345678",
            Gender: "Nam",
            BirthDate: new DateOnly(1992, 8, 20),
            ProvinceCity: "Hà Nội",
            District: "Quận Cầu Giấy",
            Ward: "Phường Dịch Vọng Hậu",
            AddressDetail: "Số 99, Phố Duy Tân",
            ProvinceCode: "01",
            WardCode: "001");

        var response = await client.PutAsJsonAsync("/api/profile/consumer", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Nguyễn Văn A Mới", updated.FullName);

        var snapshot = await factory.Store.SnapshotAsync();
        Assert.Equal("Nguyễn Văn A Mới", snapshot.Accounts[10].Name);
        Assert.Contains(snapshot.Audits, a => a.Action == "UpdateConsumerProfile" && a.TargetId == 10);
    }

    [Fact]
    public async Task UpdateProfile_Farmer_CompletesFarmAndSubmitsKyc()
    {
        // Corresponds to IT-AUTH03-02
        using var factory = new ApiFactory();
        using var client = factory.Client("farmer"); // User 20

        var updateReq = new UpdateFarmerProfileRequest(
            FullName: "Trần Văn Nông",
            PhoneNumber: "0901234567",
            FarmName: "HTX Nông Nghiệp Xanh Lâm Đồng",
            RepresentativeName: "Trần Văn Nông",
            Address: "Xã Đạ Đờn, Huyện Lâm Hà, Lâm Đồng",
            ProvinceCode: "44",
            CultivationZone: "Lâm Đồng",
            ProductionScale: "5 ha",
            CultivatedArea: 2.5m,
            GPSCoordinates: "11.833, 108.267",
            BusinessLicenseNumber: "BL-2026-LD");

        var updateResp = await client.PutAsJsonAsync("/api/profile/farmer", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Submit for KYC Verification
        var kycResp = await client.PostAsJsonAsync("/api/profile/farmer/submit-kyc",
            new SubmitKycRequest("Nộp hồ sơ thẩm định KYC VietGAP."));
        Assert.Equal(HttpStatusCode.OK, kycResp.StatusCode);

        var profile = await kycResp.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile?.FarmerProfile);
        Assert.Equal("PendingApproval", profile.FarmerProfile.VerificationStatus);

        var snapshot = await factory.Store.SnapshotAsync();
        Assert.Contains(snapshot.Audits, a => a.Action == "SubmitKyc");
    }

    [Fact]
    public async Task UpdateProfile_Farmer_ChangingLegalFields_TriggersReverification()
    {
        // Corresponds to IT-AUTH03-03
        using var factory = new ApiFactory();
        using var client = factory.Client("farmer");

        var updateReq = new UpdateFarmerProfileRequest(
            FullName: "Trần Văn Nông",
            PhoneNumber: "0901234567",
            FarmName: "HTX Nông Nghiệp Xanh Lâm Đồng",
            RepresentativeName: "Người Đại Diện Khác Mới", // Changed legal rep
            Address: "Xã Đạ Đờn, Huyện Lâm Hà, Lâm Đồng",
            ProvinceCode: "44",
            CultivationZone: "Lâm Đồng",
            ProductionScale: "5 ha",
            CultivatedArea: 2.5m,
            GPSCoordinates: "11.833, 108.267",
            BusinessLicenseNumber: "BL-NEW-2026"); // Changed license

        var response = await client.PutAsJsonAsync("/api/profile/farmer", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(updated?.FarmerProfile);
        Assert.Equal("PendingApproval", updated.FarmerProfile.VerificationStatus);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_ReturnsUnauthorized()
    {
        using var factory = new ApiFactory();
        using var client = factory.Client(null); // Anonymous

        var response = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
