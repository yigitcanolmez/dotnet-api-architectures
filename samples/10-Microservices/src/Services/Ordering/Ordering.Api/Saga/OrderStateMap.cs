// [Ordering — Saga Persistence] OrderState'in EF Core eşlemesi (aynı ordering.db içinde "OrderSagas" tablosu).
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ordering.Api.Saga;

public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.ToTable("OrderSagas");
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.FailureReason).HasMaxLength(500);
        entity.Property(x => x.Version).IsConcurrencyToken();
    }
}
