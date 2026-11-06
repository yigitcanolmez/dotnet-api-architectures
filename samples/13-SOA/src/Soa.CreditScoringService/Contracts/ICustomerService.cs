using System.Runtime.Serialization;
using CoreWCF;

namespace Soa.CreditScoringService.Contracts;

// [Entity Service] Veri odaklı servis: müşteri bilgisinin kurumdaki tek "resmi" kaynağı.
[ServiceContract(Namespace = Namespaces.Customer)]
public interface ICustomerService
{
    [OperationContract]
    [FaultContract(typeof(ServiceFault))]
    CustomerResponse GetCustomer(GetCustomerRequest request);
}

[DataContract(Namespace = Namespaces.Customer)]
public sealed class GetCustomerRequest
{
    [DataMember(Order = 1, IsRequired = true)] public Guid CustomerId { get; set; }
}

[DataContract(Namespace = Namespaces.Customer)]
public sealed class CustomerResponse
{
    [DataMember(Order = 1)] public Guid CustomerId { get; set; }
    [DataMember(Order = 2)] public string FullName { get; set; } = "";
    [DataMember(Order = 3)] public string NationalId { get; set; } = "";
    [DataMember(Order = 4)] public decimal MonthlyIncome { get; set; }
}
