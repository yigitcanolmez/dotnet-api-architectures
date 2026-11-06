// [SOA — Tüketici / Orchestration Service] Dış dünyaya REST (JSON) sunar, içeride SOAP servislerini
// (CoreWCF, :5400) System.ServiceModel istemcisiyle çağırır. Protokol köprüsü: REST ⇄ SOAP.
using System.ServiceModel;
using Soa.LoanOrchestrator.Api.Orchestration;
using Soa.LoanOrchestrator.Api.ServiceReferences;
using SoaFault = Soa.LoanOrchestrator.Api.ServiceReferences.ServiceFault;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<SoaChannels>();
builder.Services.AddScoped<LoanApprovalOrchestrator>();

var app = builder.Build();

app.MapGet("/", () => "Loan Orchestrator (REST → SOAP) - POST /loan-applications");

app.MapPost("/loan-applications", async (LoanApplicationRequest req, LoanApprovalOrchestrator orchestrator) =>
{
    var errors = new Dictionary<string, string[]>();
    if (req.Amount <= 0) errors["amount"] = ["Tutar 0'dan büyük olmalıdır."];
    if (req.TermMonths is < 1 or > 120) errors["termMonths"] = ["Vade 1-120 ay arasında olmalıdır."];
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    try
    {
        var application = await orchestrator.ProcessAsync(req);
        return Results.Created($"/loan-applications/{application.Id}", application);
    }
    catch (FaultException<SoaFault> fault) when (fault.Detail.Code == "CUSTOMER_NOT_FOUND")
    {
        return Results.Problem(fault.Detail.Message, statusCode: 404, title: "SOAP Fault: " + fault.Detail.Code);
    }
    catch (FaultException<SoaFault> fault)
    {
        return Results.Problem(fault.Detail.Message, statusCode: 422, title: "SOAP Fault: " + fault.Detail.Code);
    }
    catch (FaultException fault)
    {
        return Results.Problem(fault.Reason.ToString(), statusCode: 502, title: "SOAP Fault");
    }
    catch (TimeoutException)
    {
        return Results.Problem("SOAP servisi zamanında yanıt vermedi.", statusCode: 504);
    }
    catch (CommunicationException ex)
    {
        return Results.Problem($"SOAP servisine ulaşılamadı: {ex.Message}", statusCode: 503);
    }
});

app.MapGet("/loan-applications/{id:guid}", (Guid id, LoanApprovalOrchestrator orchestrator) =>
    orchestrator.Get(id) is { } a ? Results.Ok(a) : Results.Problem($"Başvuru bulunamadı: {id}", statusCode: 404));

app.MapGet("/loan-applications", (LoanApprovalOrchestrator orchestrator) => orchestrator.List());

app.Run();
