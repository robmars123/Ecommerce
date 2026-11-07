using Ecommerce.Catalog.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Catalog.Tests.Application;

public class StockStatusRollupTests
{
    private const StockStatus U = StockStatus.Unknown;
    private const StockStatus I = StockStatus.InStock;
    private const StockStatus L = StockStatus.Low;
    private const StockStatus O = StockStatus.Out;

    public static TheoryData<StockStatus[], StockStatus> Cases => new()
    {
        { [], O },
        { [U], O },
        { [U, U], O },
        { [I], I },
        { [L], L },
        { [O], O },
        { [I, O], I },
        { [L, O], L },
        { [I, L, O], I },
        { [O, O], O },
        { [U, O], O },
        { [U, L], L },
        { [U, I], I },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rolls_up_to_the_best_status_and_Out_when_nothing_is_in_stock_or_low(StockStatus[] input, StockStatus expected) =>
        Assert.Equal(expected, StockStatusRollup.Rollup(input));
}
