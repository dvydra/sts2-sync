namespace Sts2Sync.Core.Models;

public record AuthResult(
    string AccountName,
    string RefreshToken,
    string? GuardData
)
{
    // Prevent accidental credential leaks via ToString() / logging
    public override string ToString() => $"AuthResult {{ AccountName = {AccountName} }}";
}
