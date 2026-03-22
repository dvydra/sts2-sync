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
        Log($"LoginAsync: connecting for user '{username}'...");
        await _connection.ConnectAsync(ct);
        Log($"LoginAsync: connected, state={_connection.State}");

        Log("LoginAsync: calling BeginAuthSessionViaCredentialsAsync...");
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
        Log("LoginAsync: auth session started, polling for result...");

        var pollResult = await authSession.PollingWaitForResultAsync(ct);
        Log($"LoginAsync: poll complete, account={pollResult.AccountName}");

        Log("LoginAsync: logging on with refresh token...");
        await _connection.LogOnWithRefreshTokenAsync(pollResult.AccountName, pollResult.RefreshToken, ct);
        _isLoggedIn = true;
        Log("LoginAsync: logged in successfully");

        return new AuthResult(
            AccountName: pollResult.AccountName,
            RefreshToken: pollResult.RefreshToken,
            GuardData: pollResult.NewGuardData
        );
    }

    public async Task<AuthResult> LoginViaQRAsync(
        Action<string> onChallengeUrl,
        CancellationToken ct = default)
    {
        Log("LoginViaQRAsync: connecting...");
        await _connection.ConnectAsync(ct);
        Log($"LoginViaQRAsync: connected, state={_connection.State}");

        Log("LoginViaQRAsync: calling BeginAuthSessionViaQRAsync...");
        var authSession = await _connection.Client.Authentication.BeginAuthSessionViaQRAsync(
            new AuthSessionDetails
            {
                IsPersistentSession = true,
                DeviceFriendlyName = "STS2 Sync Android"
            });
        Log($"LoginViaQRAsync: challenge URL = {authSession.ChallengeURL}");

        onChallengeUrl(authSession.ChallengeURL);
        Log("LoginViaQRAsync: QR displayed, polling for scan...");

        var pollResult = await authSession.PollingWaitForResultAsync(ct);
        Log($"LoginViaQRAsync: poll complete, account={pollResult.AccountName}");

        Log("LoginViaQRAsync: logging on with refresh token...");
        await _connection.LogOnWithRefreshTokenAsync(pollResult.AccountName, pollResult.RefreshToken, ct);
        _isLoggedIn = true;
        Log("LoginViaQRAsync: logged in successfully");

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
        Log($"LoginWithRefreshTokenAsync: connecting for '{credentials.AccountName}'...");
        await _connection.ConnectAsync(ct);
        Log($"LoginWithRefreshTokenAsync: connected, logging on...");
        await _connection.LogOnWithRefreshTokenAsync(credentials.AccountName, credentials.RefreshToken, ct);
        _isLoggedIn = true;
        Log("LoginWithRefreshTokenAsync: logged in successfully");
    }

    public async Task DisconnectAsync()
    {
        Log("DisconnectAsync: disconnecting...");
        _isLoggedIn = false;
        await _connection.DisconnectAsync();
        Log("DisconnectAsync: done");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[SteamAuth] {message}");
    }

    private sealed class CallbackAuthenticator(
        Func<AuthCodeRequest, Task<string>> codeProvider) : IAuthenticator
    {
        public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            Console.WriteLine($"[SteamAuth] 2FA: device code requested (retry={previousCodeWasIncorrect})");
            return await codeProvider(new AuthCodeRequest(
                AuthCodeType.DeviceCode,
                PreviousCodeWasIncorrect: previousCodeWasIncorrect));
        }

        public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            Console.WriteLine($"[SteamAuth] 2FA: email code requested for {email} (retry={previousCodeWasIncorrect})");
            return await codeProvider(new AuthCodeRequest(
                AuthCodeType.EmailCode,
                EmailHint: email,
                PreviousCodeWasIncorrect: previousCodeWasIncorrect));
        }

        public Task<bool> AcceptDeviceConfirmationAsync()
        {
            Console.WriteLine("[SteamAuth] 2FA: device confirmation polling...");
            return Task.FromResult(true);
        }
    }
}
