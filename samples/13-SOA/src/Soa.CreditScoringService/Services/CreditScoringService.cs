using CoreWCF;
using Soa.CreditScoringService.Contracts;

namespace Soa.CreditScoringService.Services;

// [Servis implementasyonu] Durumsuz (stateless): her çağrı bağımsız, sunucuda oturum yok.
// Soyutlama: tüketici skor algoritmasını bilmez; sadece sözleşmeyi (WSDL) görür.
[ServiceBehavior(Namespace = Namespaces.Credit, InstanceContextMode = InstanceContextMode.PerCall)]
public sealed class CreditScoringService(ILogger<CreditScoringService> logger) : ICreditScoringService
{
    public CreditScoreResponse GetScore(CreditScoreRequest request)
    {
        if (request.NationalId.Length != 11 || !request.NationalId.All(char.IsDigit))
            throw new FaultException<ServiceFault>(
                new ServiceFault { Code = "INVALID_NATIONAL_ID", Message = "TC kimlik no 11 haneli olmalıdır." },
                new FaultReason("Geçersiz TC kimlik no"));

        if (request.MonthlyIncome < 0)
            throw new FaultException<ServiceFault>(
                new ServiceFault { Code = "INVALID_INCOME", Message = "Gelir negatif olamaz." },
                new FaultReason("Geçersiz gelir"));

        // Basit, deterministik "algoritma": gelir katkısı + kimlik numarasından türeyen geçmiş katkısı.
        var incomePart = (int)Math.Min(request.MonthlyIncome / 100m, 300m);
        var historyPart = request.NationalId.Sum(c => c - '0') * 3 % 120;
        var score = Math.Clamp(400 + incomePart + historyPart, 300, 900);
        var risk = score >= 700 ? "Low" : score >= 550 ? "Medium" : "High";

        logger.LogInformation("SOAP GetScore: {NationalId} → {Score} ({Risk})", Mask(request.NationalId), score, risk);
        return new CreditScoreResponse { Score = score, RiskLevel = risk };
    }

    private static string Mask(string id) => id[..3] + "******" + id[^2..];
}
