using SteamKit2;
using SteamKit2.Authentication;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class SteamAuthService : ISteamAuthService
{
    private readonly ISteamConnectionManager _connection;
    private bool _isLoggedIn;

    public bool IsLoggedIn => _isLoggedIn;

    public SteamAuthService(ISteamConnectionManager connection)
    {
        _connection = connection;
    }

    public async Task<AuthResult> LoginAsync(
        string username,
        string password,
        Func<AuthCodeRequest, Task<string>> codeProvider,
        CancellationToken ct = default)
    {
        await _connection.ConnectAsync(ct);

        var authSession = await _connection.Client.Authentication.BeginAuthSessionViaCredentialsAsync(
            new AuthSessionDetails
            {
                Username = username,
                Password = password,
                IsPersistentSession = true,
                DeviceFriendlyName = "STS2 Sync Android",
                Authenticator = new CallbackAuthenticator(codeProvider),
                GuardData = null
            });

        var pollResult = await authSession.PollingWaitForResultAsync(ct);

        // Now log on with the refresh token
        await _connection.LogOnWithRefreshTokenAsync(pollResult.AccountName, pollResult.RefreshToken, ct);
        _isLoggedIn = true;

        return new AuthResult(
            AccountName: pollResult.AccountName,
            RefreshToken: pollResult.RefreshToken,
            GuardData: pollResult.NewGuardData
        );
    }

    public async Task LoginWithRefreshTokenAsync(
        SteamCredentials credentials,
        CancellationToken ct = default)
    {
        await _connection.ConnectAsync(ct);
        await _connection.LogOnWithRefreshTokenAsync(credentials.AccountName, credentials.RefreshToken, ct);
        _isLoggedIn = true;
    }

    public async Task DisconnectAsync()
    {
        _isLoggedIn = false;
        await _connection.DisconnectAsync();
    }

    /// <summary>
    /// IAuthenticator implementation that delegates to a user-provided callback.
    /// </summary>
    private sealed class CallbackAuthenticator(
        Func<AuthCodeRequest, Task<string>> codeProvider) : IAuthenticator
    {
        public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            return await codeProvider(new AuthCodeRequest(
                AuthCodeType.DeviceCode,
                PreviousCodeWasIncorrect: previousCodeWasIncorrect));
        }

        public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            return await codeProvider(new AuthCodeRequest(
                AuthCodeType.EmailCode,
                EmailHint: email,
                PreviousCodeWasIncorrect: previousCodeWasIncorrect));
        }

        public Task<bool> AcceptDeviceConfirmationAsync()
        {
            // Return true to keep polling for mobile app confirmation.
            // The user can cancel via CancellationToken if they want to
            // fall back to entering a code manually.
            return Task.FromResult(true);
        }
    }
}
