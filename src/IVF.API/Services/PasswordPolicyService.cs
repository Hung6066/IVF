using System.Text.RegularExpressions;

namespace IVF.API.Services;

/// <summary>
/// Enterprise Password Policy Engine aligned with NIST SP 800-63B (2024) +
/// Google Workspace + Microsoft Entra ID + AWS IAM best practices.
/// 
/// Key standards:
/// - NIST: Min 8 chars, check against breached password lists, no composition rules forced
/// - Google: Min 8 chars, strength meter, breached password check
/// - Microsoft: Min 8 chars, complexity optional, banned password list
/// - AWS IAM: Min 8 chars, require mixed case + number + symbol
///
/// This implementation combines all: complexity + breached list + entropy scoring.
/// </summary>
public sealed class PasswordPolicyService
{
    // Common breached passwords (top 100 from HaveIBeenPwned aggregated lists)
    private static readonly HashSet<string> BannedPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "12345678", "qwerty", "abc123", "monkey", "1234567",
        "letmein", "trustno1", "dragon", "baseball", "iloveyou", "master", "sunshine",
        "ashley", "bailey", "shadow", "123123", "654321", "superman", "qazwsx",
        "michael", "football", "password1", "password123", "admin", "admin123",
        "welcome", "welcome1", "p@ssw0rd", "passw0rd", "p@ssword", "changeme",
        "ivf", "ivfsystem", "hospital", "doctor", "clinic", "patient", "medical",
        "12345", "123456789", "1234567890", "0987654321", "qwerty123", "111111",
        "1q2w3e4r", "1q2w3e", "password!", "pa$$word", "p@ss1234", "admin@123",
        "test", "test123", "guest", "root", "toor", "pass", "pass123", "system"
    };

    public PasswordValidationResult Validate(string password, string? username = null)
    {
        var errors = new List<string>();

        // 1. Minimum length (NIST: 8, Enterprise: 12 recommended)
        if (password.Length < 10)
            errors.Add("Mật khẩu phải có ít nhất 10 ký tự");

        // 2. Maximum length (NIST: at least 64, prevent DoS on hashing)
        if (password.Length > 128)
            errors.Add("Mật khẩu không được vượt quá 128 ký tự");

        // 3. Complexity requirements (Microsoft Entra ID style)
        int categories = 0;
        if (Regex.IsMatch(password, @"[a-z]")) categories++;
        if (Regex.IsMatch(password, @"[A-Z]")) categories++;
        if (Regex.IsMatch(password, @"[0-9]")) categories++;
        if (Regex.IsMatch(password, @"[^a-zA-Z0-9]")) categories++;

        if (categories < 3)
            errors.Add("Mật khẩu phải chứa ít nhất 3 trong 4 loại: chữ thường, chữ hoa, số, ký tự đặc biệt");

        // 4. Banned password check (NIST breached password list)
        if (BannedPasswords.Contains(password))
            errors.Add("Mật khẩu này quá phổ biến và không an toàn");

        // 5. Username similarity check (Microsoft/Google standard)
        if (!string.IsNullOrEmpty(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase))
            errors.Add("Mật khẩu không được chứa tên đăng nhập");

        // 6. Repetitive/sequential pattern detection
        if (HasRepetitivePattern(password))
            errors.Add("Mật khẩu không được chứa các ký tự lặp lại liên tiếp (ví dụ: aaa, 111)");

        if (HasSequentialPattern(password))
            errors.Add("Mật khẩu không được chứa chuỗi ký tự liên tiếp (ví dụ: abc, 123)");

        // 7. Entropy scoring (Google style)
        var entropy = CalculateEntropy(password);

        return new PasswordValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Strength: entropy switch
            {
                >= 60 => PasswordStrength.VeryStrong,
                >= 45 => PasswordStrength.Strong,
                >= 30 => PasswordStrength.Medium,
                >= 15 => PasswordStrength.Weak,
                _ => PasswordStrength.VeryWeak
            },
            EntropyBits: entropy
        );
    }

    private static bool HasRepetitivePattern(string password)
    {
        for (int i = 2; i < password.Length; i++)
        {
            if (password[i] == password[i - 1] && password[i] == password[i - 2])
                return true;
        }
        return false;
    }

    private static bool HasSequentialPattern(string password)
    {
        for (int i = 2; i < password.Length; i++)
        {
            if (password[i] - password[i - 1] == 1 && password[i - 1] - password[i - 2] == 1)
                return true;
            if (password[i] - password[i - 1] == -1 && password[i - 1] - password[i - 2] == -1)
                return true;
        }
        return false;
    }

    private static double CalculateEntropy(string password)
    {
        int poolSize = 0;
        if (Regex.IsMatch(password, @"[a-z]")) poolSize += 26;
        if (Regex.IsMatch(password, @"[A-Z]")) poolSize += 26;
        if (Regex.IsMatch(password, @"[0-9]")) poolSize += 10;
        if (Regex.IsMatch(password, @"[^a-zA-Z0-9]")) poolSize += 32;

        return poolSize > 0 ? password.Length * Math.Log2(poolSize) : 0;
    }
}

public record PasswordValidationResult(
    bool IsValid,
    List<string> Errors,
    PasswordStrength Strength,
    double EntropyBits);

public enum PasswordStrength
{
    VeryWeak,
    Weak,
    Medium,
    Strong,
    VeryStrong
}
