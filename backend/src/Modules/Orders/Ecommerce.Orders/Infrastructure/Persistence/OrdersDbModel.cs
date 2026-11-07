using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Orders.Infrastructure.Persistence;

internal sealed class OrdersDbModel : IModuleDbModel
{
    public const string OrderNumberSequence = "order_number_seq";

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OrdersDbModel).Assembly,
            t => t.Namespace == typeof(OrdersDbModel).Namespace);
        // Order numbers (Slice 4 D15): a sequence is lock-free, unlike max()+1.
        modelBuilder.HasSequence<long>(OrderNumberSequence);
    }
}
