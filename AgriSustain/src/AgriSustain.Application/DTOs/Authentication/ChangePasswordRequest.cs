namespace AgriSustain.Application.DTOs.Authentication;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);
