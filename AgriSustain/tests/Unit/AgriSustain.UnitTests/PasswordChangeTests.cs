using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Validators.Authentication;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class PasswordChangeTests
{
    [Theory]
    [InlineData("", "NewPass123", "NewPass123", "MSG02")]
    [InlineData("wrong", "NewPass123", "NewPass123", "MSG04")]
    [InlineData("OldPass123", "weak", "weak", "MSG06")]
    [InlineData("OldPass123", "NewPassword", "NewPassword", "MSG06")]
    [InlineData("OldPass123", "12345678", "12345678", "MSG06")]
    [InlineData("OldPass123", "OldPass123", "OldPass123", "MSG12")]
    [InlineData("OldPass123", "NewPass123", "Different123", "MSG05")]
    public async Task ChangeAsync_InvalidInput_KeepsExistingPassword(
        string current, string next, string confirmation, string code)
    {
        var repository = new PasswordStore();
        var service = Create(repository);

        var result = await service.ChangeAsync(
            new ChangePasswordRequest(current, next, confirmation), default);

        Assert.Equal(code, result.Error?.Code);
        Assert.Equal("hash:OldPass123", repository.StoredHash);
    }

    [Fact]
    public async Task ChangeAsync_ValidInput_StoresHashAndAllowsNextChange()
    {
        var repository = new PasswordStore();
        var service = Create(repository);

        var result = await service.ChangeAsync(
            new("OldPass123", "NewPass123", "NewPass123"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash:NewPass123", repository.StoredHash);
        Assert.Equal("MSG04", (await service.ChangeAsync(
            new("OldPass123", "NextPass123", "NextPass123"), default)).Error?.Code);
    }

    [Fact]
    public async Task ChangeAsync_ConcurrentUpdate_DoesNotOverwritePassword()
    {
        var repository = new PasswordStore { RejectUpdate = true };
        var result = await Create(repository).ChangeAsync(
            new("OldPass123", "NewPass123", "NewPass123"), default);

        Assert.Equal("MSG07", result.Error?.Code);
        Assert.Equal("hash:OldPass123", repository.StoredHash);
    }

    [Fact]
    public async Task ChangeAsync_NoActiveAccount_RejectsRequest()
    {
        var repository = new PasswordStore { StoredHash = null };
        var result = await Create(repository).ChangeAsync(
            new("OldPass123", "NewPass123", "NewPass123"), default);

        Assert.Equal("Unauthorized", result.Error?.Kind);
    }

    private static PasswordChangeService Create(PasswordStore repository) => new(
        repository, new Hasher(), new CurrentUser(), new ChangePasswordValidator(),
        new AgriSustain.Domain.Entities.Accounts.PasswordPolicy());

    private sealed class CurrentUser : ICurrentUser
    {
        public long? UserId => 42;
    }

    private sealed class Hasher : IPasswordHasher
    {
        public bool Verify(string password, string hash) => hash == Hash(password);
        public string Hash(string password) => "hash:" + password;
    }

    private sealed class PasswordStore : IPasswordRepository
    {
        public string? StoredHash { get; set; } = "hash:OldPass123";
        public bool RejectUpdate { get; init; }

        public Task<string?> FindActiveHashAsync(long userId, CancellationToken cancellationToken)
            => Task.FromResult(StoredHash);

        public Task<bool> ReplaceAsync(
            long userId, string expectedHash, string newHash, CancellationToken cancellationToken)
        {
            if (RejectUpdate || StoredHash != expectedHash)
            {
                return Task.FromResult(false);
            }

            StoredHash = newHash;
            return Task.FromResult(true);
        }
    }
}
