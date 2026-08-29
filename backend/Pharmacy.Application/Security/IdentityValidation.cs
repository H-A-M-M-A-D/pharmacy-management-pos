using System.Net.Mail;
using System.Text.RegularExpressions;
using Pharmacy.Application.Common;

namespace Pharmacy.Application.Security;

public static partial class IdentityValidation
{
    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant();

    public static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length is < 3 or > 100 ||
            !UsernamePattern().IsMatch(username.Trim()))
        {
            throw new RequestValidationException(
                "Username must be 3-100 characters and contain only letters, numbers, dots, underscores, or hyphens.");
        }
    }

    public static void ValidateProfile(string fullName, string? email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length is < 2 or > 200)
        {
            throw new RequestValidationException("Full name must be 2-200 characters.");
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            (email.Trim().Length > 100 || !MailAddress.TryCreate(email.Trim(), out _)))
        {
            throw new RequestValidationException("Email address is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.Trim().Length > 20)
        {
            throw new RequestValidationException("Phone number cannot exceed 20 characters.");
        }
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 10 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new RequestValidationException(
                "Password must be at least 10 characters and include uppercase, lowercase, digit, and special characters.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
