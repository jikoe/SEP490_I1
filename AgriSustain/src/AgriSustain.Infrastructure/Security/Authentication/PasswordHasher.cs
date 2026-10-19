using System.Text.RegularExpressions;
using AgriSustain.Application.Interfaces.Identity;

namespace AgriSustain.Infrastructure.Security.Authentication;

public sealed class PasswordHasher : IPasswordHasher
{
    public bool Verify(string password, string hash)
    {
        if (!Regex.IsMatch(hash, @"\A\$2[aby]\$(0[4-9]|[12]\d|3[01])\$[./A-Za-z0-9]{53}\z"))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
}
