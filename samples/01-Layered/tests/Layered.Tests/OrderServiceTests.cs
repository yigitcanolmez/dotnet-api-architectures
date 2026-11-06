using Layered.Business.Dtos;
using Layered.Business.Exceptions;
using Layered.Business.Services;
using Layered.DataAccess.Entities;
using Layered.DataAccess.Repositories;

namespace Layered.Tests;

// Bu testler "unit test" gibi görünse de aslında INTEGRATION test:
// her biri EF Core + SQLite şeması kuruyor. Saf iş kuralı testi yazmak katmanlı mimaride mümkün değil,
// çünkü Business katmanı DataAccess'in somut sınıflarına bağımlı.
// Karşılaştırın: ../../02-Onion/tests/Onion.Domain.Tests (DB yok, sadece `new Order(...)`).
public class OrderServiceTests : IDisposable
{
    private static readonly Guid Klavye = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly SqliteTestDatabase _database = new();

    // Servisi oluşturmak için bile DbContext → Repository zincirini elle kurmak gerekiyor.
    private OrderService CreateService()
    {
        var db = _database.CreateContext();
        return new OrderService(new OrderRepository(db), new ProductRepository(db));
    }

    [Fact]
    public async Task PlaceAsync_calculates_total_from_product_prices()
    {
        var service = CreateService();

        var id = await service.PlaceAsync(new PlaceOrderRequest(Guid.NewGuid(), [new(Klavye, 2)]));

        var order = await CreateService().GetAsync(id);
        Assert.Equal(1500.00m, order.Total);
        Assert.Equal("Placed", order.Status);
    }

    [Fact]
    public async Task CancelAsync_throws_when_order_is_shipped()
    {
        // Arrange: "Shipped" bir sipariş elde etmek için ya servisi iki kez çağırmalı
        // ya da veritabanına doğrudan satır yazmalıyız. (Anemic model: public set ile Status'ü direkt atıyoruz.)
        await using (var db = _database.CreateContext())
        {
            db.Orders.Add(new Order { Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), CustomerId = Guid.NewGuid(), Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CancelAsync(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")));
        Assert.Contains("iptal edilemez", ex.Message);
    }

    [Fact]
    public async Task PlaceAsync_throws_for_unknown_product()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessException>(
            () => service.PlaceAsync(new PlaceOrderRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)])));
    }

    public void Dispose() => _database.Dispose();
}
