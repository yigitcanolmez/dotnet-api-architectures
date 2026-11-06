namespace ResilienceClient.Api.Infrastructure;

public sealed record PriceDto(Guid ProductId, string Name, decimal Price, string Currency);
