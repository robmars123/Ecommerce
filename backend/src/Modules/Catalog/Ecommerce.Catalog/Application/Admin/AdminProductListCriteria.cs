namespace Ecommerce.Catalog.Application.Admin;

internal enum AdminProductStatus
{
    All,
    Active,
    Inactive,
}

/// <summary>Admin list filters (D17). Parses and validates like <see cref="ProductSearchCriteria"/>.</summary>
internal sealed record AdminProductListCriteria(string? Search, string? CategorySlug, AdminProductStatus Status, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    public static AdminCriteriaResult Create(string? search, string? category, string? status, int? page, int? pageSize)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();

        string? trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (trimmedSearch is { Length: > MaxSearchLength })
        {
            errors["search"] = [$"search must be at most {MaxSearchLength} characters."];
        }

        string? trimmedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

        AdminProductStatus resolvedStatus = AdminProductStatus.All;
        if (!string.IsNullOrWhiteSpace(status))
        {
            resolvedStatus = status.Trim() switch
            {
                "all" => AdminProductStatus.All,
                "active" => AdminProductStatus.Active,
                "inactive" => AdminProductStatus.Inactive,
                _ => (AdminProductStatus)(-1),
            };
            if (!Enum.IsDefined(resolvedStatus))
            {
                errors["status"] = ["status must be one of: all, active, inactive."];
            }
        }

        if (page is < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        return errors.Count > 0
            ? new AdminCriteriaResult(null, errors)
            : new AdminCriteriaResult(
                new AdminProductListCriteria(trimmedSearch, trimmedCategory, resolvedStatus, page ?? 1, pageSize ?? DefaultPageSize),
                null);
    }
}

internal sealed record AdminCriteriaResult(
    AdminProductListCriteria? Criteria,
    IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool IsValid => Errors is null;
}
