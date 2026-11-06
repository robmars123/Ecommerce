namespace Ecommerce.Identity.Domain;

internal static class EmailAddress
{
    public const int MaxLength = 254;
    private const int MinLength = 3;

    public static string Normalize(string? raw) => raw?.Trim() ?? "";

    /// <summary>
    /// Deliberately simple: delivery is proven by the reset flow, not by a regex. Exactly one at-sign, non-empty
    /// local and domain parts, no whitespace or control characters.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length < MinLength || value.Length > MaxLength)
        {
            return false;
        }
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return false;
        }
        int at = value.IndexOf('@');
        return at > 0 && at == value.LastIndexOf('@') && at < value.Length - 1;
    }
}
