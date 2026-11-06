namespace Ecommerce.Catalog.Application;

internal sealed record ProductSearchCriteria(string? Search, string? CategorySlug, ProductSort Sort, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    public static CriteriaResult Create(string? search, string? category, string? sort, int? page, int? pageSize)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();

        string? trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (trimmedSearch is { Length: > MaxSearchLength })
        {
            errors["search"] = [$"search must be at most {MaxSearchLength} characters."];
        }

        string? trimmedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

        if (page is < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        ProductSort resolvedSort = trimmedSearch is null ? ProductSort.Newest : ProductSort.Relevance;
        if (!string.IsNullOrWhiteSpace(sort))
        {
            if (!ProductSortNames.TryParse(sort.Trim(), out resolvedSort))
            {
                errors["sort"] = ["sort must be one of: relevance, price-asc, price-desc, newest."];
            }
            else if (resolvedSort == ProductSort.Relevance && trimmedSearch is null)
            {
                errors["sort"] = ["sort=relevance requires a search term."];
            }
        }

        if (errors.Count > 0)
        {
            return new CriteriaResult(null, errors);
        }

        return new CriteriaResult(
            new ProductSearchCriteria(trimmedSearch, trimmedCategory, resolvedSort, page ?? 1, pageSize ?? DefaultPageSize),
            null);
    }
}

internal sealed record CriteriaResult(
    ProductSearchCriteria? Criteria,
    IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool IsValid => Errors is null;
}
