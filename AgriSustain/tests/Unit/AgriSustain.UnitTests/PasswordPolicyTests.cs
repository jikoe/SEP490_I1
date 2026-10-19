using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Infrastructure.Security.Authentication;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("newpass123")]
    [InlineData("NEWPASS123")]
    public void IsSatisfiedBy_LettersAndDigits_AcceptsSrsPasswordPolicy(string password)
    {
        Assert.True(new PasswordPolicy().IsSatisfiedBy(password));
    }

    [Fact]
    public void IsSatisfiedBy_UnicodePassword_EnforcesBcryptByteLimit()
    {
        var policy = new PasswordPolicy();

        Assert.True(policy.IsSatisfiedBy("Aa1" + new string('é', 34)));
        Assert.False(policy.IsSatisfiedBy("Aa1" + new string('é', 35)));
    }

    [Fact]
    public void Hash_ValidPassword_ProducesVerifiableSaltedHashes()
    {
        var hasher = new PasswordHasher();

        string first = hasher.Hash("NewPass123");
        string second = hasher.Hash("NewPass123");

        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("NewPass123", first));
        Assert.False(hasher.Verify("wrong", first));
        Assert.False(hasher.Verify("NewPass123", "!DEMO-NO-LOGIN-HASH!"));
    }
}
