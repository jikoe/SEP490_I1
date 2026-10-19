namespace AgriSustain.Application.DTOs.Authentication;

public class UpdateAccountRequestDto
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}
