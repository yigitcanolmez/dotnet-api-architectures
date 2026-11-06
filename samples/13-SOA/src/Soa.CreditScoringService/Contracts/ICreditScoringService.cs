using System.Runtime.Serialization;
using CoreWCF;

namespace Soa.CreditScoringService.Contracts;

// [Task/Business Service] "Kredi skoru hesapla" kurumsal iş yeteneği — portal, mobil, çağrı merkezi
// hepsi AYNI servisi yeniden kullanır (SOA'nın ana hedefi: yeniden kullanım).
[ServiceContract(Namespace = Namespaces.Credit)]
public interface ICreditScoringService
{
    [OperationContract]
    [FaultContract(typeof(ServiceFault))]
    CreditScoreResponse GetScore(CreditScoreRequest request);
}

[DataContract(Namespace = Namespaces.Credit)]
public sealed class CreditScoreRequest
{
    [DataMember(Order = 1, IsRequired = true)] public string NationalId { get; set; } = "";
    [DataMember(Order = 2, IsRequired = true)] public decimal MonthlyIncome { get; set; }
}

[DataContract(Namespace = Namespaces.Credit)]
public sealed class CreditScoreResponse
{
    [DataMember(Order = 1)] public int Score { get; set; }
    [DataMember(Order = 2)] public string RiskLevel { get; set; } = ""; // Low | Medium | High
}
