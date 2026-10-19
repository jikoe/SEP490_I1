using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Validators.Profiles;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class ProfileValidationTests
{
    private readonly UpdateConsumerProfileValidator _consumerValidator = new();
    private readonly UpdateFarmerProfileValidator _farmerValidator = new();

    [Fact]
    public async Task ConsumerProfile_ValidInputs_PassesValidation()
    {
        var request = new UpdateConsumerProfileRequest(
            FullName: "Nguyễn Văn A",
            PhoneNumber: "0912345678",
            Gender: "Nam",
            BirthDate: new DateOnly(1990, 5, 15),
            ProvinceCity: "Hà Nội",
            District: "Quận Cầu Giấy",
            Ward: "Phường Dịch Vọng Hậu",
            AddressDetail: "Số 18, Phố Duy Tân",
            ProvinceCode: "01",
            WardCode: "001");

        var result = await _consumerValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "0912345678")] // Blank FullName
    [InlineData("Nguyễn Văn A", "123456")] // Invalid Phone
    [InlineData("Nguyễn Văn A", "")] // Empty Phone
    public async Task ConsumerProfile_InvalidInputs_FailsValidation(string name, string phone)
    {
        var request = new UpdateConsumerProfileRequest(
            name, phone, null, null, null, null, null, null, null, null);

        var result = await _consumerValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task FarmerProfile_ValidInputs_PassesValidation()
    {
        var request = new UpdateFarmerProfileRequest(
            FullName: "Trần Văn Nông",
            PhoneNumber: "0901234567",
            FarmName: "HTX Nông Nghiệp Xanh",
            RepresentativeName: "Trần Văn Nông",
            Address: "Xã Đạ Đờn, Huyện Lâm Hà, Tỉnh Lâm Đồng",
            ProvinceCode: "44",
            CultivationZone: "Lâm Đồng",
            ProductionScale: "5 ha",
            CultivatedArea: 2.5m,
            GPSCoordinates: "11.833, 108.267",
            BusinessLicenseNumber: "BL-2026-LD");

        var result = await _farmerValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task FarmerProfile_NegativeCultivatedArea_FailsValidation()
    {
        // Corresponds to IT-AUTH03-04: Negative cultivated area rejected
        var request = new UpdateFarmerProfileRequest(
            FullName: "Trần Văn Nông",
            PhoneNumber: "0901234567",
            FarmName: "HTX Nông Nghiệp Xanh",
            RepresentativeName: "Trần Văn Nông",
            Address: "Địa chỉ nông trại",
            ProvinceCode: "44",
            CultivationZone: null,
            ProductionScale: null,
            CultivatedArea: -2.0m, // Negative area
            GPSCoordinates: null,
            BusinessLicenseNumber: null);

        var result = await _farmerValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.CultivatedArea));
    }

    [Theory]
    [InlineData("", "0901234567", "HTX Xanh", "Đại diện", "Địa chỉ")] // Empty name
    [InlineData("Họ Tên", "0901234567", "", "Đại diện", "Địa chỉ")] // Empty farm name
    [InlineData("Họ Tên", "0901234567", "HTX Xanh", "", "Địa chỉ")] // Empty representative
    [InlineData("Họ Tên", "0901234567", "HTX Xanh", "Đại diện", "")] // Empty address
    public async Task FarmerProfile_MissingRequiredFields_FailsValidation(
        string name, string phone, string farmName, string rep, string address)
    {
        var request = new UpdateFarmerProfileRequest(
            name, phone, farmName, rep, address, null, null, null, null, null, null);

        var result = await _farmerValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
    }
}
