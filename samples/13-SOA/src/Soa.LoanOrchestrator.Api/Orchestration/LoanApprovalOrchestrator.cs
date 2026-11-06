using System.Collections.Concurrent;
using Soa.LoanOrchestrator.Api.ServiceReferences;

namespace Soa.LoanOrchestrator.Api.Orchestration;

// [Orchestration Service] Birden çok servisi (Customer = Entity Service, CreditScoring = Task Service)
// belirli bir sırayla çağırıp tek bir iş sürecine (kredi başvurusu) BİRLEŞTİRİR (composability).
// Gerçek SOA'da bu akış çoğunlukla ESB içinde BPEL/BizTalk orchestration olarak yaşardı.
public sealed class LoanApprovalOrchestrator(SoaChannels soa, ILogger<LoanApprovalOrchestrator> logger)
{
    private static readonly ConcurrentDictionary<Guid, LoanApplication> Store = new();

    public async Task<LoanApplication> ProcessAsync(LoanApplicationRequest req)
    {
        // 1) Entity Service: müşteri bilgisi (SOAP)
        var customer = await soa.Customer(c => c.GetCustomerAsync(new GetCustomerRequest { CustomerId = req.CustomerId }));
        logger.LogInformation("1/3 CustomerService.GetCustomer → {Name}", customer.FullName);

        // 2) Task Service: kredi skoru (SOAP) — müşteri servisinden gelen veriyle (protokol/veri aracılığı)
        var score = await soa.CreditScoring(c => c.GetScoreAsync(new CreditScoreRequest
        {
            NationalId = customer.NationalId,
            MonthlyIncome = customer.MonthlyIncome
        }));
        logger.LogInformation("2/3 CreditScoringService.GetScore → {Score} ({Risk})", score.Score, score.RiskLevel);

        // 3) Süreç kuralı: karar
        var installment = Math.Round(req.Amount / req.TermMonths, 2);
        var (decision, reason) = LoanDecisionPolicy.Decide(score.RiskLevel, installment, customer.MonthlyIncome);
        logger.LogInformation("3/3 Karar → {Decision}", decision);

        var application = new LoanApplication(Guid.NewGuid(), customer.CustomerId, customer.FullName, req.Amount, req.TermMonths,
            installment, score.Score, score.RiskLevel, decision, reason, DateTime.UtcNow);
        Store[application.Id] = application;
        return application;
    }

    public LoanApplication? Get(Guid id) => Store.GetValueOrDefault(id);
    public IEnumerable<LoanApplication> List() => Store.Values.OrderByDescending(a => a.CreatedAt);
}
