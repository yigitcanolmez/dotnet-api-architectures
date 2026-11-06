using System.Runtime.Serialization;
using System.ServiceModel;

namespace Soa.LoanOrchestrator.Api.ServiceReferences;

// WSDL: http://localhost:5400/CreditScoringService.svc?wsdl
[ServiceContract(Namespace = Ns, Name = "ICreditScoringService")]
public interface ICreditScoringService
{
    const string Ns = "http://bank.example.com/credit/2026/01";

    // Async istemci metodu; tel üzerindeki operasyon adı "GetScore" (Async soneki atılır).
    // Parametre adı ("request") SOAP gövdesindeki eleman adıdır — sözleşmenin parçası!
    [OperationContract(Action = Ns + "/ICreditScoringService/GetScore", ReplyAction = Ns + "/ICreditScoringService/GetScoreResponse")]
    [FaultContract(typeof(ServiceFault), Action = Ns + "/ICreditScoringService/GetScoreServiceFaultFault", Name = "ServiceFault", Namespace = "http://bank.example.com/credit/2026/01")]
    Task<CreditScoreResponse> GetScoreAsync(CreditScoreRequest request);
}

[DataContract(Namespace = ICreditScoringService.Ns)]
public sealed class CreditScoreRequest
{
    [DataMember(Order = 1, IsRequired = true)] public string NationalId { get; set; } = "";
    [DataMember(Order = 2, IsRequired = true)] public decimal MonthlyIncome { get; set; }
}

[DataContract(Namespace = ICreditScoringService.Ns)]
public sealed class CreditScoreResponse
{
    [DataMember(Order = 1)] public int Score { get; set; }
    [DataMember(Order = 2)] public string RiskLevel { get; set; } = "";
}
