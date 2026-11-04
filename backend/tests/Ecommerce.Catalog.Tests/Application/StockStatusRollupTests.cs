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
        { [], U },
        { [U], U },
        { [U, U], U },
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
    public void Rolls_up_known_statuses_ignoring_unknown(StockStatus[] input, StockStatus expected) =>
        Assert.Equal(expected, StockStatusRollup.Rollup(input));
}
