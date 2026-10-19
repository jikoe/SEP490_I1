namespace AgriSustain.Application.Interfaces.Identity;

public interface IPasswordHasher
{
    bool Verify(string password, string hash);
    string Hash(string password);
}
