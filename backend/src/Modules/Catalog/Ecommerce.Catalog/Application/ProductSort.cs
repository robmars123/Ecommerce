namespace Ecommerce.Catalog.Application;

internal enum ProductSort
{
    Relevance,
    PriceAsc,
    PriceDesc,
    Newest,
    BestSelling,
}

internal static class ProductSortNames
{
    public const string Relevance = "relevance";
    public const string PriceAsc = "price-asc";
    public const string PriceDesc = "price-desc";
    public const string Newest = "newest";
    public const string BestSelling = "bestselling";

    public static bool TryParse(string value, out ProductSort sort)
    {
        switch (value)
        {
            case Relevance: sort = ProductSort.Relevance; return true;
            case PriceAsc: sort = ProductSort.PriceAsc; return true;
            case PriceDesc: sort = ProductSort.PriceDesc; return true;
            case Newest: sort = ProductSort.Newest; return true;
            case BestSelling: sort = ProductSort.BestSelling; return true;
            default: sort = default; return false;
        }
    }
}
