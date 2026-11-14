using System.Globalization;

namespace Ecommerce.Admin.Api;

internal static class QueryValues
{
    // Reads the first value of a query key as an int. A blank value is null; a bad one adds MVC's binding message to errors.
    public static int? ReadInt(string[]? values, string field, Dictionary<string, string[]> errors)
    {
        string? raw = values?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }

        errors[field] = [$"The value '{raw}' is not valid."];
        return null;
    }
}
