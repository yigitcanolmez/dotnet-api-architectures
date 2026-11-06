// [SOA — Servis Sağlayıcı] CoreWCF ile iki SOAP servisi yayınlar (BasicHttpBinding = SOAP 1.1 / HTTP)
// ve sözleşmelerini WSDL olarak keşfedilebilir kılar:
//   http://localhost:5400/CreditScoringService.svc?wsdl
//   http://localhost:5400/CustomerService.svc?wsdl
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Soa.CreditScoringService.Contracts;
using Soa.CreditScoringService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceModelServices();
// Servis sınıfları DI'dan çözülür (constructor injection). PerCall: her çağrıda yeni örnek → durumsuzluk.
builder.Services.AddTransient<CreditScoringService>();
builder.Services.AddTransient<CustomerService>();
builder.Services.AddServiceModelMetadata();
// WSDL içindeki adresleri, isteğin Host başlığına göre üret (proxy/port değişse de doğru adres).
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

var app = builder.Build();

app.UseServiceModel(sb =>
{
    sb.AddService<CreditScoringService>(o => o.DebugBehavior.IncludeExceptionDetailInFaults = app.Environment.IsDevelopment());
    sb.AddServiceEndpoint<CreditScoringService, ICreditScoringService>(new BasicHttpBinding(), "/CreditScoringService.svc");

    sb.AddService<CustomerService>();
    sb.AddServiceEndpoint<CustomerService, ICustomerService>(new BasicHttpBinding(), "/CustomerService.svc");
});

// [Keşfedilebilirlik] ?wsdl ile sözleşme yayını (UDDI registry'nin küçük bir karşılığı).
app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;

app.Run();
