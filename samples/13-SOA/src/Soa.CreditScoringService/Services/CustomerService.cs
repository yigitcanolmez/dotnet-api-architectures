using CoreWCF;
using Soa.CreditScoringService.Contracts;

namespace Soa.CreditScoringService.Services;

[ServiceBehavior(Namespace = Namespaces.Customer, InstanceContextMode = InstanceContextMode.PerCall)]
public sealed class CustomerService : ICustomerService
{
    // Öğretim amaçlı bellek içi kayıt. Gerçek SOA'da burası çoğu zaman PAYLAŞILAN kurumsal DB'dir.
    private static readonly Dictionary<Guid, CustomerResponse> Customers = new[]
    {
        new CustomerResponse { CustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), FullName = "Ahmet Yılmaz", NationalId = "12345678901", MonthlyIncome = 45000m },
        new CustomerResponse { CustomerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), FullName = "Ayşe Kaya",    NationalId = "98765432109", MonthlyIncome = 18000m },
        new CustomerResponse { CustomerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), FullName = "Mehmet Demir", NationalId = "10000000000", MonthlyIncome = 9000m }
    }.ToDictionary(c => c.CustomerId);

    public CustomerResponse GetCustomer(GetCustomerRequest request) =>
        Customers.TryGetValue(request.CustomerId, out var c)
            ? c
            : throw new FaultException<ServiceFault>(
                new ServiceFault { Code = "CUSTOMER_NOT_FOUND", Message = $"Müşteri bulunamadı: {request.CustomerId}" },
                new FaultReason("Müşteri bulunamadı"));
}
