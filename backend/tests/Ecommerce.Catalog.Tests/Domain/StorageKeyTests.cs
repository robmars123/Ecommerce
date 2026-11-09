using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class StorageKeyTests
{
    [Fact]
    public void A_product_image_key_is_lower_case_valid_and_built_from_the_ids_and_extension()
    {
        Guid product = Guid.Parse("AAAAAAAA-0000-0000-0000-00000000000A");
        Guid image = Guid.Parse("BBBBBBBB-0000-0000-0000-00000000000B");

        string key = StorageKey.ForProductImage(product, image, "png");

        Assert.Equal("products/aaaaaaaa-0000-0000-0000-00000000000a/bbbbbbbb-0000-0000-0000-00000000000b.png", key);
        Assert.True(StorageKey.IsValid(key));
    }
}
