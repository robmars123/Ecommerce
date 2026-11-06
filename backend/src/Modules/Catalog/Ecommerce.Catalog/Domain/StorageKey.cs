namespace Ecommerce.Catalog.Domain;

/// <summary>
/// An object-storage key, never a URL (architecture 8.7). Rejecting ':' blocks URL schemes, so an admin cannot point
/// an image at an arbitrary external host. Spaces are allowed; the URL resolver escapes them.
/// </summary>
internal static class StorageKey
{
    public const int MaxLength = 512;

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }
        if (value[0] == '/' || value.Any(c => char.IsControl(c) || c is '\\' or ':'))
        {
            return false;
        }
        foreach (string segment in value.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }
        }
        return true;
    }
}
