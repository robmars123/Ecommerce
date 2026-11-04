using System.Text.RegularExpressions;

namespace Ecommerce.Api.Observability;

public static partial class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    public const string LogProperty = "CorrelationId";
    public const int MaxLength = 64;

    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && AllowedPattern().IsMatch(value);

    public static string NewId() => Guid.NewGuid().ToString("N");

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex AllowedPattern();
}
