using System.Runtime.Serialization;

namespace Soa.CreditScoringService.Contracts;

// SOAP Fault'un detay kısmı. WSDL'de <wsdl:fault> olarak yayınlanır; tüketici tipli olarak yakalar.
[DataContract(Namespace = Namespaces.Credit)]
public sealed class ServiceFault
{
    [DataMember(Order = 1)] public string Code { get; set; } = "";
    [DataMember(Order = 2)] public string Message { get; set; } = "";
}
