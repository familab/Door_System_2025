using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Application.Services;
using Xunit;

namespace DoorApp.Familab.Tests.ApplicationTests;

public class AccessRulesServiceTests
{
    private static AccessRulesService Build(AuthOptions auth)
    {
        var options = new DoorOptions { Auth = auth };
        return new AccessRulesService(new TestOptionsMonitor<DoorOptions>(options));
    }

    [Fact]
    public void Empty_whitelist_allows_everyone()
    {
        var svc = Build(new AuthOptions());
        Assert.True(svc.IsEmailAllowed("anyone@example.com"));
    }

    [Fact]
    public void Exact_email_whitelist_is_enforced()
    {
        var svc = Build(new AuthOptions { WhitelistEmails = { "alice@example.com" } });
        Assert.True(svc.IsEmailAllowed("ALICE@example.com"));
        Assert.False(svc.IsEmailAllowed("bob@example.com"));
    }

    [Theory]
    [InlineData("*.domain.com", "user@sub.domain.com", true)]
    [InlineData("*.domain.com", "user@domain.com", true)]
    [InlineData("*.domain.com", "user@other.com", false)]
    [InlineData("domain.com", "user@domain.com", true)]
    [InlineData("domain.com", "user@sub.domain.com", false)]
    public void Domain_whitelist_patterns(string pattern, string email, bool expected)
    {
        var svc = Build(new AuthOptions { WhitelistDomains = { pattern } });
        Assert.Equal(expected, svc.IsEmailAllowed(email));
    }

    [Fact]
    public void Blank_email_is_rejected()
    {
        var svc = Build(new AuthOptions { WhitelistEmails = { "alice@example.com" } });
        Assert.False(svc.IsEmailAllowed(""));
    }
}
