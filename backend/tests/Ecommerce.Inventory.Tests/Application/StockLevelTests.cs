using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Application;

namespace Ecommerce.Inventory.Tests.Application;

// Test 20 (unit half): arch 7.4 boundaries.
public class StockLevelTests
{
    [Theory]
    [InlineData(-1, 5, StockStatus.Out)]
    [InlineData(0, 5, StockStatus.Out)]
    [InlineData(1, 5, StockStatus.Low)]
    [InlineData(5, 5, StockStatus.Low)]
    [InlineData(6, 5, StockStatus.InStock)]
    [InlineData(1, 1, StockStatus.Low)]
    [InlineData(2, 1, StockStatus.InStock)]
    [InlineData(0, 1, StockStatus.Out)]
    public void T20_Classify_follows_the_threshold(int available, int threshold, StockStatus expected) =>
        Assert.Equal(expected, StockLevel.Classify(available, threshold));
}
