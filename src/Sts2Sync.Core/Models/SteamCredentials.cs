namespace Sts2Sync.Core.Models;

public record SteamCredentials(
    string AccountName,
    string RefreshToken,
    string? GuardData
)
{
    // Prevent accidental credential leaks via ToString() / logging
    public override string ToString() => $"SteamCredentials {{ AccountName = {AccountName} }}";
}
