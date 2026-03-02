using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Multi-Factor Authentication settings for a user.
/// Supports TOTP (Authenticator App) and SMS OTP.
/// </summary>
public class UserMfaSetting : BaseEntity
{
    public Guid UserId { get; private set; }
    public bool IsMfaEnabled { get; private set; }
    public string MfaMethod { get; private set; } = "none"; // none, totp, sms, passkey
    public string? TotpSecretKey { get; private set; } // Base32 encoded
    public bool IsTotpVerified { get; private set; }
    public string? PhoneNumber { get; private set; } // For SMS OTP
    public bool IsPhoneVerified { get; private set; }
    public string? RecoveryCodes { get; private set; } // JSON array of hashed codes
    public int FailedMfaAttempts { get; private set; }
    public DateTime? LastMfaAt { get; private set; }

    private UserMfaSetting() { }

    public static UserMfaSetting Create(Guid userId)
    {
        return new UserMfaSetting
        {
            UserId = userId,
            IsMfaEnabled = false,
            MfaMethod = "none"
        };
    }

    public void EnableTotp(string totpSecretKey)
    {
        TotpSecretKey = totpSecretKey;
        MfaMethod = "totp";
        IsTotpVerified = false;
        SetUpdated();
    }

    public void VerifyTotp()
    {
        IsTotpVerified = true;
        IsMfaEnabled = true;
        SetUpdated();
    }

    public void SetPhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;
        IsPhoneVerified = false;
        SetUpdated();
    }

    public void VerifyPhone()
    {
        IsPhoneVerified = true;
        if (MfaMethod == "none") MfaMethod = "sms";
        IsMfaEnabled = true;
        SetUpdated();
    }

    public void SetRecoveryCodes(string hashedCodesJson)
    {
        RecoveryCodes = hashedCodesJson;
        SetUpdated();
    }

    public void RecordMfaSuccess()
    {
        FailedMfaAttempts = 0;
        LastMfaAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void RecordMfaFailure()
    {
        FailedMfaAttempts++;
        SetUpdated();
    }

    public void DisableMfa()
    {
        IsMfaEnabled = false;
        MfaMethod = "none";
        TotpSecretKey = null;
        IsTotpVerified = false;
        PhoneNumber = null;
        IsPhoneVerified = false;
        RecoveryCodes = null;
        FailedMfaAttempts = 0;
        SetUpdated();
    }
}
