namespace Soa.LoanOrchestrator.Api.Orchestration;

public sealed record LoanApplicationRequest(Guid CustomerId, decimal Amount, int TermMonths);

public sealed record LoanApplication(
    Guid Id, Guid CustomerId, string CustomerName, decimal Amount, int TermMonths, decimal MonthlyInstallment,
    int CreditScore, string RiskLevel, string Decision, string Reason, DateTime CreatedAt);
