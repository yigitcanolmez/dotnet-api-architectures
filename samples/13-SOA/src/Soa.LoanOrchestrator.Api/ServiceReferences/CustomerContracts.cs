using System.Runtime.Serialization;
using System.ServiceModel;

namespace Soa.LoanOrchestrator.Api.ServiceReferences;

// WSDL: http://localhost:5400/CustomerService.svc?wsdl
[ServiceContract(Namespace = Ns, Name = "ICustomerService")]
public interface ICustomerService
{
    const string Ns = "http://bank.example.com/customer/2026/01";

    [OperationContract(Action = Ns + "/ICustomerService/GetCustomer", ReplyAction = Ns + "/ICustomerService/GetCustomerResponse")]
    [FaultContract(typeof(ServiceFault), Action = Ns + "/ICustomerService/GetCustomerServiceFaultFault", Name = "ServiceFault", Namespace = "http://bank.example.com/credit/2026/01")]
    Task<CustomerResponse> GetCustomerAsync(GetCustomerRequest request);
}

[DataContract(Namespace = ICustomerService.Ns)]
public sealed class GetCustomerRequest
{
    [DataMember(Order = 1, IsRequired = true)] public Guid CustomerId { get; set; }
}

[DataContract(Namespace = ICustomerService.Ns)]
public sealed class CustomerResponse
{
    [DataMember(Order = 1)] public Guid CustomerId { get; set; }
    [DataMember(Order = 2)] public string FullName { get; set; } = "";
    [DataMember(Order = 3)] public string NationalId { get; set; } = "";
    [DataMember(Order = 4)] public decimal MonthlyIncome { get; set; }
}
