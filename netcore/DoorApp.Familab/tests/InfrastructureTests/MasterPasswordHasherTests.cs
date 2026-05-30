using DoorApp.Familab.Infrastructure.Auth;
using Xunit;

namespace DoorApp.Familab.Tests.InfrastructureTests;

public class MasterPasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_roundtrips()
    {
        var hash = MasterPasswordHasher.Hash("s3cret!");
        Assert.True(MasterPasswordHasher.Verify("s3cret!", hash));
        Assert.False(MasterPasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Default_shipped_hash_verifies_changeme()
    {
        const string shipped = "pbkdf2_sha256$100000$ABEiM0RVZneImqu8zd7v8A==$sb87PE5SVx+GvGG9tirFWcus4aBq3U/HPcKV5H66298=";
        Assert.True(MasterPasswordHasher.Verify("changeme", shipped));
        Assert.False(MasterPasswordHasher.Verify("Changeme", shipped));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("pbkdf2_sha256$abc$xx$yy")]
    public void Invalid_hash_returns_false(string? hash)
    {
        Assert.False(MasterPasswordHasher.Verify("anything", hash));
    }

    [Fact]
    public void Each_hash_uses_unique_salt()
    {
        Assert.NotEqual(MasterPasswordHasher.Hash("same"), MasterPasswordHasher.Hash("same"));
    }
}
