namespace Soa.CreditScoringService.Contracts;

// [SOA Prensibi: Standartlaştırılmış Sözleşme] XML namespace'leri sözleşmenin kimliğidir ve
// versiyon içerir. Değiştirmek = sözleşmeyi kırmak (tüketiciler WSDL'den yeniden proxy üretir).
public static class Namespaces
{
    public const string Credit = "http://bank.example.com/credit/2026/01";
    public const string Customer = "http://bank.example.com/customer/2026/01";
}
