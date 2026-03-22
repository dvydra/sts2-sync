using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SaveFileHasherTests
{
    [Fact]
    public void ComputeSha256_ReturnsConsistentHash()
    {
        var hash1 = SaveFileHasher.ComputeSha256("hello world");
        var hash2 = SaveFileHasher.ComputeSha256("hello world");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeSha256_DifferentInputs_DifferentHashes()
    {
        var hash1 = SaveFileHasher.ComputeSha256("hello");
        var hash2 = SaveFileHasher.ComputeSha256("world");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeSha256_KnownValue()
    {
        // SHA-256 of "hello world"
        var hash = SaveFileHasher.ComputeSha256("hello world");
        Assert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", hash);
    }

    [Fact]
    public void ComputeSha1_KnownValue()
    {
        // SHA-1 of "hello world"
        var hash = SaveFileHasher.ComputeSha1("hello world");
        Assert.Equal("2aae6c35c94fcfb415dbe95f408b9ce91ee846ed", hash);
    }

    [Fact]
    public void AreEqual_IdenticalContent_ReturnsTrue()
    {
        var data = "same content"u8.ToArray();
        Assert.True(SaveFileHasher.AreEqual(data, data));
    }

    [Fact]
    public void AreEqual_DifferentContent_ReturnsFalse()
    {
        var left = "content a"u8.ToArray();
        var right = "content b"u8.ToArray();
        Assert.False(SaveFileHasher.AreEqual(left, right));
    }

    [Fact]
    public void ComputeSha256_BytesAndString_Match()
    {
        var text = "test data";
        var fromString = SaveFileHasher.ComputeSha256(text);
        var fromBytes = SaveFileHasher.ComputeSha256(System.Text.Encoding.UTF8.GetBytes(text));
        Assert.Equal(fromString, fromBytes);
    }
}
