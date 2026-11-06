namespace Soa.LoanOrchestrator.Api.Orchestration;

// Süreç kuralı (orchestration'a ait): skor servisi "risk"i söyler, onay/ret kararını süreç verir.
public static class LoanDecisionPolicy
{
    public static (string Decision, string Reason) Decide(string riskLevel, decimal monthlyInstallment, decimal monthlyIncome)
    {
        var ratio = monthlyIncome == 0 ? decimal.MaxValue : monthlyInstallment / monthlyIncome;
        return riskLevel switch
        {
            "Low" when ratio <= 0.50m => ("Approved", $"Düşük risk, taksit/gelir oranı %{ratio * 100:0} (≤ %50)."),
            "Medium" when ratio <= 0.30m => ("Approved", $"Orta risk, taksit/gelir oranı %{ratio * 100:0} (≤ %30)."),
            "Low" or "Medium" => ("Rejected", $"{riskLevel} risk için taksit/gelir oranı çok yüksek: %{ratio * 100:0}."),
            _ => ("Rejected", "Yüksek kredi riski.")
        };
    }
}
