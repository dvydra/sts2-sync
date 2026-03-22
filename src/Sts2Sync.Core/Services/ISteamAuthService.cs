using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public interface ISteamAuthService
{
    /// <summary>
    /// First-time login with username + password + 2FA.
    /// codeProvider is called when Steam requires a 2FA code.
    /// </summary>
    Task<AuthResult> LoginAsync(
        string username,
        string password,
        Func<AuthCodeRequest, Task<string>> codeProvider,
        CancellationToken ct = default);

    /// <summary>
    /// Subsequent login using stored refresh token.
    /// </summary>
    Task LoginWithRefreshTokenAsync(
        SteamCredentials credentials,
        CancellationToken ct = default);

    /// <summary>
    /// Login via QR code — scan with Steam mobile app.
    /// onChallengeUrl is called with the URL to encode as QR.
    /// </summary>
    Task<AuthResult> LoginViaQRAsync(
        Action<string> onChallengeUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Clean disconnect from Steam.
    /// </summary>
    Task DisconnectAsync();

    bool IsLoggedIn { get; }
}
