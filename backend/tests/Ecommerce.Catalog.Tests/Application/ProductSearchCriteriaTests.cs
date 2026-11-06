using Ecommerce.Catalog.Application;

namespace Ecommerce.Catalog.Tests.Application;

public class ProductSearchCriteriaTests
{
    [Fact]
    public void Defaults_to_page_1_size_20_and_newest_without_search()
    {
        CriteriaResult r = ProductSearchCriteria.Create(null, null, null, null, null);

        Assert.True(r.IsValid);
        ProductSearchCriteria c = r.Criteria!;
        Assert.Equal(1, c.Page);
        Assert.Equal(20, c.PageSize);
        Assert.Equal(ProductSort.Newest, c.Sort);
        Assert.Null(c.Search);
        Assert.Null(c.CategorySlug);
    }

    [Fact]
    public void Defaults_to_relevance_when_search_is_present_and_trims_it()
    {
        CriteriaResult r = ProductSearchCriteria.Create("  shirt  ", null, null, null, null);

        Assert.True(r.IsValid);
        Assert.Equal("shirt", r.Criteria!.Search);
        Assert.Equal(ProductSort.Relevance, r.Criteria.Sort);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Whitespace_search_means_no_search(string search)
    {
        CriteriaResult r = ProductSearchCriteria.Create(search, null, null, null, null);

        Assert.True(r.IsValid);
        Assert.Null(r.Criteria!.Search);
        Assert.Equal(ProductSort.Newest, r.Criteria.Sort);
    }

    [Theory]
    [InlineData("price-asc", ProductSort.PriceAsc)]
    [InlineData("price-desc", ProductSort.PriceDesc)]
    [InlineData("newest", ProductSort.Newest)]
    public void Accepts_explicit_sorts(string wire, object expected)
    {
        CriteriaResult r = ProductSearchCriteria.Create(null, null, wire, null, null);

        Assert.True(r.IsValid);
        Assert.Equal((ProductSort)expected, r.Criteria!.Sort);
    }

    [Fact]
    public void Accepts_explicit_relevance_with_search()
    {
        CriteriaResult r = ProductSearchCriteria.Create("shirt", null, "relevance", null, null);

        Assert.True(r.IsValid);
        Assert.Equal(ProductSort.Relevance, r.Criteria!.Sort);
    }

    [Theory]
    [InlineData(0, null, "page")]
    [InlineData(-1, null, "page")]
    [InlineData(null, 0, "pageSize")]
    [InlineData(null, 101, "pageSize")]
    [InlineData(null, -5, "pageSize")]
    public void Rejects_out_of_range_page_and_page_size(int? page, int? pageSize, string field)
    {
        CriteriaResult r = ProductSearchCriteria.Create(null, null, null, page, pageSize);

        Assert.False(r.IsValid);
        Assert.Null(r.Criteria);
        Assert.Contains(field, r.Errors!.Keys);
    }

    [Fact]
    public void Accepts_page_size_boundaries_1_and_100()
    {
        Assert.True(ProductSearchCriteria.Create(null, null, null, 1, 1).IsValid);
        Assert.True(ProductSearchCriteria.Create(null, null, null, 1, 100).IsValid);
    }

    [Fact]
    public void Rejects_unknown_sort()
    {
        CriteriaResult r = ProductSearchCriteria.Create(null, null, "bogus", null, null);

        Assert.False(r.IsValid);
        Assert.Contains("sort", r.Errors!.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Rejects_relevance_without_search(string? search)
    {
        CriteriaResult r = ProductSearchCriteria.Create(search, null, "relevance", null, null);

        Assert.False(r.IsValid);
        Assert.Contains("sort", r.Errors!.Keys);
    }

    [Fact]
    public void Rejects_search_longer_than_100_characters_but_accepts_exactly_100()
    {
        CriteriaResult tooLong = ProductSearchCriteria.Create(new string('a', 101), null, null, null, null);
        Assert.False(tooLong.IsValid);
        Assert.Contains("search", tooLong.Errors!.Keys);

        Assert.True(ProductSearchCriteria.Create(new string('a', 100), null, null, null, null).IsValid);
    }

    [Fact]
    public void Collects_every_error_at_once()
    {
        CriteriaResult r = ProductSearchCriteria.Create(null, null, "bogus", 0, 0);

        Assert.False(r.IsValid);
        Assert.Equal(["page", "pageSize", "sort"], r.Errors!.Keys.Order().ToArray());
    }

    [Fact]
    public void Blank_category_means_no_category_filter()
    {
        CriteriaResult r = ProductSearchCriteria.Create(null, "  ", null, null, null);

        Assert.True(r.IsValid);
        Assert.Null(r.Criteria!.CategorySlug);
        Assert.Equal("tops", ProductSearchCriteria.Create(null, " tops ", null, null, null).Criteria!.CategorySlug);
    }
}
