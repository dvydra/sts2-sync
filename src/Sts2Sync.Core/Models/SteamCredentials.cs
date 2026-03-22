namespace Sts2Sync.Core.Models;

public record SteamCredentials(
    string AccountName,
    string RefreshToken,
    string? GuardData
);
