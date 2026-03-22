namespace Sts2Sync.Core.Models;

public enum AuthCodeType
{
    DeviceCode,
    EmailCode,
    DeviceConfirmation
}

public record AuthCodeRequest(
    AuthCodeType Type,
    string? EmailHint = null,
    bool PreviousCodeWasIncorrect = false
);
