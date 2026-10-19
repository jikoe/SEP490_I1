using System.Text;

namespace AgriSustain.Domain.Entities.Accounts;

public sealed class PasswordPolicy
{
    public int MinimumLength { get; init; } = 8;
    public bool RequireUppercase { get; init; }
    public bool RequireLowercase { get; init; }
    public bool RequireDigit { get; init; } = true;

    public bool IsSatisfiedBy(string password) => password.Length >= MinimumLength
        && Encoding.UTF8.GetByteCount(password) <= 72
        && !password.Contains('\0')
        && password.Any(char.IsLetter)
        && (!RequireUppercase || password.Any(char.IsUpper))
        && (!RequireLowercase || password.Any(char.IsLower))
        && (!RequireDigit || password.Any(char.IsDigit));
}
