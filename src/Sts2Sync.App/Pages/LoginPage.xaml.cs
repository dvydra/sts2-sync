using QRCoder;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.App.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ISteamAuthService _authService;
    private readonly ICredentialStore _credentialStore;
    private TaskCompletionSource<string>? _codePromise;

    public LoginPage(ISteamAuthService authService, ICredentialStore credentialStore)
    {
        InitializeComponent();
        _authService = authService;
        _credentialStore = credentialStore;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Try auto-login with stored credentials
        var credentials = await _credentialStore.LoadAsync();
        if (credentials is not null)
        {
            SetLoading(true, "Logging in with saved credentials...");
            try
            {
                await _authService.LoginWithRefreshTokenAsync(credentials);
                await Shell.Current.GoToAsync("//sync");
                return;
            }
            catch
            {
                SetStatus("Saved credentials expired. Please log in again.");
            }
            finally
            {
                SetLoading(false);
            }
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetStatus("Enter username and password.");
            return;
        }

        SetLoading(true, "Connecting to Steam...");
        LoginButton.IsEnabled = false;

        try
        {
            var result = await _authService.LoginAsync(username, password, OnCodeRequested);

            // Save credentials for next time
            await _credentialStore.SaveAsync(new SteamCredentials(
                result.AccountName, result.RefreshToken, result.GuardData));

            await Shell.Current.GoToAsync("//sync");
        }
        catch (Exception ex)
        {
            SetStatus($"Login failed: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
            LoginButton.IsEnabled = true;
            TwoFactorSection.IsVisible = false;
        }
    }

    private Task<string> OnCodeRequested(AuthCodeRequest request)
    {
        _codePromise = new TaskCompletionSource<string>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var label = request.Type switch
            {
                AuthCodeType.EmailCode => "Enter the code sent to your email",
                AuthCodeType.DeviceCode => "Enter your Steam Guard code",
                AuthCodeType.DeviceConfirmation => "Confirm on your Steam mobile app",
                _ => "Enter confirmation code"
            };

            TwoFactorLabel.Text = label;
            TwoFactorSection.IsVisible = true;
            TwoFactorEntry.Text = "";
            TwoFactorEntry.Focus();
            SetLoading(false, "Waiting for 2FA code...");
        });

        return _codePromise.Task;
    }

    private void OnSubmitCodeClicked(object? sender, EventArgs e)
    {
        var code = TwoFactorEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(code) && _codePromise is not null)
        {
            SetLoading(true, "Verifying code...");
            _codePromise.TrySetResult(code);
        }
    }

    private async void OnQrLoginClicked(object? sender, EventArgs e)
    {
        SetLoading(true, "Generating QR code...");
        QrLoginButton.IsEnabled = false;
        LoginButton.IsEnabled = false;

        try
        {
            var result = await _authService.LoginViaQRAsync(challengeUrl =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    using var qrGenerator = new QRCodeGenerator();
                    var qrData = qrGenerator.CreateQrCode(challengeUrl, QRCodeGenerator.ECCLevel.M);
                    using var qrCode = new PngByteQRCode(qrData);
                    var pngBytes = qrCode.GetGraphic(10);

                    QrImage.Source = ImageSource.FromStream(() => new MemoryStream(pngBytes));
                    QrSection.IsVisible = true;
                    SetLoading(false, "Scan with Steam mobile app...");
                });
            });

            await _credentialStore.SaveAsync(new SteamCredentials(
                result.AccountName, result.RefreshToken, result.GuardData));

            await Shell.Current.GoToAsync("//sync");
        }
        catch (Exception ex)
        {
            SetStatus($"QR login failed: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
            QrLoginButton.IsEnabled = true;
            LoginButton.IsEnabled = true;
            QrSection.IsVisible = false;
        }
    }

    private void SetLoading(bool loading, string? message = null)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        if (message is not null)
            StatusLabel.Text = message;
    }

    private void SetStatus(string text)
    {
        StatusLabel.Text = text;
    }
}
