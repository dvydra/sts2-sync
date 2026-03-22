namespace Sts2Sync.Core.Models;

public record AuthResult(
    string AccountName,
    string RefreshToken,
    string? GuardData
);
