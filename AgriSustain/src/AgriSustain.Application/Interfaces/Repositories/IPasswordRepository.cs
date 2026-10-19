namespace AgriSustain.Application.Interfaces.Repositories;

public interface IPasswordRepository
{
    Task<string?> FindActiveHashAsync(long userId, CancellationToken cancellationToken);

    /// <summary>Atomically replaces the expected hash and writes a credential-free audit.</summary>
    Task<bool> ReplaceAsync(
        long userId, string expectedHash, string newHash, CancellationToken cancellationToken);
}
