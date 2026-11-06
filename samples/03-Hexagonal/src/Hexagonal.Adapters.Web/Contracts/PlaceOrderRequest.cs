namespace Hexagonal.Adapters.Web.Contracts;

// HTTP'ye özgü istek modeli. Adapter bunu Core'un PlaceOrderCommand'ına çevirir;
// böylece JSON şekli değişse bile inbound port değişmez.
public record PlaceOrderRequest(Guid CustomerId, List<PlaceOrderRequestItem>? Items);

public record PlaceOrderRequestItem(Guid ProductId, int Quantity);
