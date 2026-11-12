namespace Ecommerce.Admin.Application;

internal static class AdminSearchTerm
{
    public const int MinLength = 2;
    public const int MaxLength = 100;

    public static bool TryParse(string? raw, out string term, out string? error)
    {
        term = raw?.Trim() ?? "";
        if (term.Length == 0)
        {
            error = "q is required.";
            return false;
        }
        if (term.Length is < MinLength or > MaxLength)
        {
            error = $"q must be between {MinLength} and {MaxLength} characters.";
            return false;
        }
        error = null;
        return true;
    }
}
