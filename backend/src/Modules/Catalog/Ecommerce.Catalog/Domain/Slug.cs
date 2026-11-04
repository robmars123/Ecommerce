using System.Text.RegularExpressions;

namespace Ecommerce.Catalog.Domain;

internal static partial class Slug
{
    public const int MaxLength = 100;

    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && Pattern().IsMatch(value);

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
