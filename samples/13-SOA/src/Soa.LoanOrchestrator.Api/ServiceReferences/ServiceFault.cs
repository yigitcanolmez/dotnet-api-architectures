using System.Runtime.Serialization;

namespace Soa.LoanOrchestrator.Api.ServiceReferences;

// ── Tüketici tarafı sözleşme kopyaları ───────────────────────────────────────────────
// Bu klasördeki tipler, `dotnet-svcutil http://localhost:5400/...svc?wsdl` komutunun ÜRETECEĞİ
// koda eşdeğer, sadeleştirilmiş el yazımı proxy'lerdir. Servis projesine ProjectReference YOK:
// tüketici sağlayıcıyı sadece WSDL/XSD sözleşmesi üzerinden tanır (gevşek bağlılık).
[DataContract(Namespace = "http://bank.example.com/credit/2026/01")]
public sealed class ServiceFault
{
    [DataMember(Order = 1)] public string Code { get; set; } = "";
    [DataMember(Order = 2)] public string Message { get; set; } = "";
}
